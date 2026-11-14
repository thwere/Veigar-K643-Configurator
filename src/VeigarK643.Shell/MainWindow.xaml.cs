using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace VeigarK643.Shell;

public partial class MainWindow : Window
{
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpDoNotRound = 1;

    private enum EditorTool { Brush, Fill, Select, Eraser }
    private readonly Dictionary<string, List<Border>> _keyGlows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Rect>> _keyBounds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Color> _keyColors = new(StringComparer.Ordinal);
    private readonly HashSet<string> _selectedKeys = new(StringComparer.Ordinal);
    private readonly List<HashSet<string>> _groups = new();
    private EditorTool _activeTool = EditorTool.Brush;
    private Color _currentColor = Colors.Red;
    private bool _toolsPanelIsOpen;
    private bool _isPointerDown;
    private bool _isMarquee;
    private bool _shiftAtStart;
    private Point _dragStart;
    private string? _lastPaintedKey;
    private Rectangle? _marquee;
    private Canvas? _colorField;
    private Ellipse? _colorThumb;
    private Border? _colorPreview;
    private TextBlock? _colorHexText;
    private StackPanel? _colorControls;
    private Button? _groupButton;
    private Button? _fillButton;
    private TextBlock? _toolHelpText;
    private Slider? _brightnessSlider;
    private Slider? _saturationSlider;
    private double _selectedHue;
    private double _selectedValue = 1;
    private readonly DispatcherTimer _hardwareUpdateTimer = new() { Interval = TimeSpan.FromMilliseconds(45) };

    public MainWindow()
    {
        InitializeComponent();
        FontFamily captionFont = GetCaptionFontFamily();
        MinimizeGlyph.FontFamily = captionFont;
        CloseGlyph.FontFamily = captionFont;
        CreateKeyVisuals();
        BuildToolbar();
        BuildToolsPanel();
        SetActiveTool(EditorTool.Brush);
        SourceInitialized += (_, _) => DisableRoundedCorners();
        Deactivated += (_, _) => ReleasePointer();
        _hardwareUpdateTimer.Tick += async (_, _) =>
        {
            _hardwareUpdateTimer.Stop();
            await SendHardwareFrameAsync();
        };
    }

    private void CreateKeyVisuals()
    {
        Uri layoutUri = new("pack://application:,,,/assets/layout/veigar-k643-layout.json", UriKind.Absolute);
        var resource = Application.GetResourceStream(layoutUri);
        if (resource is null) throw new InvalidOperationException("Keyboard layout resource was not found.");
        using Stream stream = resource.Stream;
        KeyboardLayoutConfig? layout = JsonSerializer.Deserialize<KeyboardLayoutConfig>(
            stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (layout is null) throw new InvalidOperationException("Keyboard layout resource is empty or invalid.");

        foreach (KeyboardKeyConfig keyConfig in layout.Keys)
        foreach (KeyboardKeyArea area in keyConfig.Areas)
        {
            if (area.Width <= 0 || area.Height <= 0) continue;
            AddKeyVisuals(keyConfig.Key, area.X, area.Y, area.Width, area.Height);
        }

        KeyHitboxCanvas.PreviewMouseLeftButtonDown += Keyboard_MouseLeftButtonDown;
        KeyHitboxCanvas.PreviewMouseMove += Keyboard_MouseMove;
        KeyHitboxCanvas.PreviewMouseLeftButtonUp += Keyboard_MouseLeftButtonUp;
    }

    private void AddKeyVisuals(string keyName, double left, double top, double width, double height)
    {
        if (!_keyGlows.ContainsKey(keyName))
        {
            _keyGlows[keyName] = new List<Border>();
            _keyBounds[keyName] = new List<Rect>();
        }

        Border glow = new()
        {
            Width = width, Height = height, Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(5), Opacity = 0, IsHitTestVisible = false
        };
        Canvas.SetLeft(glow, left);
        Canvas.SetTop(glow, top);
        KeyGlowCanvas.Children.Add(glow);
        _keyGlows[keyName].Add(glow);

        const double hitboxScale = 1.08;
        double hitboxWidth = width * hitboxScale;
        double hitboxHeight = height * hitboxScale;
        double hitboxLeft = left - (hitboxWidth - width) / 2;
        double hitboxTop = top - (hitboxHeight - height) / 2;
        _keyBounds[keyName].Add(new Rect(hitboxLeft, hitboxTop, hitboxWidth, hitboxHeight));

        Border hitbox = new()
        {
            Width = hitboxWidth, Height = hitboxHeight, Tag = keyName,
            Background = Brushes.Transparent, BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(4),
            Cursor = Cursors.Hand, Focusable = false
        };
        Canvas.SetLeft(hitbox, hitboxLeft);
        Canvas.SetTop(hitbox, hitboxTop);
        KeyHitboxCanvas.Children.Add(hitbox);
    }

    private void BuildToolbar()
    {
        ToolbarHost.Children.Clear();
        StackPanel tools = new()
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(10, 0, 0, 0)
        };
        AddToolButton(tools, "✎", "Кисть", EditorTool.Brush);
        AddToolButton(tools, "▧", "Заливка", EditorTool.Fill);
        AddToolButton(tools, "□", "Выделение", EditorTool.Select);
        AddToolButton(tools, "⌫", "Ластик", EditorTool.Eraser);
        ToolbarHost.Children.Add(tools);
    }

    private void AddToolButton(Panel host, string glyph, string label, EditorTool tool)
    {
        Button button = new()
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new TextBlock { Text = glyph, FontSize = 16, Margin = new Thickness(0,0,7,0) },
                    new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }
                }
            },
            Tag = tool, Height = 32, MinWidth = 84, Margin = new Thickness(2,0,2,0),
            Padding = new Thickness(9,0,9,0), Foreground = new SolidColorBrush(Color.FromRgb(220,223,230)),
            Background = new SolidColorBrush(Color.FromRgb(42,46,56)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(62,67,79)), BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand, Focusable = false
        };
        button.Click += (_, _) => SetActiveTool(tool);
        host.Children.Add(button);
    }

    private void BuildToolsPanel()
    {
        ToolsPanelContent.Children.Clear();
        ScrollViewer scroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        StackPanel root = new() { Margin = new Thickness(14, 16, 14, 12) };
        scroll.Content = root;
        ToolsPanelContent.Children.Add(scroll);

        TextBlock title = new() { Text = "ПАРАМЕТРЫ ИНСТРУМЕНТА", Foreground = BrushFrom("#FFE3E5EA"), FontSize = 10, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,0,0,14) };
        root.Children.Add(title);
        _toolHelpText = new TextBlock { Foreground = BrushFrom("#FFB8BEC9"), FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,12), Visibility = Visibility.Collapsed };
        root.Children.Add(_toolHelpText);
        _colorControls = new StackPanel();
        root.Children.Add(_colorControls);
        TextBlock colorLabel = new() { Text = "ЦВЕТ", Foreground = BrushFrom("#FF9DA4B2"), FontSize = 10, Margin = new Thickness(0,0,0,8) };
        _colorControls.Children.Add(colorLabel);

        Grid fieldHost = new() { Width = 190, Height = 150, HorizontalAlignment = HorizontalAlignment.Left, ClipToBounds = false };
        _colorField = new Canvas { Width = 190, Height = 150, Background = Brushes.Transparent };
        Rectangle hue = new() { Width = 190, Height = 150, Fill = CreateHueBrush() };
        Rectangle white = new() { Width = 190, Height = 150, Fill = new LinearGradientBrush(Colors.White, Color.FromArgb(0,255,255,255), 0) };
        Rectangle black = new() { Width = 190, Height = 150, Fill = new LinearGradientBrush(Color.FromArgb(0,0,0,0), Colors.Black, 90) };
        _colorField.Children.Add(hue);
        _colorField.Children.Add(white);
        _colorField.Children.Add(black);
        _colorThumb = new Ellipse { Width = 12, Height = 12, Stroke = Brushes.White, StrokeThickness = 2, Fill = Brushes.Transparent, IsHitTestVisible = false };
        _colorField.Children.Add(_colorThumb);
        _colorField.MouseLeftButtonDown += ColorField_MouseDown;
        _colorField.MouseMove += ColorField_MouseMove;
        _colorField.MouseLeftButtonUp += ColorField_MouseUp;
        fieldHost.Children.Add(_colorField);
        _colorControls.Children.Add(fieldHost);

        StackPanel previewRow = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0,12,0,12), VerticalAlignment = VerticalAlignment.Center };
        _colorPreview = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(6), Background = new SolidColorBrush(_currentColor), BorderBrush = BrushFrom("#FF697080"), BorderThickness = new Thickness(1), Margin = new Thickness(0,0,10,0) };
        previewRow.Children.Add(_colorPreview);
        StackPanel previewText = new();
        previewText.Children.Add(new TextBlock { Text = "Предпросмотр", Foreground = BrushFrom("#FFE0E3E9"), FontSize = 11 });
         _colorHexText = new TextBlock { Text = "#FF0000", Foreground = BrushFrom("#FF9299A7"), FontSize = 10, Margin = new Thickness(0,3,0,0) };
        previewText.Children.Add(_colorHexText);
        previewRow.Children.Add(previewText);
        _colorControls.Children.Add(previewRow);

        AddSlider(_colorControls, "Яркость", out _brightnessSlider, 0, 1, 1);
        AddSlider(_colorControls, "Насыщенность", out _saturationSlider, 0, 1, 1);
        _brightnessSlider.ValueChanged += (_, _) => UpdateColorFromControls();
        _saturationSlider.ValueChanged += (_, _) => UpdateColorFromControls();

        Border separator = new() { Height = 1, Background = BrushFrom("#FF3A3F4A"), Margin = new Thickness(0,16,0,14) };
        root.Children.Add(separator);
        Button groupButton = CreatePanelButton("Создать группу из выделения");
        groupButton.Click += (_, _) => CreateGroupFromSelection();
        _groupButton = groupButton;
        root.Children.Add(groupButton);
        _fillButton = CreatePanelButton("Применить цвет");
        _fillButton.Margin = new Thickness(0,8,0,0);
        _fillButton.Click += (_, _) => ApplyFill();
        root.Children.Add(_fillButton);
        UpdateColorFromControls();
    }

    private void AddSlider(Panel host, string label, out Slider slider, double min, double max, double value)
    {
        host.Children.Add(new TextBlock { Text = label, Foreground = BrushFrom("#FFB8BEC9"), FontSize = 11, Margin = new Thickness(0,8,0,3) });
        slider = new Slider { Minimum = min, Maximum = max, Value = value, Width = 190, HorizontalAlignment = HorizontalAlignment.Left };
        host.Children.Add(slider);
    }

    private Button CreatePanelButton(string text) => new()
    {
        Content = text, Height = 34, HorizontalContentAlignment = HorizontalAlignment.Center,
        Foreground = BrushFrom("#FFE3E5EA"), Background = BrushFrom("#FF343945"),
        BorderBrush = BrushFrom("#FF505664"), BorderThickness = new Thickness(1),
        Cursor = Cursors.Hand, Focusable = false
    };

    private static Brush BrushFrom(string color) => (Brush)new BrushConverter().ConvertFromString(color)!;

    private static LinearGradientBrush CreateHueBrush()
    {
        LinearGradientBrush brush = new() { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
        brush.GradientStops.Add(new GradientStop(Colors.Red, 0));
        brush.GradientStops.Add(new GradientStop(Colors.Magenta, 0.17));
        brush.GradientStops.Add(new GradientStop(Colors.Blue, 0.34));
        brush.GradientStops.Add(new GradientStop(Colors.Cyan, 0.51));
        brush.GradientStops.Add(new GradientStop(Colors.Lime, 0.68));
        brush.GradientStops.Add(new GradientStop(Colors.Yellow, 0.84));
        brush.GradientStops.Add(new GradientStop(Colors.Red, 1));
        return brush;
    }

    private void SetActiveTool(EditorTool tool)
    {
        _activeTool = tool;
        foreach (Button button in ((StackPanel)ToolbarHost.Children[0]).Children.OfType<Button>())
        {
            bool active = button.Tag is EditorTool candidate && candidate == tool;
            button.Background = BrushFrom(active ? "#FF515A6B" : "#FF2A2E38");
            button.BorderBrush = BrushFrom(active ? "#FF818B9F" : "#FF3E4350");
        }
        if (_colorControls is not null)
            _colorControls.Visibility = tool == EditorTool.Brush || tool == EditorTool.Fill ? Visibility.Visible : Visibility.Collapsed;
        if (_groupButton is not null) _groupButton.Visibility = tool == EditorTool.Select ? Visibility.Visible : Visibility.Collapsed;
        if (_fillButton is not null) _fillButton.Visibility = tool == EditorTool.Fill ? Visibility.Visible : Visibility.Collapsed;
        if (_toolHelpText is not null)
        {
            _toolHelpText.Visibility = tool == EditorTool.Eraser || tool == EditorTool.Select ? Visibility.Visible : Visibility.Collapsed;
            _toolHelpText.Text = tool == EditorTool.Eraser
                ? "Проведите курсором по клавишам, чтобы убрать их подсветку."
                : "Клик — выбрать. Shift — добавить или снять. Перетащите курсор — выделить рамкой.";
        }
        if (!_toolsPanelIsOpen) SetToolsPanelOpen(true);
    }

    private void Keyboard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Point point = e.GetPosition(KeyHitboxCanvas);
        _isPointerDown = true;
        _dragStart = point;
        _shiftAtStart = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        _lastPaintedKey = null;
        Mouse.Capture(KeyHitboxCanvas, CaptureMode.Element);

        string? key = FindKeyAt(point);
        if (_activeTool == EditorTool.Brush) PaintKey(key, _currentColor);
        else if (_activeTool == EditorTool.Eraser) EraseKey(key);
        else if (_activeTool == EditorTool.Fill) ApplyFill();
        else
        {
            _isMarquee = false;
            if (!_shiftAtStart)
            {
                _selectedKeys.Clear();
                if (key is not null)
                {
                    HashSet<string>? group = _groups.FirstOrDefault(candidate => candidate.Contains(key));
                    if (group is null) _selectedKeys.Add(key);
                    else _selectedKeys.UnionWith(group);
                }
                RenderSelection();
            }
        }
        e.Handled = true;
    }

    private void Keyboard_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPointerDown) return;
        Point point = e.GetPosition(KeyHitboxCanvas);
        if (_activeTool == EditorTool.Brush)
        {
            PaintKey(FindKeyAt(point), _currentColor);
        }
        else if (_activeTool == EditorTool.Eraser)
        {
            EraseKey(FindKeyAt(point));
        }
        else if (_activeTool == EditorTool.Select)
        {
            if (!_isMarquee && (Math.Abs(point.X - _dragStart.X) > 5 || Math.Abs(point.Y - _dragStart.Y) > 5))
            {
                _isMarquee = true;
                _marquee = new Rectangle { Stroke = BrushFrom("#FF9DB7FF"), StrokeThickness = 1, Fill = new SolidColorBrush(Color.FromArgb(35,100,145,255)), IsHitTestVisible = false };
                SelectionOverlayCanvas.Children.Add(_marquee);
            }
            if (_isMarquee && _marquee is not null)
            {
                double x = Math.Min(point.X, _dragStart.X), y = Math.Min(point.Y, _dragStart.Y);
                _marquee.Width = Math.Abs(point.X - _dragStart.X);
                _marquee.Height = Math.Abs(point.Y - _dragStart.Y);
                Canvas.SetLeft(_marquee, x);
                Canvas.SetTop(_marquee, y);
            }
        }
    }

    private void Keyboard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isPointerDown) return;
        Point point = e.GetPosition(KeyHitboxCanvas);
        if (_activeTool == EditorTool.Select)
        {
            if (_isMarquee)
            {
                Rect area = new(_dragStart, point);
                if (area.Width < 1) area.Inflate(1, 0);
                if (area.Height < 1) area.Inflate(0, 1);
                if (!_shiftAtStart) _selectedKeys.Clear();
                foreach (KeyValuePair<string, List<Rect>> entry in _keyBounds)
                    if (entry.Value.Any(bounds => area.IntersectsWith(bounds)))
                    {
                        if (_shiftAtStart && !_selectedKeys.Add(entry.Key)) _selectedKeys.Remove(entry.Key);
                        else if (!_shiftAtStart) _selectedKeys.Add(entry.Key);
                    }
            }
            else
            {
                string? key = FindKeyAt(_dragStart);
                if (key is not null && _shiftAtStart)
                {
                    HashSet<string>? group = _groups.FirstOrDefault(candidate => candidate.Contains(key));
                    IEnumerable<string> targets = group ?? new HashSet<string>(new[] { key }, StringComparer.Ordinal);
                    bool remove = targets.All(_selectedKeys.Contains);
                    foreach (string target in targets)
                    {
                        if (remove) _selectedKeys.Remove(target);
                        else _selectedKeys.Add(target);
                    }
                }
            }
            RenderSelection();
        }
        _isPointerDown = false;
        _isMarquee = false;
        _lastPaintedKey = null;
        if (_marquee is not null) SelectionOverlayCanvas.Children.Remove(_marquee);
        _marquee = null;
        Mouse.Capture(null);
        e.Handled = true;
    }

    private void PaintKey(string? key, Color color)
    {
        if (key is null || key == _lastPaintedKey) return;
        _lastPaintedKey = key;
        _keyColors[key] = color;
        UpdateKeyVisual(key);
        ScheduleHardwareFrame();
    }

    private void EraseKey(string? key)
    {
        if (key is null || key == _lastPaintedKey) return;
        _lastPaintedKey = key;
        _keyColors.Remove(key);
        UpdateKeyVisual(key);
        ScheduleHardwareFrame();
    }

    private void UpdateKeyVisual(string key)
    {
        if (!_keyGlows.TryGetValue(key, out List<Border>? glows)) return;
        bool hasColor = _keyColors.TryGetValue(key, out Color color);
        foreach (Border glow in glows)
        {
            glow.Background = hasColor ? CreateRgbGlow(color) : Brushes.Transparent;
            glow.Opacity = hasColor ? 1 : 0;
        }
    }

    private static Brush CreateRgbGlow(Color color)
    {
        RadialGradientBrush brush = new()
        {
            Center = new Point(0.5,0.5), GradientOrigin = new Point(0.5,0.5),
            RadiusX = 0.9, RadiusY = 0.9
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(235,color.R,color.G,color.B),0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(165,color.R,color.G,color.B),0.55));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0,color.R,color.G,color.B),1));
        brush.Freeze();
        return brush;
    }

    private void ApplyFill()
    {
        IEnumerable<string> targets = _selectedKeys.Count > 0 ? _selectedKeys.ToArray() : _keyGlows.Keys.ToArray();
        foreach (string key in targets)
        {
            _keyColors[key] = _currentColor;
            UpdateKeyVisual(key);
        }
        ScheduleHardwareFrame();
    }

    private void ScheduleHardwareFrame()
    {
        _hardwareUpdateTimer.Stop();
        _hardwareUpdateTimer.Start();
    }

    private async Task SendHardwareFrameAsync()
    {
        Dictionary<string, Color> frame = new(_keyColors, StringComparer.Ordinal);
        try { await VeigarK643HidController.SendColorsAsync(frame); }
        catch (Exception ex) { Debug.WriteLine("Veigar K643 HID: " + ex.Message); }
    }

    private string? FindKeyAt(Point point)
    {
        foreach (KeyValuePair<string, List<Rect>> entry in _keyBounds)
            if (entry.Value.Any(rect => rect.Contains(point))) return entry.Key;
        return null;
    }

    private void RenderSelection()
    {
        SelectionOverlayCanvas.Children.Clear();
        foreach (HashSet<string> group in _groups)
            AddOutline(group, "#FFCCAA72", 1.4, 18);
        if (_selectedKeys.Count > 0)
            AddOutline(_selectedKeys, "#FF9DB7FF", 1.6, 24);
    }

    private void AddOutline(IEnumerable<string> keys, string stroke, double thickness, byte fillAlpha)
    {
        Geometry? combined = null;
        foreach (string key in keys)
        {
            if (!_keyBounds.TryGetValue(key, out List<Rect>? bounds)) continue;
            foreach (Rect rect in bounds)
            {
                Rect expanded = rect;
                expanded.Inflate(2.2, 2.2);
                Geometry next = new RectangleGeometry(expanded, 4, 4);
                combined = combined is null ? next : Geometry.Combine(combined, next, GeometryCombineMode.Union, null);
            }
        }
        if (combined is null) return;
        Color outlineColor = ((SolidColorBrush)BrushFrom(stroke)).Color;
        System.Windows.Shapes.Path outline = new()
        {
            Data = combined,
            Stroke = new SolidColorBrush(outlineColor),
            StrokeThickness = thickness,
            Fill = new SolidColorBrush(Color.FromArgb(fillAlpha, outlineColor.R, outlineColor.G, outlineColor.B)),
            IsHitTestVisible = false
        };
        SelectionOverlayCanvas.Children.Add(outline);
    }

    private void CreateGroupFromSelection()
    {
        if (_selectedKeys.Count == 0) return;
        HashSet<string> group = new(_selectedKeys, StringComparer.Ordinal);
        _groups.RemoveAll(existing => existing.SetEquals(group));
        _groups.Add(group);
        RenderSelection();
    }

    private void ColorField_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_colorField is null) return;
        _colorField.CaptureMouse();
        UpdateColorFromPoint(e.GetPosition(_colorField));
    }

    private void ColorField_MouseMove(object sender, MouseEventArgs e)
    {
        if (_colorField is not null && e.LeftButton == MouseButtonState.Pressed)
            UpdateColorFromPoint(e.GetPosition(_colorField));
    }

    private void ColorField_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _colorField?.ReleaseMouseCapture();
    }

    private void UpdateColorFromPoint(Point point)
    {
        if (_colorField is null || _colorThumb is null) return;
        _selectedHue = Math.Clamp(point.X / _colorField.Width, 0, 1) * 360;
        _selectedValue = 1 - Math.Clamp(point.Y / _colorField.Height, 0, 1);
        Canvas.SetLeft(_colorThumb, Math.Clamp(point.X - 6, 0, _colorField.Width - 12));
        Canvas.SetTop(_colorThumb, Math.Clamp(point.Y - 6, 0, _colorField.Height - 12));
        UpdateColorFromControls();
    }

    private void UpdateColorFromControls()
    {
        if (_brightnessSlider is null || _saturationSlider is null) return;
        double value = _selectedValue * _brightnessSlider.Value;
        _currentColor = HsvToColor(_selectedHue, _saturationSlider.Value, value);
        if (_colorPreview is not null) _colorPreview.Background = new SolidColorBrush(_currentColor);
        if (_colorHexText is not null) _colorHexText.Text = $"#{_currentColor.R:X2}{_currentColor.G:X2}{_currentColor.B:X2}";
    }

    private static Color HsvToColor(double hue, double saturation, double value)
    {
        double chroma = value * saturation;
        double x = chroma * (1 - Math.Abs((hue / 60 % 2) - 1));
        double m = value - chroma;
        (double r, double g, double b) = hue switch
        {
            < 60 => (chroma,x,0d), < 120 => (x,chroma,0d), < 180 => (0d,chroma,x),
            < 240 => (0d,x,chroma), < 300 => (x,0d,chroma), _ => (chroma,0d,x)
        };
        return Color.FromRgb((byte)Math.Round((r+m)*255),(byte)Math.Round((g+m)*255),(byte)Math.Round((b+m)*255));
    }

    private void ReleasePointer()
    {
        _isPointerDown = false;
        _isMarquee = false;
        Mouse.Capture(null);
        if (_marquee is not null) SelectionOverlayCanvas.Children.Remove(_marquee);
        _marquee = null;
    }

    private static FontFamily GetCaptionFontFamily()
    {
        foreach (FontFamily font in Fonts.SystemFontFamilies)
            if (string.Equals(font.Source, "Segoe Fluent Icons", StringComparison.OrdinalIgnoreCase)) return font;
        return new FontFamily("Segoe MDL2 Assets");
    }

    private void ToolsPanelToggle_Click(object sender, RoutedEventArgs e) => SetToolsPanelOpen(!_toolsPanelIsOpen);

    private void SetToolsPanelOpen(bool isOpen)
    {
        _toolsPanelIsOpen = isOpen;
        ToolsPanel.BeginAnimation(WidthProperty, new System.Windows.Media.Animation.DoubleAnimation
        {
            To = _toolsPanelIsOpen ? 240 : 0,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut }
        });
        ToolsPanelToggleGlyph.Text = _toolsPanelIsOpen ? ">" : "<";
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void DisableRoundedCorners()
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        int preference = DwmwcpDoNotRound;
        _ = DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref preference, sizeof(int));
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);
}

internal sealed class KeyboardLayoutConfig { public List<KeyboardKeyConfig> Keys { get; set; } = new(); }
internal sealed class KeyboardKeyConfig { public string Key { get; set; } = string.Empty; public List<KeyboardKeyArea> Areas { get; set; } = new(); }
internal sealed class KeyboardKeyArea { public double X { get; set; } public double Y { get; set; } public double Width { get; set; } public double Height { get; set; } }
