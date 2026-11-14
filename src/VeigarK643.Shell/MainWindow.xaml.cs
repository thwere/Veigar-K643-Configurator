using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using IOPath = System.IO.Path;
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
    private readonly List<EditorGroup> _groups = new();
    private readonly Stack<EditorSnapshot> _undoHistory = new();
    private readonly Stack<EditorSnapshot> _redoHistory = new();
    private const int HistoryLimit = 250;
    private readonly List<Button> _toolButtons = new();
    private readonly string _profilesDirectory = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VeigarConfig", "Profiles");
    private string _activeProfileName = "Основной";
    private string _activeProfileColor = "#FF6D8CFF";
    private string _dialogColor = "#FF6D8CFF";
    private IntPtr _deviceNotification = IntPtr.Zero;
    private int _reconnectAttempts;
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
    private Button? _profileButton;
    private StackPanel? _groupManagerContent;
    private TextBlock? _colorHintText;
    private Slider? _saturationSlider;
    private double _selectedHue;
    private double _selectedValue = 1;
    private readonly DispatcherTimer _profileSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private readonly DispatcherTimer _reconnectTimer = new() { Interval = TimeSpan.FromMilliseconds(900) };
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
        SourceInitialized += MainWindow_SourceInitialized;
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        Closed += (_, _) => { SaveCurrentProfile(); if (_deviceNotification != IntPtr.Zero) UnregisterDeviceNotification(_deviceNotification); };
        Deactivated += (_, _) => ReleasePointer();
        InitializeProfiles();
        UpdateProfileButton();
        _reconnectTimer.Start();
        _hardwareUpdateTimer.Tick += async (_, _) =>
        {
            _hardwareUpdateTimer.Stop();
            await SendHardwareFrameAsync();
        };
        _profileSaveTimer.Tick += (_, _) => { _profileSaveTimer.Stop(); SaveCurrentProfile(); };
        _reconnectTimer.Tick += async (_, _) =>
        {
            _reconnectTimer.Stop();
            bool sent = await SendHardwareFrameAsync();
            if (!sent && ++_reconnectAttempts < 5) _reconnectTimer.Start();
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
        _toolButtons.Clear();
        Grid layout = new();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        StackPanel tools = new()
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(10, 0, 0, 0)
        };
        AddToolButton(tools, "✎", "Кисть (B)", EditorTool.Brush);
        AddToolButton(tools, "▰", "Заливка (G)", EditorTool.Fill);
        AddToolButton(tools, "▧", "Выделение (V)", EditorTool.Select);
        AddToolButton(tools, "⌫", "Ластик (E)", EditorTool.Eraser);
        layout.Children.Add(tools);
        StackPanel right = new()
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 10, 0)
        };
        _profileButton = CreateToolbarButton("", "Профили", ShowProfilesManager);
        _profileButton.Width = 180;
        right.Children.Add(_profileButton);
        Button groups = CreateToolbarButton("▦", "Группы клавиш", ShowGroupsManager);
        groups.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = "▦", FontFamily = new FontFamily("Segoe UI Symbol"), FontSize = 15, Margin = new Thickness(0,0,7,0) },
                new TextBlock { Text = "Группы клавиш", FontSize = 11, VerticalAlignment = VerticalAlignment.Center }
            }
        };
        groups.Width = 142;
        groups.Margin = new Thickness(6,0,0,0);
        right.Children.Add(groups);
        Grid.SetColumn(right, 1);
        layout.Children.Add(right);
        ToolbarHost.Children.Add(layout);
        UpdateProfileButton();
    }

    private void AddToolButton(Panel host, string glyph, string tooltip, EditorTool tool)
    {
        Button button = new()
        {
            Content = new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe UI Symbol"), FontSize = 17, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            Tag = tool, Width = 42, Height = 34, Margin = new Thickness(2,0,2,0),
            Padding = new Thickness(0), ToolTip = tooltip, Foreground = BrushFrom("#FFDCE0E8"),
            Background = BrushFrom("#FF2A2E38"), BorderBrush = BrushFrom("#FF3E4350"),
            BorderThickness = new Thickness(1), Cursor = Cursors.Hand, Focusable = false
        };
        button.Click += (_, _) => SetActiveTool(tool);
        _toolButtons.Add(button);
        host.Children.Add(button);
    }

    private Button CreateToolbarButton(string glyph, string tooltip, Action action)
    {
        Button button = new()
        {
            Content = new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 17, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            Width = 42, Height = 34, Margin = new Thickness(2,0,2,0), ToolTip = tooltip,
            Foreground = BrushFrom("#FFDCE0E8"), Background = BrushFrom("#FF2A2E38"),
            BorderBrush = BrushFrom("#FF3E4350"), BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand, Focusable = false
        };
        button.Click += (_, _) => action();
        return button;
    }

    private void BuildToolsPanel()
    {
        ToolsPanelContent.Children.Clear();
        ScrollViewer scroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        StackPanel root = new() { Margin = new Thickness(16, 18, 16, 14) };
        scroll.Content = root;
        ToolsPanelContent.Children.Add(scroll);
        root.Children.Add(new TextBlock { Text = "ПАРАМЕТРЫ", Foreground = BrushFrom("#FFE3E5EA"), FontSize = 10, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,0,0,16) });
        root.Children.Add(new TextBlock { Text = "ЦВЕТ КИСТИ", Foreground = BrushFrom("#FF9DA4B2"), FontSize = 10, Margin = new Thickness(0,0,0,8) });

        Grid paletteGrid = new() { Width = 230, Height = 178, HorizontalAlignment = HorizontalAlignment.Left };
        paletteGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        paletteGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        _colorField = new Canvas { Width = 194, Height = 178, Background = Brushes.Transparent, ClipToBounds = true };
        Rectangle hue = new() { Width = 194, Height = 178, Fill = CreateHueBrush(1) };
        Rectangle black = new() { Width = 194, Height = 178, Fill = new LinearGradientBrush { StartPoint = new Point(0,0), EndPoint = new Point(0,1), GradientStops = new GradientStopCollection { new GradientStop(Color.FromArgb(0,0,0,0),0), new GradientStop(Colors.Black,1) } } };
        _colorField.Children.Add(hue);
        _colorField.Children.Add(black);
        _colorThumb = new Ellipse { Width = 13, Height = 13, Stroke = Brushes.White, StrokeThickness = 2, Fill = Brushes.Transparent, IsHitTestVisible = false };
        _colorField.Children.Add(_colorThumb);
        _colorField.MouseLeftButtonDown += ColorField_MouseDown;
        _colorField.MouseMove += ColorField_MouseMove;
        _colorField.MouseLeftButtonUp += ColorField_MouseUp;
        Grid.SetColumn(_colorField,0);
        paletteGrid.Children.Add(_colorField);
        _saturationSlider = new Slider
        {
            Minimum = 0, Maximum = 1, Value = 1, Orientation = Orientation.Vertical,
            Width = 22, Height = 172, VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center, IsMoveToPointEnabled = false,
            LargeChange = 0.08, SmallChange = 0.01, ToolTip = "Насыщенность"
        };
        _saturationSlider.ValueChanged += (_, _) => { UpdateHueBrush(); UpdateColorFromControls(); };
        Grid.SetColumn(_saturationSlider,1);
        paletteGrid.Children.Add(_saturationSlider);
        root.Children.Add(paletteGrid);
        _colorPreview = new Border { Height = 34, CornerRadius = new CornerRadius(5), Background = new SolidColorBrush(_currentColor), BorderBrush = BrushFrom("#FF697080"), BorderThickness = new Thickness(1), Margin = new Thickness(0,10,0,0) };
        root.Children.Add(_colorPreview);
        _colorHexText = new TextBlock { Text = "#FF0000", Foreground = BrushFrom("#FF9299A7"), FontSize = 10, Margin = new Thickness(0,5,0,0), HorizontalAlignment = HorizontalAlignment.Center };
        root.Children.Add(_colorHexText);
        _colorHintText = new TextBlock { Text = "Выберите оттенок и яркость на палитре. Насыщенность регулируется ползунком справа.", Foreground = BrushFrom("#FF9DA4B2"), FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,14,0,0) };
        root.Children.Add(_colorHintText);
        _groupManagerContent = new StackPanel { Visibility = Visibility.Collapsed };
        root.Children.Add(_groupManagerContent);
        UpdateColorFromControls();
    }

    private static LinearGradientBrush CreateHueBrush(double saturation)
    {
        LinearGradientBrush brush = new() { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
        for (int i = 0; i <= 12; i++)
            brush.GradientStops.Add(new GradientStop(HsvToColor(i * 30, saturation, 1), i / 12d));
        return brush;
    }

    private void UpdateHueBrush()
    {
        if (_colorField is null || _saturationSlider is null || _colorField.Children.Count == 0) return;
        if (_colorField.Children[0] is Rectangle hue) hue.Fill = CreateHueBrush(_saturationSlider.Value);
    }

    private Button CreatePanelButton(string text) => new()
    {
        Content = text, Height = 34, HorizontalContentAlignment = HorizontalAlignment.Center,
        Foreground = BrushFrom("#FFE3E5EA"), Background = BrushFrom("#FF343945"),
        BorderBrush = BrushFrom("#FF505664"), BorderThickness = new Thickness(1),
        Cursor = Cursors.Hand, Focusable = false
    };

    private static Brush BrushFrom(string color) => (Brush)new BrushConverter().ConvertFromString(color)!;

    private void SetActiveTool(EditorTool tool)
    {
        _activeTool = tool;
        foreach (Button button in _toolButtons)
        {
            bool active = button.Tag is EditorTool candidate && candidate == tool;
            button.Background = BrushFrom(active ? "#FF515A6B" : "#FF2A2E38");
            button.BorderBrush = BrushFrom(active ? "#FF818B9F" : "#FF3E4350");
        }
        bool colorTool = tool == EditorTool.Brush || tool == EditorTool.Fill;
        if (_colorField?.Parent is Grid palette) palette.Visibility = colorTool ? Visibility.Visible : Visibility.Collapsed;
        if (_colorPreview is not null) _colorPreview.Visibility = colorTool ? Visibility.Visible : Visibility.Collapsed;
        if (_colorHexText is not null) _colorHexText.Visibility = colorTool ? Visibility.Visible : Visibility.Collapsed;
        if (_colorHintText is not null) _colorHintText.Visibility = colorTool ? Visibility.Visible : Visibility.Collapsed;
        if (_groupManagerContent is not null) _groupManagerContent.Visibility = Visibility.Collapsed;
        ToolStatusText.Text = tool switch
        {
            EditorTool.Brush => "Кисть: проведите по клавишам, чтобы назначить выбранный цвет.",
            EditorTool.Fill => "Заливка: щёлкните по клавише, чтобы залить выделение или всю клавиатуру.",
            EditorTool.Select => "Выделение: щелчок — выбрать; Shift — добавить/снять; перетаскивание — рамка; Esc — снять выделение.",
            _ => "Ластик: проведите по клавишам, чтобы удалить их подсветку."
        };
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
            if (!_shiftAtStart || key is null)
            {
                _selectedKeys.Clear();
                if (key is not null)
                {
                    EditorGroup? group = _groups.FirstOrDefault(candidate => candidate.Keys.Contains(key));
                    if (group is null) _selectedKeys.Add(key);
                    else _selectedKeys.UnionWith(group.Keys);
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
                    EditorGroup? group = _groups.FirstOrDefault(candidate => candidate.Keys.Contains(key));
                    IEnumerable<string> targets = group?.Keys ?? new HashSet<string>(new[] { key }, StringComparer.Ordinal);
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
        PushUndoSnapshot();
        _keyColors[key] = color;
        UpdateKeyVisual(key);
        ScheduleHardwareFrame();
        ScheduleProfileSave();
    }

    private void EraseKey(string? key)
    {
        if (key is null || key == _lastPaintedKey) return;
        _lastPaintedKey = key;
        PushUndoSnapshot();
        _keyColors.Remove(key);
        UpdateKeyVisual(key);
        ScheduleHardwareFrame();
        ScheduleProfileSave();
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
        PushUndoSnapshot();
        foreach (string key in targets)
        {
            _keyColors[key] = _currentColor;
            UpdateKeyVisual(key);
        }
        ScheduleHardwareFrame();
        ScheduleProfileSave();
    }

    private void PushUndoSnapshot()
    {
        _undoHistory.Push(CaptureSnapshot());
        if (_undoHistory.Count > HistoryLimit)
        {
            EditorSnapshot[] keep = _undoHistory.Take(HistoryLimit).Reverse().ToArray();
            _undoHistory.Clear();
            foreach (EditorSnapshot snapshot in keep) _undoHistory.Push(snapshot);
        }
        _redoHistory.Clear();
    }

    private EditorSnapshot CaptureSnapshot() => new()
    {
        KeyColors = new Dictionary<string, Color>(_keyColors, StringComparer.Ordinal),
        Groups = _groups.Select(g => new EditorGroupData { Name = g.Name, ColorHex = g.ColorHex, State = g.State, Keys = g.Keys.ToList() }).ToList()
    };

    private void RestoreSnapshot(EditorSnapshot snapshot)
    {
        _keyColors.Clear();
        foreach (var pair in snapshot.KeyColors) _keyColors[pair.Key] = pair.Value;
        _groups.Clear();
        foreach (EditorGroupData g in snapshot.Groups)
            _groups.Add(new EditorGroup { Name = g.Name, ColorHex = g.ColorHex, State = g.State, Keys = new HashSet<string>(g.Keys, StringComparer.Ordinal) });
        foreach (string key in _keyGlows.Keys) UpdateKeyVisual(key);
        RenderSelection();
        ScheduleHardwareFrame();
        ScheduleProfileSave();
        if (_groupManagerContent?.Visibility == Visibility.Visible) ShowGroupsManager();
    }

    private void Undo()
    {
        if (_undoHistory.Count == 0) return;
        _redoHistory.Push(CaptureSnapshot());
        RestoreSnapshot(_undoHistory.Pop());
    }

    private void Redo()
    {
        if (_redoHistory.Count == 0) return;
        _undoHistory.Push(CaptureSnapshot());
        RestoreSnapshot(_redoHistory.Pop());
    }

    private void ScheduleHardwareFrame()
    {
        _hardwareUpdateTimer.Stop();
        _hardwareUpdateTimer.Start();
    }

    private async Task<bool> SendHardwareFrameAsync()
    {
        Dictionary<string, Color> frame = new(_keyColors, StringComparer.Ordinal);
        try { await VeigarK643HidController.SendColorsAsync(frame); return true; }
        catch (Exception ex) { Debug.WriteLine("Veigar K643 HID: " + ex.Message); return false; }
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
        foreach (EditorGroup group in _groups)
            AddOutline(group.Keys, group.ColorHex, 1.4, 30);
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
        double x = Math.Clamp(point.X, 0, _colorField.Width);
        double y = Math.Clamp(point.Y, 0, _colorField.Height);
        _selectedHue = x / _colorField.Width * 360;
        _selectedValue = 1 - y / _colorField.Height;
        Canvas.SetLeft(_colorThumb, Math.Clamp(x - 6.5, 0, _colorField.Width - 13));
        Canvas.SetTop(_colorThumb, Math.Clamp(y - 6.5, 0, _colorField.Height - 13));
        UpdateColorFromControls();
    }

    private void UpdateColorFromControls()
    {
        if (_saturationSlider is null) return;
        _currentColor = HsvToColor(_selectedHue, _saturationSlider.Value, _selectedValue);
        if (_colorPreview is not null) _colorPreview.Background = new SolidColorBrush(_currentColor);
        if (_colorHexText is not null) _colorHexText.Text = $"#{_currentColor.R:X2}{_currentColor.G:X2}{_currentColor.B:X2}";
    }

    private static Color HsvToColor(double hue, double saturation, double value)
    {
        hue = ((hue % 360) + 360) % 360;
        saturation = Math.Clamp(saturation, 0, 1);
        value = Math.Clamp(value, 0, 1);
        double chroma = value * saturation;
        double x = chroma * (1 - Math.Abs((hue / 60 % 2) - 1));
        double m = value - chroma;
        (double r, double g, double b) = hue switch
        {
            < 60 => (chroma, x, 0d),
            < 120 => (x, chroma, 0d),
            < 180 => (0d, chroma, x),
            < 240 => (0d, x, chroma),
            < 300 => (x, 0d, chroma),
            _ => (chroma, 0d, x)
        };
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (ModalOverlay.Visibility == Visibility.Visible) CloseModal();
            else { _selectedKeys.Clear(); RenderSelection(); }
            e.Handled = true;
            return;
        }
        if (ModalOverlay.Visibility == Visibility.Visible) return;
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z) { Undo(); e.Handled = true; return; }
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y) { Redo(); e.Handled = true; return; }
        if (Keyboard.Modifiers == ModifierKeys.None)
        {
            if (e.Key == Key.B) SetActiveTool(EditorTool.Brush);
            else if (e.Key == Key.G) SetActiveTool(EditorTool.Fill);
            else if (e.Key == Key.V) SetActiveTool(EditorTool.Select);
            else if (e.Key == Key.E) SetActiveTool(EditorTool.Eraser);
        }
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        DisableRoundedCorners();
        HwndSource? source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        source?.AddHook(WindowMessageHook);
        RegisterHidNotifications();
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0219 && unchecked((int)wParam.ToInt64()) == 0x8000)
        {
            _reconnectAttempts = 0;
            _reconnectTimer.Stop();
            _reconnectTimer.Start();
        }
        return IntPtr.Zero;
    }

    private void RegisterHidNotifications()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        DeviceBroadcastInterface filter = new()
        {
            Size = Marshal.SizeOf<DeviceBroadcastInterface>(),
            DeviceType = 5,
            ClassGuid = new Guid("4D1E55B2-F16F-11CF-88CB-001111000030")
        };
        IntPtr memory = Marshal.AllocHGlobal(filter.Size);
        try
        {
            Marshal.StructureToPtr(filter, memory, false);
            _deviceNotification = RegisterDeviceNotification(hwnd, memory, 0);
        }
        finally { Marshal.FreeHGlobal(memory); }
    }

    private void ScheduleProfileSave()
    {
        _profileSaveTimer.Stop();
        _profileSaveTimer.Start();
    }

    private void InitializeProfiles()
    {
        Directory.CreateDirectory(_profilesDirectory);
        string[] files = Directory.GetFiles(_profilesDirectory, "*.json");
        if (files.Length == 0)
        {
            SaveCurrentProfile();
            return;
        }
        string preferred = ProfilePath(_activeProfileName);
        LoadProfile(File.Exists(preferred) ? preferred : files[0]);
    }

    private static string SafeFileName(string name)
    {
        foreach (char c in IOPath.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "Profile" : name.Trim();
    }

    private string ProfilePath(string name) => IOPath.Combine(_profilesDirectory, SafeFileName(name) + ".json");

    private void SaveCurrentProfile()
    {
        try
        {
            Directory.CreateDirectory(_profilesDirectory);
            EditorProfileData data = new()
            {
                Name = _activeProfileName,
                ColorHex = _activeProfileColor,
                KeyColors = _keyColors.ToDictionary(pair => pair.Key, pair => $"#{pair.Value.R:X2}{pair.Value.G:X2}{pair.Value.B:X2}", StringComparer.Ordinal),
                Groups = _groups.Select(group => new EditorGroupData { Name = group.Name, ColorHex = group.ColorHex, State = group.State, Keys = group.Keys.ToList() }).ToList()
            };
            File.WriteAllText(ProfilePath(_activeProfileName), JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) { Debug.WriteLine("Profile save failed: " + ex.Message); }
    }

    private void LoadProfile(string path)
    {
        try
        {
            EditorProfileData? data = JsonSerializer.Deserialize<EditorProfileData>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (data is null) return;
            _keyColors.Clear();
            foreach (var pair in data.KeyColors)
            {
                object? parsed = ColorConverter.ConvertFromString(pair.Value);
                if (parsed is Color color) _keyColors[pair.Key] = color;
            }
            foreach (string key in _keyGlows.Keys) UpdateKeyVisual(key);
            _groups.Clear();
            foreach (EditorGroupData group in data.Groups)
                _groups.Add(new EditorGroup { Name = group.Name, ColorHex = group.ColorHex, State = group.State, Keys = new HashSet<string>(group.Keys, StringComparer.Ordinal) });
            _activeProfileName = data.Name;
            _activeProfileColor = data.ColorHex;
            RenderSelection();
            ScheduleHardwareFrame();
            UpdateProfileButton();
        }
        catch (Exception ex) { Debug.WriteLine("Profile load failed: " + ex.Message); }
    }

    private void ShowGroupsManager()
    {
        if (_groupManagerContent is null) return;
        if (_colorField?.Parent is Grid palette) palette.Visibility = Visibility.Collapsed;
        if (_colorPreview is not null) _colorPreview.Visibility = Visibility.Collapsed;
        if (_colorHexText is not null) _colorHexText.Visibility = Visibility.Collapsed;
        if (_colorHintText is not null) _colorHintText.Visibility = Visibility.Collapsed;
        _groupManagerContent.Visibility = Visibility.Visible;
        _groupManagerContent.Children.Clear();
        _groupManagerContent.Children.Add(new TextBlock { Text = "ГРУППЫ КЛАВИШ", Foreground = BrushFrom("#FF9DA4B2"), FontSize = 10, Margin = new Thickness(0,12,0,10) });
        if (_groups.Count == 0)
            _groupManagerContent.Children.Add(new TextBlock { Text = "Групп пока нет. Выделите клавиши и нажмите «Новая группа».", Foreground = BrushFrom("#FF9DA4B2"), FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,10) });
        foreach (EditorGroup group in _groups.ToArray())
        {
            Grid row = new() { Margin = new Thickness(0,4,0,4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new Ellipse { Width = 11, Height = 11, Fill = BrushFrom(group.ColorHex), VerticalAlignment = VerticalAlignment.Center });
            Button select = CreatePanelButton($"{group.Name} · {group.Keys.Count}");
            select.Height = 32; select.HorizontalContentAlignment = HorizontalAlignment.Left;
            select.Click += (_, _) => { _selectedKeys.Clear(); _selectedKeys.UnionWith(group.Keys); RenderSelection(); SetActiveTool(EditorTool.Select); };
            Grid.SetColumn(select,1); row.Children.Add(select);
            StackPanel actions = new() { Orientation = Orientation.Horizontal };
            Button edit = CreatePanelButton("✎"); edit.Width = 30; edit.Margin = new Thickness(4,0,0,0); edit.ToolTip = "Настроить группу";
            edit.Click += (_, _) => ShowGroupEditor(group);
            Button delete = CreatePanelButton("×"); delete.Width = 28; delete.Margin = new Thickness(3,0,0,0); delete.ToolTip = "Удалить группу";
            delete.Click += (_, _) =>
            {
                ShowModal("УДАЛИТЬ ГРУППУ?", new TextBlock { Text = $"Удалить группу «{group.Name}»? Клавиши останутся с текущими цветами.", Foreground = BrushFrom("#FFE3E5EA"), TextWrapping = TextWrapping.Wrap },
                    ("Удалить", () => { PushUndoSnapshot(); _groups.Remove(group); RenderSelection(); ScheduleProfileSave(); CloseModal(); ShowGroupsManager(); }),
                    ("Отмена", CloseModal));
            };
            actions.Children.Add(edit); actions.Children.Add(delete);
            Grid.SetColumn(actions,2); row.Children.Add(actions);
            _groupManagerContent.Children.Add(row);
        }
        Button add = CreatePanelButton("＋  Новая группа");
        add.Margin = new Thickness(0,12,0,0);
        add.Click += (_, _) => ShowGroupEditor(null);
        _groupManagerContent.Children.Add(add);
        if (!_toolsPanelIsOpen) SetToolsPanelOpen(true);
    }

    private void ShowGroupEditor(EditorGroup? existing)
    {
        if (existing is null && _selectedKeys.Count == 0)
        {
            ToolStatusText.Text = "Сначала выделите клавиши, которые нужно объединить в группу.";
            return;
        }
        TextBox name = new() { Text = existing?.Name ?? "Новая группа", Height = 32, Padding = new Thickness(8,5,8,5), Background = BrushFrom("#FF191B22"), Foreground = BrushFrom("#FFE3E5EA"), BorderBrush = BrushFrom("#FF454A55") };
        ComboBox state = new() { Height = 32, Margin = new Thickness(0,8,0,10), ItemsSource = new[] { "Статичный", "Анимация", "По сценарию" }, SelectedItem = existing?.State ?? "Статичный" };
        _dialogColor = existing?.ColorHex ?? "#FF6D8CFF";
        StackPanel colors = BuildColorSwatches(color => _dialogColor = color, _dialogColor);
        StackPanel body = new();
        body.Children.Add(new TextBlock { Text = "Название", Foreground = BrushFrom("#FFB8BEC9"), Margin = new Thickness(0,0,0,5) });
        body.Children.Add(name);
        body.Children.Add(new TextBlock { Text = "Цвет группы", Foreground = BrushFrom("#FFB8BEC9"), Margin = new Thickness(0,12,0,5) });
        body.Children.Add(colors);
        body.Children.Add(new TextBlock { Text = "Состояние", Foreground = BrushFrom("#FFB8BEC9"), Margin = new Thickness(0,8,0,5) });
        body.Children.Add(state);
        body.Children.Add(new TextBlock { Text = "Состав группы — отметьте клавиши, которые должны входить в неё:", Foreground = BrushFrom("#FFB8BEC9"), Margin = new Thickness(0,10,0,5) });
        ListBox membership = new() { Height = 150, Background = BrushFrom("#FF191B22"), Foreground = BrushFrom("#FFE3E5EA"), BorderBrush = BrushFrom("#FF454A55"), SelectionMode = SelectionMode.Extended };
        foreach (string key in _keyGlows.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            membership.Items.Add(key);
            if (existing?.Keys.Contains(key) == true || (existing is null && _selectedKeys.Contains(key))) membership.SelectedItems.Add(key);
        }
        body.Children.Add(membership);
        ShowModal(existing is null ? "СОЗДАТЬ ГРУППУ" : "НАСТРОЙКА ГРУППЫ", body,
            (existing is null ? "Создать" : "Сохранить", () =>
            {
                string groupName = name.Text.Trim();
                if (groupName.Length == 0) { ToolStatusText.Text = "Введите название группы."; return; }
                HashSet<string> memberKeys = membership.SelectedItems.Cast<string>().ToHashSet(StringComparer.Ordinal);
                if (memberKeys.Count == 0) { ToolStatusText.Text = "В группе должна быть хотя бы одна клавиша."; return; }
                PushUndoSnapshot();
                if (existing is null)
                    _groups.Add(new EditorGroup { Name = groupName, ColorHex = _dialogColor, State = state.SelectedItem?.ToString() ?? "Статичный", Keys = memberKeys });
                else { existing.Name = groupName; existing.ColorHex = _dialogColor; existing.State = state.SelectedItem?.ToString() ?? "Статичный"; existing.Keys = memberKeys; }
                RenderSelection(); ScheduleProfileSave(); CloseModal(); ShowGroupsManager();
            }),
            ("Отмена", () => { CloseModal(); ShowGroupsManager(); }));
    }

    private StackPanel BuildColorSwatches(Action<string> choose, string selected)
    {
        string[] palette = { "#FFE85D5D", "#FFFFA94D", "#FFFFD166", "#FF65C18C", "#FF4DB6AC", "#FF5B9BFF", "#FF9B7BFF", "#FFE879B9", "#FFB8BEC9" };
        StackPanel row = new() { Orientation = Orientation.Horizontal };
        foreach (string color in palette)
        {
            Button swatch = new() { Width = 25, Height = 25, Margin = new Thickness(0,0,7,0), Padding = new Thickness(0), Background = BrushFrom(color), BorderBrush = BrushFrom(color == selected ? "#FFFFFFFF" : "#FF4A4F5B"), BorderThickness = new Thickness(color == selected ? 2 : 1), Cursor = Cursors.Hand, ToolTip = color };
            swatch.Click += (_, _) => { choose(color); foreach (Button item in row.Children.OfType<Button>()) item.BorderThickness = new Thickness(1); swatch.BorderThickness = new Thickness(2); };
            row.Children.Add(swatch);
        }
        return row;
    }

    private void ShowProfilesManager()
    {
        SaveCurrentProfile();
        StackPanel body = new() { Margin = new Thickness(0,8,0,0) };
        body.Children.Add(new TextBlock { Text = $"Активный профиль: {_activeProfileName}", Foreground = BrushFrom("#FFB8BEC9"), Margin = new Thickness(0,0,0,10) });
        string[] files = Directory.Exists(_profilesDirectory) ? Directory.GetFiles(_profilesDirectory, "*.json") : Array.Empty<string>();
        foreach (string file in files)
        {
            try
            {
                EditorProfileData? data = JsonSerializer.Deserialize<EditorProfileData>(File.ReadAllText(file));
                if (data is null) continue;
                Grid row = new() { Margin = new Thickness(0,3,0,3) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.Children.Add(new Ellipse { Width = 12, Height = 12, Fill = BrushFrom(data.ColorHex), VerticalAlignment = VerticalAlignment.Center });
                TextBlock label = new() { Text = data.Name, Foreground = BrushFrom("#FFE3E5EA"), VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(label,1); row.Children.Add(label);
                StackPanel actions = new() { Orientation = Orientation.Horizontal };
                Button load = CreatePanelButton("Открыть"); load.Width = 72;
                load.Click += (_, _) => { SaveCurrentProfile(); LoadProfile(file); CloseModal(); };
                Button edit = CreatePanelButton("Изменить"); edit.Width = 72; edit.Margin = new Thickness(5,0,0,0);
                edit.Click += (_, _) => ShowProfileEditor(data, file);
                Button delete = CreatePanelButton("×"); delete.Width = 32; delete.Margin = new Thickness(5,0,0,0);
                delete.Click += (_, _) =>
                {
                    int profileCount = Directory.GetFiles(_profilesDirectory, "*.json").Length;
                    bool isActive = string.Equals(IOPath.GetFullPath(file), IOPath.GetFullPath(ProfilePath(_activeProfileName)), StringComparison.OrdinalIgnoreCase);
                    if (profileCount <= 1 || isActive) { ToolStatusText.Text = profileCount <= 1 ? "Последний профиль удалить нельзя." : "Сначала переключитесь на другой профиль."; return; }
                    ShowModal("ПОДТВЕРДИТЕ УДАЛЕНИЕ", new TextBlock { Text = $"Удалить профиль «{data.Name}»? Это действие нельзя отменить.", Foreground = BrushFrom("#FFE3E5EA"), TextWrapping = TextWrapping.Wrap },
                        ("Удалить", () => { File.Delete(file); CloseModal(); ShowProfilesManager(); }), ("Отмена", CloseModal));
                };
                actions.Children.Add(load); actions.Children.Add(edit); actions.Children.Add(delete);
                Grid.SetColumn(actions,2); row.Children.Add(actions);
                body.Children.Add(row);
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }
        StackPanel transfer = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0,10,0,0) };
        Button export = CreatePanelButton("Экспорт"); export.Margin = new Thickness(0,0,6,0);
        export.Click += (_, _) =>
        {
            Microsoft.Win32.SaveFileDialog dialog = new() { Filter = "JSON profile (*.json)|*.json", FileName = SafeFileName(_activeProfileName) + ".json" };
            if (dialog.ShowDialog(this) == true) { SaveCurrentProfile(); File.Copy(ProfilePath(_activeProfileName), dialog.FileName, true); }
        };
        Button import = CreatePanelButton("Импорт");
        import.Click += (_, _) =>
        {
            Microsoft.Win32.OpenFileDialog dialog = new() { Filter = "JSON profile (*.json)|*.json" };
            if (dialog.ShowDialog(this) == true)
            {
                try
                {
                    EditorProfileData? imported = JsonSerializer.Deserialize<EditorProfileData>(File.ReadAllText(dialog.FileName));
                    if (imported is null || string.IsNullOrWhiteSpace(imported.Name)) throw new InvalidDataException("Invalid profile.");
                    string target = ProfilePath(imported.Name);
                    if (File.Exists(target)) { ToolStatusText.Text = "Профиль с таким названием уже существует."; return; }
                    File.Copy(dialog.FileName, target);
                    ShowProfilesManager();
                }
                catch (Exception ex) { ToolStatusText.Text = "Не удалось импортировать профиль: " + ex.Message; }
            }
        };
        transfer.Children.Add(export); transfer.Children.Add(import); body.Children.Add(transfer);
        Button create = CreatePanelButton("＋  Создать профиль");
        create.Margin = new Thickness(0,12,0,0);
        create.Click += (_, _) => ShowProfileEditor(null, null);
        body.Children.Add(create);
        ShowModal("ЛОКАЛЬНЫЕ ПРОФИЛИ", new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, MaxHeight = 340 }, ("Закрыть", CloseModal));
    }

    private void ShowProfileEditor(EditorProfileData? existing, string? oldPath)
    {
        TextBox name = new() { Text = existing?.Name ?? "Новый профиль", Height = 32, Padding = new Thickness(8,5,8,5), Background = BrushFrom("#FF191B22"), Foreground = BrushFrom("#FFE3E5EA"), BorderBrush = BrushFrom("#FF454A55") };
        _dialogColor = existing?.ColorHex ?? "#FF6D8CFF";
        StackPanel colors = BuildColorSwatches(color => _dialogColor = color, _dialogColor);
        StackPanel body = new();
        body.Children.Add(new TextBlock { Text = "Название профиля", Foreground = BrushFrom("#FFB8BEC9"), Margin = new Thickness(0,0,0,5) });
        body.Children.Add(name);
        body.Children.Add(new TextBlock { Text = "Цвет профиля", Foreground = BrushFrom("#FFB8BEC9"), Margin = new Thickness(0,12,0,5) });
        body.Children.Add(colors);
        ShowModal(existing is null ? "СОЗДАТЬ ПРОФИЛЬ" : "НАСТРОЙКА ПРОФИЛЯ", body,
            (existing is null ? "Создать" : "Сохранить", () =>
            {
                string profileName = name.Text.Trim();
                if (profileName.Length == 0) { ToolStatusText.Text = "Введите название профиля."; return; }
                string newPath = ProfilePath(profileName);
                if (existing is null && File.Exists(newPath))
                { ToolStatusText.Text = "Профиль с таким названием уже существует."; return; }
                if (existing is not null && oldPath is not null && !string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase) && File.Exists(newPath))
                { ToolStatusText.Text = "Профиль с таким названием уже существует."; return; }
                if (existing is null)
                {
                    SaveCurrentProfile();
                    _activeProfileName = profileName;
                    _activeProfileColor = _dialogColor;
                    SaveCurrentProfile();
                }
                else
                {
                    bool editingActive = oldPath is not null && string.Equals(IOPath.GetFullPath(oldPath), IOPath.GetFullPath(ProfilePath(_activeProfileName)), StringComparison.OrdinalIgnoreCase);
                    existing.Name = profileName;
                    existing.ColorHex = _dialogColor;
                    if (oldPath is not null && !string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase) && File.Exists(oldPath)) File.Delete(oldPath);
                    if (editingActive)
                    {
                        _activeProfileName = profileName;
                        _activeProfileColor = _dialogColor;
                        SaveCurrentProfile();
                    }
                    else
                    {
                        File.WriteAllText(newPath, JsonSerializer.Serialize(existing, new JsonSerializerOptions { WriteIndented = true }));
                    }
                }
                UpdateProfileButton(); CloseModal();
            }),
            ("Отмена", ShowProfilesManager));
    }

    private void UpdateProfileButton()
    {
        if (_profileButton is not null)
        {
            StackPanel content = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8,0,8,0) };
            content.Children.Add(new Ellipse { Width = 10, Height = 10, Fill = BrushFrom(_activeProfileColor), Margin = new Thickness(0,0,8,0), VerticalAlignment = VerticalAlignment.Center });
            content.Children.Add(new TextBlock { Text = _activeProfileName, Foreground = BrushFrom("#FFE3E5EA"), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 120 });
            content.Children.Add(new TextBlock { Text = "  ⋯", Foreground = BrushFrom("#FFB8BEC9"), VerticalAlignment = VerticalAlignment.Center });
            _profileButton.Content = content;
            _profileButton.ToolTip = "Профили и действия";
            _profileButton.Width = 180;
        }
    }

    private void ShowModal(string title, UIElement body, params (string Text, Action Action)[] actions)
    {
        ModalOverlay.Children.Clear();
        Grid cardGrid = new();
        cardGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });
        cardGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        cardGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(54) });
        Border card = new() { Width = 540, MaxHeight = 500, Background = BrushFrom("#FF252832"), BorderBrush = BrushFrom("#FF4A505D"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(20), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Child = cardGrid };
        cardGrid.Children.Add(new TextBlock { Text = title, Foreground = BrushFrom("#FFE5E8EF"), FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        Border bodyBorder = new() { Child = body, Margin = new Thickness(0,8,0,8) };
        Grid.SetRow(bodyBorder,1); cardGrid.Children.Add(bodyBorder);
        StackPanel footer = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        foreach (var action in actions)
        {
            Button button = CreatePanelButton(action.Text);
            button.MinWidth = 90; button.Margin = new Thickness(6,0,0,0);
            button.Click += (_, _) => action.Action();
            footer.Children.Add(button);
        }
        Grid.SetRow(footer,2); cardGrid.Children.Add(footer);
        ModalOverlay.Children.Add(card);
        ModalOverlay.Visibility = Visibility.Visible;
    }

    private void CloseModal()
    {
        ModalOverlay.Visibility = Visibility.Collapsed;
        ModalOverlay.Children.Clear();
        Focus();
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
            To = _toolsPanelIsOpen ? 280 : 0,
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

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceBroadcastInterface
    {
        public int Size;
        public int DeviceType;
        public int Reserved;
        public Guid ClassGuid;
        public char Name;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr RegisterDeviceNotification(IntPtr recipient, IntPtr notificationFilter, int flags);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterDeviceNotification(IntPtr handle);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);
}

internal sealed class KeyboardLayoutConfig { public List<KeyboardKeyConfig> Keys { get; set; } = new(); }
internal sealed class KeyboardKeyConfig { public string Key { get; set; } = string.Empty; public List<KeyboardKeyArea> Areas { get; set; } = new(); }
internal sealed class KeyboardKeyArea { public double X { get; set; } public double Y { get; set; } public double Width { get; set; } public double Height { get; set; } }

internal sealed class EditorSnapshot
{
    public Dictionary<string, Color> KeyColors { get; set; } = new(StringComparer.Ordinal);
    public List<EditorGroupData> Groups { get; set; } = new();
}

internal sealed class EditorGroup
{
    public string Name { get; set; } = "Группа";
    public string ColorHex { get; set; } = "#FF6D8CFF";
    public string State { get; set; } = "Статичный";
    public HashSet<string> Keys { get; set; } = new(StringComparer.Ordinal);
}
internal sealed class EditorGroupData
{
    public string Name { get; set; } = "Группа";
    public string ColorHex { get; set; } = "#FF6D8CFF";
    public string State { get; set; } = "Статичный";
    public List<string> Keys { get; set; } = new();
}
internal sealed class EditorProfileData
{
    public string Name { get; set; } = "Основной";
    public string ColorHex { get; set; } = "#FF6D8CFF";
    public Dictionary<string, string> KeyColors { get; set; } = new(StringComparer.Ordinal);
    public List<EditorGroupData> Groups { get; set; } = new();
}
