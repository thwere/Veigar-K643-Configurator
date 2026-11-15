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
using System.Windows.Controls.Primitives;
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
    private readonly string _uiSettingsPath = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VeigarConfig", "ui-settings.json");
    private UiSettingsData _uiSettings = new();
    private string _activeProfileName = "Основной";
    private string _activeProfileColor = "#FF6D8CFF";
    private string _dialogColor = "#FF6D8CFF";
    private IntPtr _deviceNotification = IntPtr.Zero;
    private int _reconnectAttempts;
    private string? _lastHardwareFrameSignature;
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
    private Popup? _profilesPopup;
    private bool _strokeUndoPushed;
    private bool _isGroupRemapping;
    private EditorGroup? _remappingGroup;
    private bool _isEyedropper;
    private readonly List<Color> _recentColors = new();
    private StackPanel? _recentColorsPanel;
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
        LoadUiSettings();
        BuildToolbar();
        BuildToolsPanel();
        SetActiveTool(EditorTool.Brush);
        ShowGroupsManager();
        ApplyUiSettings();
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
            bool sent = await SendHardwareFrameAsync(force: true);
            if (!sent && ++_reconnectAttempts < 10) _reconnectTimer.Start();
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
        Button eyedropper = CreateToolbarButton("⌕", "Пипетка: взять цвет с клавиши", () => { _isEyedropper = true; ToolStatusText.Text = "Пипетка: щёлкните по клавише с назначенным цветом."; });
        eyedropper.Width = 38; eyedropper.Height = 32; eyedropper.Template = RoundedToolbarButtonTemplate(6); tools.Children.Add(eyedropper);
        layout.Children.Add(tools);
        StackPanel right = new()
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 10, 0)
        };
        _profileButton = CreateToolbarButton("", "Профили", ToggleProfilesFlyout);
        _profileButton.Width = 196;
        _profileButton.Height = 34;
        _profileButton.Background = BrushFrom("#FF2A2E38");
        _profileButton.BorderBrush = BrushFrom("#FF515866");
        _profileButton.Template = RoundedToolbarButtonTemplate(17);
        right.Children.Add(_profileButton);
        Button settings = CreateToolbarButton("", "", ShowUiSettings);
        settings.Width = 36;
        settings.Margin = new Thickness(6,0,0,0);
        right.Children.Add(settings);
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
            Padding = new Thickness(0), ToolTip = null, Foreground = BrushFrom("#FFDCE0E8"),
            Background = BrushFrom("#FF2A2E38"), BorderBrush = BrushFrom("#FF3E4350"),
            BorderThickness = new Thickness(0), Template = RoundedToolbarButtonTemplate(6), Cursor = Cursors.Hand, Focusable = false
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
            Width = 42, Height = 34, Margin = new Thickness(2,0,2,0), ToolTip = null,
            Foreground = BrushFrom("#FFDCE0E8"), Background = BrushFrom("#FF2A2E38"),
            BorderThickness = new Thickness(0), Template = RoundedToolbarButtonTemplate(8),
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
        root.Children.Add(new TextBlock{Text="ПОСЛЕДНИЕ ЦВЕТА",Foreground=BrushFrom("#FF9DA4B2"),FontSize=9,Margin=new Thickness(0,12,0,5)});
        _recentColorsPanel=new StackPanel{Orientation=Orientation.Horizontal};root.Children.Add(_recentColorsPanel);
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
        BorderThickness = new Thickness(0), Template = RoundedToolbarButtonTemplate(9),
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
        if (_groupManagerContent is not null) _groupManagerContent.Visibility = Visibility.Visible;
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
        _strokeUndoPushed = false;
        _dragStart = point;
        _shiftAtStart = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        _lastPaintedKey = null;
        Mouse.Capture(KeyHitboxCanvas, CaptureMode.Element);

        string? key = FindKeyAt(point);
        if (_isGroupRemapping && _remappingGroup is not null)
        {
            if (key is not null) { if (!_selectedKeys.Add(key)) _selectedKeys.Remove(key); RenderSelection(); ToolStatusText.Text = $"Переназначение «{_remappingGroup.Name}»: {_selectedKeys.Count} клавиш."; }
        }
        else if (_isEyedropper)
        {
            if (key is not null && _keyColors.TryGetValue(key, out Color sampled)) { SetCurrentColor(sampled); _isEyedropper=false; ToolStatusText.Text=$"Цвет взят с клавиши {key}."; }
            else ToolStatusText.Text="На этой клавише нет назначенного цвета.";
        }
        else if (_activeTool == EditorTool.Brush) PaintKey(key, _currentColor);
        else if (_activeTool == EditorTool.Eraser) EraseKey(key);
        else if (_activeTool == EditorTool.Fill) ApplyFill(FindKeyAt(point));
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
        if (_isGroupRemapping || _isEyedropper) return;
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
        if (_isGroupRemapping) { _isPointerDown=false; _lastPaintedKey=null; _strokeUndoPushed=false; Mouse.Capture(null); e.Handled=true; return; }
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
        _strokeUndoPushed = false;
        if (_marquee is not null) SelectionOverlayCanvas.Children.Remove(_marquee);
        _marquee = null;
        Mouse.Capture(null);
        e.Handled = true;
    }

    private void PaintKey(string? key, Color color)
    {
        if (key is null || key == _lastPaintedKey) return;
        _lastPaintedKey = key;
        if (!_keyColors.TryGetValue(key, out Color oldColor) || oldColor != color)
        {
            PushStrokeUndoSnapshot();
            AddRecentColor(color);
            _keyColors[key] = color;
        }
        else return;
        UpdateKeyVisual(key);
        ScheduleHardwareFrame();
        ScheduleProfileSave();
    }

    private void EraseKey(string? key)
    {
        if (key is null || key == _lastPaintedKey) return;
        _lastPaintedKey = key;
        if (!_keyColors.ContainsKey(key)) return;
        PushStrokeUndoSnapshot();
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

    private void ApplyFill(string? clickedKey)
    {
        HashSet<string> groupedKeys = _groups.SelectMany(group => group.Keys).ToHashSet(StringComparer.Ordinal);
        EditorGroup? clickedGroup = clickedKey is null ? null : _groups.FirstOrDefault(group => group.Keys.Contains(clickedKey));
        IEnumerable<string> targets;
        if (clickedGroup is not null)
            targets = clickedGroup.Keys;
        else if (_selectedKeys.Count > 0 && clickedKey is not null && _selectedKeys.Contains(clickedKey))
            targets = _selectedKeys;
        else if (groupedKeys.Count > 0 || _selectedKeys.Count > 0)
        {
            HashSet<string> excluded = new(groupedKeys, StringComparer.Ordinal);
            excluded.UnionWith(_selectedKeys);
            targets = _keyGlows.Keys.Where(key => !excluded.Contains(key));
        }
        else
            targets = _keyGlows.Keys;

        string[] targetArray = targets.Distinct(StringComparer.Ordinal).ToArray();
        if (targetArray.Length == 0 || targetArray.All(key => _keyColors.TryGetValue(key, out Color existing) && existing == _currentColor)) return;
        PushUndoSnapshot();
        AddRecentColor(_currentColor);
        foreach (string key in targetArray)
        {
            _keyColors[key] = _currentColor;
            UpdateKeyVisual(key);
        }
        ScheduleHardwareFrame();
        ScheduleProfileSave();
    }

    private void PushStrokeUndoSnapshot()
    {
        if (_strokeUndoPushed) return;
        PushUndoSnapshot();
        _strokeUndoPushed = true;
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

    private async Task<bool> SendHardwareFrameAsync(bool force = false)
    {
        Dictionary<string, Color> frame = new(_keyColors, StringComparer.Ordinal);
        string signature = string.Join("|", frame.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}:{pair.Value.R:X2}{pair.Value.G:X2}{pair.Value.B:X2}"));
        if (!force && string.Equals(signature, _lastHardwareFrameSignature, StringComparison.Ordinal)) return true;
        try
        {
            await VeigarK643HidController.SendColorsAsync(frame);
            _lastHardwareFrameSignature = signature;
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Veigar K643 HID: " + ex.Message);
            ToolStatusText.Text = "Не удалось применить подсветку: " + ex.Message;
            return false;
        }
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
        SetCurrentColor(HsvToColor(_selectedHue, _saturationSlider.Value, _selectedValue));
        if (_colorPreview is not null) _colorPreview.Background = new SolidColorBrush(_currentColor);
        if (_colorHexText is not null) _colorHexText.Text = $"#{_currentColor.R:X2}{_currentColor.G:X2}{_currentColor.B:X2}";
    }

    private void SetCurrentColor(Color color)
    {
        _currentColor=color; _selectedHue=ColorToHue(color); _selectedValue=Math.Max(color.R,Math.Max(color.G,color.B))/255d;
        if(_saturationSlider is not null)_saturationSlider.Value=ColorToSaturation(color);
        if(_colorPreview is not null)_colorPreview.Background=new SolidColorBrush(color);
        if(_colorHexText is not null)_colorHexText.Text=$"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
    private static double ColorToHue(Color c)
    {
        double r=c.R/255d,g=c.G/255d,b=c.B/255d,max=Math.Max(r,Math.Max(g,b)),min=Math.Min(r,Math.Min(g,b)),d=max-min;
        if(d==0)return 0; if(max==r)return 60*(((g-b)/d)%6); if(max==g)return 60*((b-r)/d+2); return 60*((r-g)/d+4);
    }
    private static double ColorToSaturation(Color c){double max=Math.Max(c.R,Math.Max(c.G,c.B))/255d,min=Math.Min(c.R,Math.Min(c.G,c.B))/255d;return max==0?0:(max-min)/max;}
    private void AddRecentColor(Color c)
    {
        _recentColors.Remove(c);_recentColors.Insert(0,c);if(_recentColors.Count>8)_recentColors.RemoveAt(8);
        if(_recentColorsPanel is null)return;_recentColorsPanel.Children.Clear();
        foreach(Color recent in _recentColors){Button b=new(){Width=23,Height=23,Margin=new Thickness(0,0,5,0),Padding=new Thickness(0),Background=new SolidColorBrush(recent),BorderThickness=new Thickness(0),Template=RoundedToolbarButtonTemplate(6),ToolTip=$"#{recent.R:X2}{recent.G:X2}{recent.B:X2}"};b.Click+=(_,_)=>SetCurrentColor(recent);_recentColorsPanel.Children.Add(b);}
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
            _profileSaveTimer.Stop();
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
            _selectedKeys.Clear();
            _activeProfileName = data.Name;
            _activeProfileColor = data.ColorHex;
            RenderSelection();
            ScheduleHardwareFrame();
            UpdateProfileButton();
            if (_groupManagerContent is not null) ShowGroupsManager();
        }
        catch (Exception ex) { Debug.WriteLine("Profile load failed: " + ex.Message); }
    }

    private void ShowGroupsManager()
    {
        if (_groupManagerContent is null) return;
        _groupManagerContent.Visibility = Visibility.Visible;
        _groupManagerContent.Children.Clear();
        Border frame = new() { Background=Brushes.Transparent, BorderThickness=new Thickness(0), Padding=new Thickness(0), Margin=new Thickness(0,14,0,0) };
        StackPanel content = new();
        content.Children.Add(new TextBlock { Text="ГРУППЫ КЛАВИШ", Foreground=BrushFrom("#FF9DA4B2"), FontSize=10, Margin=new Thickness(0,0,0,8) });
        if (_groups.Count == 0)
            content.Children.Add(new TextBlock { Text="Групп пока нет. Выделите клавиши и нажмите «Новая группа».", Foreground=BrushFrom("#FF89919F"), FontSize=9, TextWrapping=TextWrapping.Wrap });
        foreach (EditorGroup group in _groups.ToArray())
        {
            Grid row = new() { Height=38, Margin=new Thickness(0,2,0,2) };
            Button select = new()
            {
                Background=Brushes.Transparent, BorderThickness=new Thickness(0),
                HorizontalContentAlignment=HorizontalAlignment.Left, Padding=new Thickness(10,0,42,0),
                Template=RoundedToolbarButtonTemplate(10), Cursor=Cursors.Hand,
                HorizontalAlignment=HorizontalAlignment.Stretch
            };
            StackPanel label = new() { Orientation=Orientation.Horizontal, VerticalAlignment=VerticalAlignment.Center };
            label.Children.Add(new Ellipse { Width=10, Height=10, Fill=BrushFrom(group.ColorHex), Margin=new Thickness(0,0,8,0) });
            label.Children.Add(new TextBlock { Text=group.Name, Foreground=BrushFrom("#FFE3E5EA"), VerticalAlignment=VerticalAlignment.Center });
            label.Children.Add(new TextBlock { Text=$"  {group.Keys.Count}", Foreground=BrushFrom("#FF9DA4B2"), VerticalAlignment=VerticalAlignment.Center });
            select.Content=label;
            select.Click += (_,_) => { _isGroupRemapping=false; _remappingGroup=null; _selectedKeys.Clear(); _selectedKeys.UnionWith(group.Keys); RenderSelection(); ToolStatusText.Text=$"Выделена группа «{group.Name}» ({group.Keys.Count} клавиш)."; };
            row.Children.Add(select);
            Button more = new()
            {
                Content="⋯", Foreground=BrushFrom("#FFB8BEC9"), Background=Brushes.Transparent,
                BorderThickness=new Thickness(0), Template=RoundedToolbarButtonTemplate(14),
                Cursor=Cursors.Hand, Width=28, Height=28, HorizontalAlignment=HorizontalAlignment.Right,
                VerticalAlignment=VerticalAlignment.Center, Margin=new Thickness(0,0,5,0)
            };
            Panel.SetZIndex(more,2); row.Children.Add(more);
            ContextMenu menu = CreateDarkContextMenu(more);
            MenuItem edit = new() { Header="Настроить" }; edit.Click += (_,_) => ShowInlineGroupEditor(group);
            MenuItem delete = new() { Header="Удалить" };
            delete.Click += (_,_) => ShowModal("УДАЛИТЬ ГРУППУ?", new TextBlock { Text=$"Удалить группу «{group.Name}»? Клавиши останутся с текущими цветами.", Foreground=BrushFrom("#FFE3E5EA"), TextWrapping=TextWrapping.Wrap, TextAlignment=TextAlignment.Center },
                ("Удалить", () => { PushUndoSnapshot(); _groups.Remove(group); RenderSelection(); ScheduleProfileSave(); CloseModal(); ShowGroupsManager(); }),
                ("Отмена", CloseModal));
            menu.Items.Add(edit); menu.Items.Add(delete); more.Click += (_,_) => menu.IsOpen=true;
            content.Children.Add(row);
        }
        Button add = CreatePanelButton("＋  Новая группа");
        add.Margin=new Thickness(0,9,0,0); add.Click += (_,_) => ShowGroupEditor(null); content.Children.Add(add);
        frame.Child=content; _groupManagerContent.Children.Add(frame);
        if (!_toolsPanelIsOpen) SetToolsPanelOpen(true);
    }

    private ContextMenu CreateDarkContextMenu(Button target)
    {
        ContextMenu menu = new()
        {
            Placement=PlacementMode.Bottom, PlacementTarget=target,
            Background=BrushFrom("#FF252832"), BorderBrush=BrushFrom("#FF3A3F4A"),
            BorderThickness=new Thickness(1), Padding=new Thickness(4),
            HasDropShadow=true
        };
        Style itemStyle = new(typeof(MenuItem));
        itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, BrushFrom("#FFE3E5EA")));
        itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(14,8,28,8)));
        itemStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        ControlTemplate itemTemplate = new(typeof(MenuItem));
        FrameworkElementFactory surface = new(typeof(Border));
        surface.Name="MenuSurface";
        surface.SetValue(Border.CornerRadiusProperty,new CornerRadius(6));
        surface.SetBinding(Border.BackgroundProperty,new System.Windows.Data.Binding("Background"){RelativeSource=System.Windows.Data.RelativeSource.TemplatedParent});
        FrameworkElementFactory presenter = new(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.ContentSourceProperty,"Header");
        presenter.SetValue(ContentPresenter.MarginProperty,new Thickness(8,3,8,3));
        surface.AppendChild(presenter);
        itemTemplate.VisualTree=surface;
        Trigger hover = new(){Property=MenuItem.IsHighlightedProperty,Value=true};
        hover.Setters.Add(new Setter(Border.BackgroundProperty,BrushFrom("#FF3A414D"),"MenuSurface"));
        itemTemplate.Triggers.Add(hover);
        itemStyle.Setters.Add(new Setter(Control.TemplateProperty,itemTemplate));
        menu.ItemContainerStyle=itemStyle;
        return menu;
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

    private void ShowInlineGroupEditor(EditorGroup group)
    {
        if(_groupManagerContent is null)return;
        _groupManagerContent.Children.Clear();
        _groupManagerContent.Children.Add(new TextBlock{Text=$"НАСТРОЙКИ: {group.Name}",Foreground=BrushFrom("#FFE3E5EA"),FontSize=10,Margin=new Thickness(0,8,0,8)});
        TextBox name=new(){Text=group.Name,Height=30,Padding=new Thickness(7,4,7,4),Background=BrushFrom("#FF191B22"),Foreground=BrushFrom("#FFE3E5EA"),BorderBrush=BrushFrom("#FF454A55")};
        ComboBox state=new(){Height=30,Margin=new Thickness(0,8,0,8),ItemsSource=new[]{"Статичный","Анимация","По сценарию"},SelectedItem=group.State};
        string color=group.ColorHex; StackPanel swatches=BuildColorSwatches(c=>color=c,color);
        _groupManagerContent.Children.Add(name);_groupManagerContent.Children.Add(new TextBlock{Text="Цвет",Foreground=BrushFrom("#FFB8BEC9"),Margin=new Thickness(0,8,0,4)});_groupManagerContent.Children.Add(swatches);
        _groupManagerContent.Children.Add(new TextBlock{Text="Состояние",Foreground=BrushFrom("#FFB8BEC9"),Margin=new Thickness(0,8,0,4)});_groupManagerContent.Children.Add(state);
        Button remap=CreatePanelButton("Переназначить клавиши");remap.Margin=new Thickness(0,8,0,0);
        remap.Click+=(_,_)=>{_isGroupRemapping=true;_remappingGroup=group;_selectedKeys.Clear();_selectedKeys.UnionWith(group.Keys);RenderSelection();ToolStatusText.Text="Выделите клавиши на схеме, затем нажмите «Сохранить».";};_groupManagerContent.Children.Add(remap);
        Button save=CreatePanelButton("Сохранить");save.Margin=new Thickness(0,8,0,0);
        save.Click+=(_,_)=>{if(string.IsNullOrWhiteSpace(name.Text)){ToolStatusText.Text="Введите название группы.";return;}if(_isGroupRemapping&&_remappingGroup==group){if(_selectedKeys.Count==0){ToolStatusText.Text="Выберите хотя бы одну клавишу.";return;}group.Keys=new HashSet<string>(_selectedKeys,StringComparer.Ordinal);}PushUndoSnapshot();group.Name=name.Text.Trim();group.ColorHex=color;group.State=state.SelectedItem?.ToString()??"Статичный";_isGroupRemapping=false;_remappingGroup=null;RenderSelection();ScheduleProfileSave();ShowGroupsManager();};_groupManagerContent.Children.Add(save);
        Button back=CreatePanelButton("Назад");back.Margin=new Thickness(0,5,0,0);back.Click+=(_,_)=>{_isGroupRemapping=false;_remappingGroup=null;ShowGroupsManager();};_groupManagerContent.Children.Add(back);
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

    private void ToggleProfilesFlyout()
    {
        if (_profilesPopup?.IsOpen == true) { _profilesPopup.IsOpen = false; return; }
        SaveCurrentProfile();
        StackPanel body = new() { Margin = new Thickness(10) };
        string[] files = Directory.Exists(_profilesDirectory) ? Directory.GetFiles(_profilesDirectory, "*.json").OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray() : Array.Empty<string>();
        foreach (string file in files)
        {
            try
            {
                EditorProfileData? data = JsonSerializer.Deserialize<EditorProfileData>(File.ReadAllText(file));
                if (data is null) continue;
                bool active = string.Equals(IOPath.GetFullPath(file), IOPath.GetFullPath(ProfilePath(_activeProfileName)), StringComparison.OrdinalIgnoreCase);
                Grid row = new() { Height=40, Margin=new Thickness(0,2,0,2) };
                Button choose = new()
                {
                    Background=Brushes.Transparent, BorderThickness=new Thickness(0),
                    HorizontalContentAlignment=HorizontalAlignment.Left, Padding=new Thickness(12,0,42,0),
                    Cursor=Cursors.Hand, Template=RoundedToolbarButtonTemplate(18),
                    HorizontalAlignment=HorizontalAlignment.Stretch
                };
                StackPanel label = new() { Orientation=Orientation.Horizontal, VerticalAlignment=VerticalAlignment.Center };
                label.Children.Add(new Ellipse { Width=10, Height=10, Fill=BrushFrom(data.ColorHex), Margin=new Thickness(0,0,9,0) });
                label.Children.Add(new TextBlock { Text=data.Name, Foreground=BrushFrom("#FFE3E5EA"), VerticalAlignment=VerticalAlignment.Center });
                choose.Content=label;
                choose.Click += (_,_) => { SaveCurrentProfile(); LoadProfile(file); _profilesPopup!.IsOpen=false; _profilesPopup=null; };
                row.Children.Add(choose);
                Button more = new()
                {
                    Content="⋯", Foreground=BrushFrom("#FFB8BEC9"), Background=Brushes.Transparent,
                    BorderThickness=new Thickness(0), Template=RoundedToolbarButtonTemplate(14),
                    Cursor=Cursors.Hand, Width=28, Height=28, HorizontalAlignment=HorizontalAlignment.Right,
                    VerticalAlignment=VerticalAlignment.Center, Margin=new Thickness(0,0,6,0)
                };
                Panel.SetZIndex(more,2); row.Children.Add(more);
                ContextMenu menu = CreateDarkContextMenu(more);
                MenuItem configure = new() { Header="Настроить" };
                configure.Click += (_,_) => { _profilesPopup!.IsOpen=false; _profilesPopup=null; ShowProfileEditor(data,file); };
                MenuItem remove = new() { Header="Удалить" };
                remove.Click += (_,_) =>
                {
                    int count=Directory.GetFiles(_profilesDirectory,"*.json").Length;
                    if(count<=1){ToolStatusText.Text="Последний профиль удалить нельзя.";return;}
                    _profilesPopup!.IsOpen=false;_profilesPopup=null;
                    ShowModal("ПОДТВЕРДИТЕ УДАЛЕНИЕ",new TextBlock{Text=$"Удалить профиль «{data.Name}»? Это действие нельзя отменить.",Foreground=BrushFrom("#FFE3E5EA"),TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center},
                        ("Удалить",()=>{if(active){string? next=Directory.GetFiles(_profilesDirectory,"*.json").FirstOrDefault(path=>!string.Equals(IOPath.GetFullPath(path),IOPath.GetFullPath(file),StringComparison.OrdinalIgnoreCase));if(next is not null)LoadProfile(next);}File.Delete(file);CloseModal();ToggleProfilesFlyout();}),
                        ("Отмена",()=>{CloseModal();ToggleProfilesFlyout();}));
                };
                menu.Items.Add(configure); menu.Items.Add(remove); more.Click += (_,_) => menu.IsOpen=true;
                body.Children.Add(row);
            }
            catch(Exception ex){Debug.WriteLine("Profile list: "+ex.Message);}
        }
        body.Children.Add(new Border{Height=1,Background=BrushFrom("#FF414652"),Margin=new Thickness(0,8,0,8)});
        Button import=CreatePanelButton("Импорт");import.Height=34;import.Template=RoundedToolbarButtonTemplate(10);import.Margin=new Thickness(0,0,0,2);import.Click+=(_,_)=>ImportProfileFromFile();body.Children.Add(import);
        Button export=CreatePanelButton("Экспорт");export.Height=34;export.Template=RoundedToolbarButtonTemplate(10);export.Margin=new Thickness(0,0,0,2);export.Click+=(_,_)=>ExportActiveProfile();body.Children.Add(export);
        Button create=CreatePanelButton("＋");create.FontSize=24;create.Height=46;create.Template=RoundedToolbarButtonTemplate(10);create.Click+=(_,_)=>{_profilesPopup!.IsOpen=false;_profilesPopup=null;ShowProfileEditor(null,null);};body.Children.Add(create);
        Border card=new(){Width=250,MaxHeight=420,Background=BrushFrom("#FF252832"),BorderThickness=new Thickness(0),CornerRadius=new CornerRadius(18),Child=new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled}};
        _profilesPopup=new Popup{PlacementTarget=_profileButton,Placement=PlacementMode.Bottom,VerticalOffset=6,HorizontalOffset=-54,AllowsTransparency=true,PopupAnimation=PopupAnimation.Slide,StaysOpen=false,Child=card};
        _profilesPopup.Closed+=(_,_)=>_profilesPopup=null;_profilesPopup.IsOpen=true;
    }

    private void ImportProfileFromFile()
    {
        Microsoft.Win32.OpenFileDialog dialog = new() { Filter = "JSON profile (*.json)|*.json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            EditorProfileData? imported = JsonSerializer.Deserialize<EditorProfileData>(File.ReadAllText(dialog.FileName));
            if (imported is null || string.IsNullOrWhiteSpace(imported.Name)) throw new InvalidDataException("Некорректный файл профиля.");
            string target = ProfilePath(imported.Name);
            if (File.Exists(target)) { ToolStatusText.Text = "Профиль с таким названием уже существует."; return; }
            File.Copy(dialog.FileName, target);
            if (_profilesPopup is not null) _profilesPopup.IsOpen = false;
            _profilesPopup = null; ToggleProfilesFlyout();
        }
        catch (Exception ex) { ToolStatusText.Text = "Не удалось импортировать профиль: " + ex.Message; }
    }

    private void ExportActiveProfile()
    {
        Microsoft.Win32.SaveFileDialog dialog = new() { Filter = "JSON profile (*.json)|*.json", FileName = SafeFileName(_activeProfileName) + ".json" };
        if (dialog.ShowDialog(this) == true) { SaveCurrentProfile(); File.Copy(ProfilePath(_activeProfileName), dialog.FileName, true); }
    }

    private void ShowProfileEditor(EditorProfileData? existing, string? oldPath)
    {
        TextBlock limitWarning = new() { Text = "Название профиля не должно превышать 30 символов.", Foreground = BrushFrom("#FFFF6666"), FontSize = 10, TextAlignment = TextAlignment.Center, Visibility = Visibility.Collapsed, Margin = new Thickness(0,5,0,0) };
        TextBox name = new() { Text = existing?.Name ?? "Новый профиль", MaxLength = 30, Height = 32, Padding = new Thickness(8,5,8,5), Background = BrushFrom("#FF191B22"), Foreground = BrushFrom("#FFE3E5EA"), BorderThickness = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Center };
        name.TextChanged += (_,_) => limitWarning.Visibility = name.Text.Length >= 30 ? Visibility.Visible : Visibility.Collapsed;
        _dialogColor = existing?.ColorHex ?? "#FF6D8CFF";
        StackPanel colors = BuildColorSwatches(color => _dialogColor = color, _dialogColor);
        StackPanel body = new();
        body.Children.Add(new TextBlock { Text = "Название профиля", Foreground = BrushFrom("#FFB8BEC9"), Margin = new Thickness(0,0,0,5) });
        body.Children.Add(name);
        body.Children.Add(limitWarning);
        body.Children.Add(new TextBlock { Text = "Цвет профиля", Foreground = BrushFrom("#FFB8BEC9"), Margin = new Thickness(0,12,0,5) });
        colors.HorizontalAlignment = HorizontalAlignment.Center;
        body.Children.Add(colors);
        foreach (TextBlock label in body.Children.OfType<TextBlock>()) label.TextAlignment = TextAlignment.Center;
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
                    _profileSaveTimer.Stop();
                    SaveCurrentProfile();
                    _keyColors.Clear(); _groups.Clear(); _selectedKeys.Clear();
                    foreach(string key in _keyGlows.Keys) UpdateKeyVisual(key);
                    _activeProfileName=profileName; _activeProfileColor=_dialogColor;
                    _lastHardwareFrameSignature=null; RenderSelection(); ScheduleHardwareFrame(); SaveCurrentProfile();
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
                UpdateProfileButton(); CloseModal(); ToggleProfilesFlyout();
            }),
            ("Отмена", () => { CloseModal(); ToggleProfilesFlyout(); }));
    }

    private static ControlTemplate RoundedToolbarButtonTemplate(double radius)
    {
        FrameworkElementFactory border = new(typeof(Border));
        border.Name = "Surface";
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
        border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        FrameworkElementFactory content = new(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        content.SetValue(ContentPresenter.MarginProperty, new Thickness(4,0,4,0));
        border.AppendChild(content);
        ControlTemplate template = new(typeof(Button)) { VisualTree = border };
        Trigger hover = new() { Property = Button.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, BrushFrom("#FF3A3E48"), "Surface"));
        template.Triggers.Add(hover);
        Trigger pressed = new() { Property = Button.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Setter(UIElement.OpacityProperty, 0.78));
        template.Triggers.Add(pressed);
        return template;
    }

    private void UpdateProfileButton()
    {
        if (_profileButton is not null)
        {
            StackPanel content = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8,0,8,0) };
            content.Children.Add(new Ellipse { Width = 10, Height = 10, Fill = BrushFrom(_activeProfileColor), Margin = new Thickness(0,0,8,0), VerticalAlignment = VerticalAlignment.Center });
            content.Children.Add(new TextBlock { Text = _activeProfileName, Foreground = BrushFrom("#FFE3E5EA"), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 120 });
            content.Children.Add(new Border { Width=26, Height=26, CornerRadius=new CornerRadius(13), Background=Brushes.Transparent, Child=new TextBlock { Text="⋯", Foreground=BrushFrom("#FFB8BEC9"), FontSize=17, HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center } });
            _profileButton.Content = content;
            _profileButton.ToolTip = null;
            _profileButton.Width = 196;
        }
    }

    private void ShowModal(string title, UIElement body, params (string Text, Action Action)[] actions)
    {
        ModalOverlay.Children.Clear();
        Grid cardGrid = new();
        cardGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });
        cardGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        cardGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(54) });
        Border card = new() { Width = 360, MaxHeight = 440, Background = BrushFrom("#FF252832"), BorderBrush = BrushFrom("#FF4A505D"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(20), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Child = cardGrid };
        cardGrid.Children.Add(new TextBlock { Text = title, Foreground = BrushFrom("#FFE5E8EF"), FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center });
        Border bodyBorder = new() { Child = body, Margin = new Thickness(0,8,0,8) };
        Grid.SetRow(bodyBorder,1); cardGrid.Children.Add(bodyBorder);
        StackPanel footer = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        foreach (var action in actions)
        {
            Button button = CreatePanelButton(action.Text);
            button.MinWidth = 100; button.Margin = new Thickness(5,0,5,0); button.Template = RoundedToolbarButtonTemplate(10);
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
        _strokeUndoPushed = false;
        _lastPaintedKey = null;
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
