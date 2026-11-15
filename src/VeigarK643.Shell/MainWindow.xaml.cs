using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using IOPath = System.IO.Path;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace VeigarK643.Shell;

public partial class MainWindow : Window
{
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpDoNotRound = 1;

    private enum EditorTool { Brush, Fill, Select, Eraser, Eyedropper }
    private readonly Dictionary<string, List<Border>> _keyGlows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Rect>> _keyBounds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Color> _keyColors = new(StringComparer.Ordinal);
    private readonly HashSet<string> _selectedKeys = new(StringComparer.Ordinal);
    private readonly List<EditorGroup> _groups = new();
    private readonly Stack<EditorSnapshot> _undoHistory = new();
    private readonly Stack<EditorSnapshot> _redoHistory = new();
    private const int HistoryLimit = 250;
    private const long MaxProfileFileBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions LayoutJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly JsonSerializerOptions ProfileJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        MaxDepth = 16
    };
    private const int MaxGroupsPerProfile = 15;
    private readonly List<Button> _toolButtons = new();
    private readonly string _profilesDirectory = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VeigarConfig", "Profiles");
    private readonly string _profileOrderPath = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VeigarConfig", "Profiles", "profiles.order");
    private readonly List<string> _profileOrder = new();
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
    private TextBox? _colorHexText;
    private TextBlock? _colorToolHeading;
    private StackPanel? _colorSettingsSection;
    private Border? _groupSettingsPanel;
    private Button? _profileButton;
    private Popup? _profilesPopup;
    private bool _strokeUndoPushed;
    private bool _isGroupRemapping;
    private EditorGroup? _remappingGroup;
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

    static MainWindow()
    {
        EventManager.RegisterClassHandler(typeof(TextBlock), FrameworkElement.LoadedEvent,
            new RoutedEventHandler(ApplyHeadingLetterSpacing), true);
    }

    public MainWindow()
    {
        InitializeComponent();
        MinimizeGlyph.FontFamily = new FontFamily("Segoe UI");
        CloseGlyph.FontFamily = new FontFamily("Segoe UI");
        CreateKeyVisuals();
        BuildToolbar();
        BuildToolsPanel();
        SetActiveTool(EditorTool.Brush);
        ShowGroupsManager();
        SourceInitialized += MainWindow_SourceInitialized;
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        Closed += (_, _) =>
        {
            _profileSaveTimer.Stop();
            _hardwareUpdateTimer.Stop();
            _reconnectTimer.Stop();
            SaveCurrentProfile();
            if (_deviceNotification != IntPtr.Zero) UnregisterDeviceNotification(_deviceNotification);
        };
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

    private static void ApplyHeadingLetterSpacing(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBlock text) return;
        ApplyTextBlockPresentation(text);
    }

    private static void ApplyTextBlockPresentation(TextBlock text)
    {
        string role = text.Tag as string ?? "Standard";
        if (role == "Icon") return;

        if (role is "Heading" or "MainHeading")
        {
            // Use a native UI font and real glyph shaping. No inserted spacer
            // characters, per-character runs, outlines or synthetic shadows.
            text.FontFamily = new FontFamily("Segoe UI");
            text.FontWeight = FontWeights.SemiBold;
            text.FontStretch = FontStretches.SemiExpanded;
            text.FontSize = role == "MainHeading" ? 15 : 14;
            text.SnapsToDevicePixels = true;
            return;
        }

        string family = role switch
        {
            "Title" => "Dungeon",
            "Status" => "True Lies",
            _ => "Colville"
        };
        text.FontFamily = new FontFamily(family);
        text.FontSize = role switch { "Title" => 15, "Button" => 11, _ => 10 };
    }

    private static TextBlock CreateHeadingText(string value, bool primary = false)
    {
        TextBlock text = new()
        {
            Text = value,
            Tag = primary ? "MainHeading" : "Heading",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = primary ? 15 : 14,
            FontWeight = FontWeights.SemiBold,
            FontStretch = FontStretches.SemiExpanded,
            SnapsToDevicePixels = true
        };
        return text;
    }

    private static void SetHeadingText(TextBlock? text, string value)
    {
        if (text is null) return;
        text.Text = value;
        ApplyTextBlockPresentation(text);
    }

    private void CreateKeyVisuals()
    {
        Uri layoutUri = new("pack://application:,,,/assets/layout/veigar-k643-layout.json", UriKind.Absolute);
        var resource = Application.GetResourceStream(layoutUri);
        if (resource is null) throw new InvalidOperationException("Keyboard layout resource was not found.");
        using Stream stream = resource.Stream;
        KeyboardLayoutConfig? layout = JsonSerializer.Deserialize<KeyboardLayoutConfig>(stream, LayoutJsonOptions);
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
        if (!_keyGlows.TryGetValue(keyName, out List<Border>? glows))
        {
            glows = new List<Border>();
            _keyGlows.Add(keyName, glows);
            _keyBounds.Add(keyName, new List<Rect>());
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
        AddToolButton(tools, "\uE70F", "", EditorTool.Brush);
        AddToolButton(tools, "\uD83D\uDCA7\uFE0E", "", EditorTool.Fill);
        AddToolButton(tools, "\u26F6", "", EditorTool.Select);
        AddToolButton(tools, "\uE75C", "", EditorTool.Eraser);
        AddToolButton(tools, "\uE76D", "", EditorTool.Eyedropper);
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
        Grid profileSurface = new() { Width=196, Height=34 };
        _profileButton.HorizontalContentAlignment = HorizontalAlignment.Left;
        _profileButton.Padding = new Thickness(8,0,40,0);
        profileSurface.Children.Add(_profileButton);
        Button profileMore = new() { Content="⋯", Width=26, Height=26, Margin=new Thickness(0,0,5,0), HorizontalAlignment=HorizontalAlignment.Right, VerticalAlignment=VerticalAlignment.Center, Background=Brushes.Transparent, Foreground=BrushFrom("#FFB8BEC9"), BorderThickness=new Thickness(0), Template=RoundedToolbarButtonTemplate(13), Cursor=Cursors.Hand };
        profileMore.Click += (_,_) => ToggleProfilesFlyout();
        Panel.SetZIndex(profileMore,2);
        profileSurface.Children.Add(profileMore);
        right.Children.Add(profileSurface);
        Grid.SetColumn(right, 1);
        layout.Children.Add(right);
        ToolbarHost.Children.Add(layout);
        UpdateProfileButton();
    }

    private void AddToolButton(Panel host, string glyph, string tooltip, EditorTool tool)
    {
        Button button = new()
        {
            Content = new TextBlock { Text = glyph, Tag="Icon", FontFamily = IconFontFor(glyph), FontSize = 17, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            Tag = tool, Width = 42, Height = 34, Margin = new Thickness(2,0,2,0),
            Padding = new Thickness(0), ToolTip = null, Foreground = BrushFrom("#FFDCE0E8"),
            Background = BrushFrom("#FF2A2E38"), BorderBrush = BrushFrom("#FF3E4350"),
            BorderThickness = new Thickness(0), Template = RoundedToolbarButtonTemplate(6), Cursor = Cursors.Hand, Focusable = false
        };
        button.Click += (_, _) => SetActiveTool(tool);
        _toolButtons.Add(button);
        host.Children.Add(button);
    }

    private static FontFamily IconFontFor(string glyph) => glyph is "\uE70F" or "\uE75C" or "\uE76D" ? new FontFamily("Segoe MDL2 Assets") : new FontFamily("Segoe UI Symbol");

    private static Button CreateToolbarButton(string glyph, string tooltip, Action action)
    {
        Button button = new()
        {
            Content = new TextBlock { Text = glyph, Tag="Icon", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 17, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
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
        TextBlock parametersHeading = CreateHeadingText("ПАРАМЕТРЫ", primary: true);
        parametersHeading.Foreground = BrushFrom("#FFE3E5EA");
        parametersHeading.Margin = new Thickness(0,0,0,16);
        root.Children.Add(parametersHeading);
        _colorSettingsSection = new StackPanel();
        _colorToolHeading = CreateHeadingText("ЦВЕТ КИСТИ");
        _colorToolHeading.Foreground = BrushFrom("#FF9DA4B2");
        _colorToolHeading.Margin = new Thickness(0,0,0,8);
        _colorSettingsSection.Children.Add(_colorToolHeading);
        Grid paletteGrid=new(){Width=230,Height=178,HorizontalAlignment=HorizontalAlignment.Left};
        paletteGrid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
        paletteGrid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(28)});
        _colorField=new Canvas{Width=194,Height=178,Background=Brushes.Transparent,ClipToBounds=true};
        Rectangle hue=new(){Width=194,Height=178,Fill=CreateHueBrush(1)};
        Rectangle black=new(){Width=194,Height=178,Fill=new LinearGradientBrush{StartPoint=new Point(0,0),EndPoint=new Point(0,1),GradientStops=new GradientStopCollection{new GradientStop(Color.FromArgb(0,0,0,0),0),new GradientStop(Colors.Black,1)}}};
        _colorField.Children.Add(hue);_colorField.Children.Add(black);
        _colorThumb=new Ellipse{Width=13,Height=13,Stroke=Brushes.White,StrokeThickness=2,Fill=Brushes.Transparent,IsHitTestVisible=false};
        _colorField.Children.Add(_colorThumb);
        _colorField.MouseLeftButtonDown+=ColorField_MouseDown;_colorField.MouseMove+=ColorField_MouseMove;_colorField.MouseLeftButtonUp+=ColorField_MouseUp;
        Grid.SetColumn(_colorField,0);paletteGrid.Children.Add(_colorField);
        _saturationSlider=new Slider{Minimum=0,Maximum=1,Value=1,Orientation=Orientation.Vertical,Width=22,Height=172,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center,IsMoveToPointEnabled=false,LargeChange=0.08,SmallChange=0.01,ToolTip="Насыщенность"};
        _saturationSlider.ValueChanged+=(_,_)=>{UpdateHueBrush();UpdateColorFromControls();};
        Grid.SetColumn(_saturationSlider,1);paletteGrid.Children.Add(_saturationSlider);
        _colorSettingsSection.Children.Add(paletteGrid);
        _colorPreview=new Border{Height=34,CornerRadius=new CornerRadius(5),Background=new SolidColorBrush(_currentColor),BorderThickness=new Thickness(0),Margin=new Thickness(0,10,0,0)};
        _colorSettingsSection.Children.Add(_colorPreview);
        _colorHexText=CreateRoundedTextBox("#FF0000");_colorHexText.Margin=new Thickness(0,5,0,0);_colorHexText.HorizontalContentAlignment=HorizontalAlignment.Center;_colorHexText.MaxLength=12;
        _colorHexText.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Enter){ApplyHexColorInput();e.Handled=true;}};
        _colorHexText.LostKeyboardFocus+=(_,_)=>ApplyHexColorInput();
        _colorSettingsSection.Children.Add(_colorHexText);
        TextBlock recentColorsHeading = CreateHeadingText("ПОСЛЕДНИЕ ЦВЕТА");
        recentColorsHeading.Foreground = BrushFrom("#FF9DA4B2");
        recentColorsHeading.Margin = new Thickness(0,12,0,5);
        _colorSettingsSection.Children.Add(recentColorsHeading);
        _recentColorsPanel=new StackPanel{Orientation=Orientation.Horizontal};_colorSettingsSection.Children.Add(_recentColorsPanel);
        _colorHintText=new TextBlock{Text="Выберите оттенок и яркость на палитре. Насыщенность регулируется ползунком справа.",Foreground=BrushFrom("#FF9DA4B2"),FontSize=10,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,14,0,0)};
        _colorSettingsSection.Children.Add(_colorHintText);
        root.Children.Add(_colorSettingsSection);
        _groupManagerContent=new StackPanel{Visibility=Visibility.Collapsed};
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

    private static Button CreatePanelButton(string text) => new()
    {
        Content = text, Height = 34, HorizontalContentAlignment = HorizontalAlignment.Center,
        Foreground = BrushFrom("#FFE3E5EA"), Background = BrushFrom("#FF343945"),
        BorderThickness = new Thickness(0), Template = RoundedToolbarButtonTemplate(9),
        Cursor = Cursors.Hand, Focusable = false
    };

    private static Brush BrushFrom(string color) => (Brush)new BrushConverter().ConvertFromString(color)!;

    private void SetActiveTool(EditorTool tool)
    {
        // Leaving group settings must never leave keyboard clicks in remapping mode.
        if (_isGroupRemapping || _remappingGroup is not null) ShowGroupsManager();
        _activeTool = tool;
        foreach (Button button in _toolButtons)
        {
            bool active = button.Tag is EditorTool candidate && candidate == tool;
            button.Background = BrushFrom(active ? "#FF515A6B" : "#FF2A2E38");
            button.BorderBrush = BrushFrom(active ? "#FF818B9F" : "#FF3E4350");
        }
        bool colorTool = tool == EditorTool.Brush || tool == EditorTool.Fill || tool == EditorTool.Eyedropper;
        if (_colorSettingsSection is not null) _colorSettingsSection.Visibility = colorTool ? Visibility.Visible : Visibility.Collapsed;
        SetHeadingText(_colorToolHeading, tool switch
        {
            EditorTool.Fill => "ЦВЕТ ЗАЛИВКИ",
            EditorTool.Eyedropper => "КОПИРОВАНИЕ ЦВЕТА",
            _ => "ЦВЕТ КИСТИ"
        });
        if (_groupManagerContent is not null) _groupManagerContent.Visibility = Visibility.Visible;
        ToolStatusText.Text = tool switch
        {
            EditorTool.Brush => "Кисть: проведите по клавишам, чтобы назначить выбранный цвет.",
            EditorTool.Fill => "Заливка: щёлкните по клавише, чтобы залить выделение или всю клавиатуру.",
            EditorTool.Select => "Выделение: щелчок — выбрать; Shift — добавить/снять; перетаскивание — рамка; Esc — снять выделение.",
            EditorTool.Eraser => "Ластик: проведите по клавишам, чтобы удалить их подсветку.",
            _ => "Пипетка: щёлкните по клавише, чтобы взять её цвет."
        };
        if (!_toolsPanelIsOpen) SetToolsPanelOpen(true);
    }

    private void Keyboard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Recover from a stale remapping flag if its editor panel was removed unexpectedly.
        if (_isGroupRemapping && !IsGroupRemappingSessionActive) ShowGroupsManager();
        Point point = e.GetPosition(KeyHitboxCanvas);
        _isPointerDown = true;
        _strokeUndoPushed = false;
        _dragStart = point;
        _shiftAtStart = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        _lastPaintedKey = null;
        Mouse.Capture(KeyHitboxCanvas, CaptureMode.Element);

        string? key = FindKeyAt(point);
        if (IsGroupRemappingSessionActive && _remappingGroup is not null)
        {
            if (key is not null) { if (!_selectedKeys.Add(key)) _selectedKeys.Remove(key); RenderSelection(); ToolStatusText.Text = $"Переназначение «{_remappingGroup.Name}»: {_selectedKeys.Count} клавиш."; }
        }
        else if (_activeTool == EditorTool.Eyedropper)
        {
            if (key is not null && _keyColors.TryGetValue(key, out Color sampled)) { SetCurrentColor(sampled); ToolStatusText.Text=$"Цвет взят с клавиши {key}."; }
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
                    EditorGroup? group = GetPriorityGroupForKey(key);
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
        if (IsGroupRemappingSessionActive) return;
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
        if (IsGroupRemappingSessionActive) { _isPointerDown=false; _lastPaintedKey=null; _strokeUndoPushed=false; Mouse.Capture(null); e.Handled=true; return; }
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
                    EditorGroup? group = GetPriorityGroupForKey(key);
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

    private static RadialGradientBrush CreateRgbGlow(Color color)
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
        // Draw lower-priority groups first so the highest-priority group remains visible on overlaps.
        for (int i = _groups.Count - 1; i >= 0; i--)
            AddOutline(_groups[i].Keys, _groups[i].ColorHex, 1.4, 30);
        if (_selectedKeys.Count > 0)
            AddOutline(_selectedKeys, "#FF9DB7FF", 1.6, 24);
    }

    private EditorGroup? GetPriorityGroupForKey(string key) =>
        _groups.FirstOrDefault(group => group.Keys.Contains(key));

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
        if (_colorHexText is not null && !_colorHexText.IsKeyboardFocused) _colorHexText.Text = $"#{_currentColor.R:X2}{_currentColor.G:X2}{_currentColor.B:X2}";
    }

    private void SetCurrentColor(Color color)
    {
        _currentColor=color;
        _selectedHue=ColorToHue(color);
        _selectedValue=Math.Max(color.R,Math.Max(color.G,color.B))/255d;
        if(_saturationSlider is not null)_saturationSlider.Value=ColorToSaturation(color);
        SyncColorThumbPosition();
        if(_colorPreview is not null)_colorPreview.Background=new SolidColorBrush(color);
        if(_colorHexText is not null && !_colorHexText.IsKeyboardFocused)_colorHexText.Text=$"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private void SyncColorThumbPosition()
    {
        if(_colorField is null || _colorThumb is null)return;
        double x=Math.Clamp(_selectedHue/360d*_colorField.Width,0,_colorField.Width);
        double y=Math.Clamp((1-_selectedValue)*_colorField.Height,0,_colorField.Height);
        Canvas.SetLeft(_colorThumb,Math.Clamp(x-_colorThumb.Width/2,0,_colorField.Width-_colorThumb.Width));
        Canvas.SetTop(_colorThumb,Math.Clamp(y-_colorThumb.Height/2,0,_colorField.Height-_colorThumb.Height));
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
        foreach(Color recent in _recentColors){Button b=new(){Width=23,Height=23,Margin=new Thickness(0,0,5,0),Padding=new Thickness(0),Background=new SolidColorBrush(recent),BorderThickness=new Thickness(0),Template=RoundedToolbarButtonTemplate(6),ToolTip=null};b.Click+=(_,_)=>SetCurrentColor(recent);_recentColorsPanel.Children.Add(b);}
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
            else if (_isGroupRemapping) ShowGroupsManager();
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
        LoadProfileOrder();
        string[] files = GetOrderedProfileFiles();
        if (files.Length == 0)
        {
            if (!_profileOrder.Contains(SafeFileName(_activeProfileName), StringComparer.OrdinalIgnoreCase))
                _profileOrder.Insert(0, SafeFileName(_activeProfileName));
            SaveProfileOrder();
            SaveCurrentProfile();
            return;
        }

        // The first profile in the saved order is the profile loaded when the application starts.
        LoadProfile(files[0]);
    }

    private void LoadProfileOrder()
    {
        try
        {
            if (File.Exists(_profileOrderPath))
            {
                if (new FileInfo(_profileOrderPath).Length > MaxProfileFileBytes)
                    throw new InvalidDataException("Profile order file is too large.");
                List<string>? loadedOrder = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_profileOrderPath), ProfileJsonOptions);
                if (loadedOrder is not null)
                    _profileOrder.AddRange(loadedOrder.Where(name => !string.IsNullOrWhiteSpace(name)));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Profile order load failed: " + ex.Message);
            _profileOrder.Clear();
        }

        string[] existing = Directory.GetFiles(_profilesDirectory, "*.json")
            .Select(path => IOPath.GetFileNameWithoutExtension(path))
            .ToArray();

        if (_profileOrder.Count == 0 && existing.Contains(SafeFileName(_activeProfileName), StringComparer.OrdinalIgnoreCase))
            _profileOrder.Add(SafeFileName(_activeProfileName));

        List<string> normalized = _profileOrder
            .Where(name => !string.IsNullOrWhiteSpace(name) && existing.Contains(name, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (string name in existing.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            if (!normalized.Contains(name, StringComparer.OrdinalIgnoreCase))
                normalized.Add(name);

        _profileOrder.Clear();
        _profileOrder.AddRange(normalized);
        SaveProfileOrder();
    }

    private string[] GetOrderedProfileFiles()
    {
        if (!Directory.Exists(_profilesDirectory)) return Array.Empty<string>();
        Dictionary<string, int> orderByName = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < _profileOrder.Count; i++)
            orderByName.TryAdd(_profileOrder[i], i);

        return Directory.GetFiles(_profilesDirectory, "*.json")
            .OrderBy(path => orderByName.TryGetValue(IOPath.GetFileNameWithoutExtension(path), out int index) ? index : int.MaxValue)
            .ThenBy(path => IOPath.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void SaveProfileOrder()
    {
        try
        {
            Directory.CreateDirectory(_profilesDirectory);
            WriteAllTextAtomically(_profileOrderPath, JsonSerializer.Serialize(_profileOrder, ProfileJsonOptions));
        }
        catch (Exception ex) { Debug.WriteLine("Profile order save failed: " + ex.Message); }
    }

    private void RenameProfileOrderEntry(string? oldPath, string newPath)
    {
        string newName = IOPath.GetFileNameWithoutExtension(newPath);
        if (oldPath is not null)
        {
            string oldName = IOPath.GetFileNameWithoutExtension(oldPath);
            int index = _profileOrder.FindIndex(name => string.Equals(name, oldName, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) _profileOrder[index] = newName;
        }
        if (!_profileOrder.Contains(newName, StringComparer.OrdinalIgnoreCase))
            _profileOrder.Add(newName);
        SaveProfileOrder();
    }

    private void CommitProfileOrder(int sourceIndex, int targetIndex)
    {
        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex >= _profileOrder.Count || targetIndex >= _profileOrder.Count) return;
        string moved = _profileOrder[sourceIndex];
        _profileOrder.RemoveAt(sourceIndex);
        _profileOrder.Insert(targetIndex, moved);
        SaveProfileOrder();
        ToolStatusText.Text = $"Приоритет профилей обновлён. «{moved}» — позиция {targetIndex + 1}.";
    }

    private void AttachProfileDragDrop(StackPanel body, Grid row, TextBlock handle)
    {
        AttachAnimatedLocalReorder(
            body,
            row,
            handle,
            () => body.Children.OfType<Grid>().Where(item => item.Tag is string).ToList(),
            CommitProfileOrder);
    }

    private static string SafeFileName(string name)
    {
        foreach (char c in IOPath.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        name = name.Trim().TrimEnd('.', ' ');
        if (string.IsNullOrWhiteSpace(name)) return "Profile";

        string deviceName = name.Split('.')[0];
        if (deviceName.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            (deviceName.Length == 4 &&
             (deviceName.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || deviceName.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
             deviceName[3] is >= '1' and <= '9'))
            name = "_" + name;

        return name;
    }

    private string ProfilePath(string name) => IOPath.Combine(_profilesDirectory, SafeFileName(name) + ".json");

    private static void WriteAllTextAtomically(string path, string content)
    {
        string directory = IOPath.GetDirectoryName(path) ?? throw new InvalidOperationException("A target directory is required.");
        Directory.CreateDirectory(directory);
        string temporaryPath = IOPath.Combine(directory, $".{IOPath.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporaryPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
            catch (Exception cleanupError)
            {
                Debug.WriteLine("Temporary profile cleanup failed: " + cleanupError.Message);
            }
        }
    }

    private static bool TryParseProfileColor(string? value, out Color color)
    {
        try
        {
            if (ColorConverter.ConvertFromString(value) is Color parsed)
            {
                color = parsed;
                return true;
            }
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or NotSupportedException)
        {
            // Invalid profile color values are rejected during profile normalization.
        }

        color = Colors.Transparent;
        return false;
    }

    private static EditorProfileData ReadProfileFile(string path, bool requireName = false)
    {
        FileInfo file = new(path);
        if (file.Length > MaxProfileFileBytes)
            throw new InvalidDataException("Profile file exceeds the 1 MiB safety limit.");

        EditorProfileData? data = JsonSerializer.Deserialize<EditorProfileData>(File.ReadAllText(path), ProfileJsonOptions);
        if (data is null) throw new InvalidDataException("Profile file is empty or invalid.");
        if (requireName && string.IsNullOrWhiteSpace(data.Name))
            throw new InvalidDataException("Profile name is required.");

        data.Name = string.IsNullOrWhiteSpace(data.Name) ? IOPath.GetFileNameWithoutExtension(path) : data.Name.Trim();
        if (!TryParseProfileColor(data.ColorHex, out _)) data.ColorHex = "#FF6D8CFF";

        Dictionary<string, string> keyColors = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> entry in data.KeyColors ?? new Dictionary<string, string>())
            if (!string.IsNullOrWhiteSpace(entry.Key) && TryParseProfileColor(entry.Value, out _))
                keyColors[entry.Key] = entry.Value;
        if (keyColors.Count > 512) throw new InvalidDataException("Profile contains too many key color entries.");
        data.KeyColors = keyColors;

        List<EditorGroupData> groups = new();
        foreach (EditorGroupData? group in data.Groups ?? new List<EditorGroupData>())
        {
            if (group is null) continue;
            group.Name = string.IsNullOrWhiteSpace(group.Name) ? "Группа" : group.Name.Trim();
            if (!TryParseProfileColor(group.ColorHex, out _)) group.ColorHex = "#FF6D8CFF";
            group.State = string.IsNullOrWhiteSpace(group.State) ? "Статичный" : group.State;
            group.Keys = (group.Keys ?? new List<string>())
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (group.Keys.Count > 512) throw new InvalidDataException("A group contains too many keys.");
            groups.Add(group);
        }
        if (groups.Count > 128) throw new InvalidDataException("Profile contains too many groups.");
        data.Groups = groups;
        return data;
    }

    private bool SaveCurrentProfile()
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
            WriteAllTextAtomically(ProfilePath(_activeProfileName), JsonSerializer.Serialize(data, ProfileJsonOptions));
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Profile save failed: " + ex.Message);
            ToolStatusText.Text = "Не удалось сохранить профиль. Переключение профиля отменено.";
            return false;
        }
    }

    private void LoadProfile(string path)
    {
        try
        {
            EditorProfileData data = ReadProfileFile(path);
            Dictionary<string, Color> loadedColors = new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pair in data.KeyColors)
                if (TryParseProfileColor(pair.Value, out Color color))
                    loadedColors[pair.Key] = color;

            List<EditorGroup> loadedGroups = data.Groups
                .Select(group => new EditorGroup
                {
                    Name = group.Name,
                    ColorHex = group.ColorHex,
                    State = group.State,
                    Keys = new HashSet<string>(group.Keys, StringComparer.Ordinal)
                })
                .ToList();

            // Parse and validate the entire profile before changing the active editor state.
            _profileSaveTimer.Stop();
            _keyColors.Clear();
            foreach (KeyValuePair<string, Color> pair in loadedColors) _keyColors[pair.Key] = pair.Value;
            foreach (string key in _keyGlows.Keys) UpdateKeyVisual(key);
            _groups.Clear();
            _groups.AddRange(loadedGroups);
            _selectedKeys.Clear();
            _activeProfileName = data.Name;
            _activeProfileColor = data.ColorHex;
            RenderSelection();
            ScheduleHardwareFrame();
            UpdateProfileButton();
            if (_groupManagerContent is not null) ShowGroupsManager();
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Profile load failed: " + ex.Message);
            ToolStatusText.Text = "Не удалось загрузить профиль. Проверьте файл профиля.";
        }
    }

    private void CommitGroupOrder(int sourceIndex, int targetIndex)
    {
        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex >= _groups.Count || targetIndex >= _groups.Count) return;
        EditorGroup moved = _groups[sourceIndex];
        _groups.RemoveAt(sourceIndex);
        _groups.Insert(targetIndex, moved);
        RenderSelection();
        ScheduleProfileSave();
        ToolStatusText.Text = $"Приоритет группы «{moved.Name}» обновлён. Верхние группы главнее нижних.";
    }

    private void AttachGroupDragDrop(StackPanel listHost, Grid row, TextBlock handle, EditorGroup group)
    {
        AttachAnimatedLocalReorder(
            listHost,
            row,
            handle,
            () => listHost.Children.OfType<Grid>().Where(item => item.Tag is EditorGroup).ToList(),
            CommitGroupOrder);
    }

    private static void AttachAnimatedLocalReorder(
        StackPanel listHost,
        Grid row,
        TextBlock handle,
        Func<List<Grid>> getRows,
        Action<int, int> commitOrder)
    {
        Point pointerStart = default;
        double startRowTop = 0;
        double firstRowTop = 0;
        double lastRowTop = 0;
        double rowStep = 0;
        int sourceIndex = -1;
        int targetIndex = -1;
        bool dragging = false;
        TranslateTransform rowTransform = new();
        row.RenderTransform = rowTransform;

        handle.PreviewMouseLeftButtonDown += (_, e) =>
        {
            pointerStart = e.GetPosition(listHost);
            dragging = false;
            sourceIndex = -1;
            targetIndex = -1;
            // Capture immediately so the pointer can leave the narrow grip while dragging.
            Mouse.Capture(handle, CaptureMode.Element);
            e.Handled = true;
        };

        handle.PreviewMouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            List<Grid> rows = getRows();
            if (rows.Count < 2) return;

            Point pointer = e.GetPosition(listHost);
            if (!dragging)
            {
                if (Math.Abs(pointer.X - pointerStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
                    Math.Abs(pointer.Y - pointerStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;

                sourceIndex = rows.IndexOf(row);
                if (sourceIndex < 0) return;
                targetIndex = sourceIndex;
                startRowTop = row.TranslatePoint(new Point(0, 0), listHost).Y;
                firstRowTop = rows[0].TranslatePoint(new Point(0, 0), listHost).Y;
                lastRowTop = rows[^1].TranslatePoint(new Point(0, 0), listHost).Y;
                rowStep = rows.Count > 1
                    ? rows[1].TranslatePoint(new Point(0, 0), listHost).Y - firstRowTop
                    : row.ActualHeight + row.Margin.Top + row.Margin.Bottom;
                if (rowStep <= 0) return;

                dragging = true;
                listHost.ClipToBounds = true;
                row.Opacity = 0.94;
                Panel.SetZIndex(row, 20);
                row.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 13,
                    ShadowDepth = 3,
                    Opacity = 0.35
                };
                Mouse.Capture(handle, CaptureMode.Element);
            }

            // The dragged row is clamped to the list's first and last slots.
            double desiredTop = Math.Clamp(startRowTop + pointer.Y - pointerStart.Y, firstRowTop, lastRowTop);
            rowTransform.Y = desiredTop - startRowTop;
            int nextIndex = Math.Clamp(
                (int)Math.Round((desiredTop - firstRowTop) / rowStep, MidpointRounding.AwayFromZero),
                0,
                rows.Count - 1);

            if (nextIndex != targetIndex)
            {
                targetIndex = nextIndex;
                for (int i = 0; i < rows.Count; i++)
                {
                    if (i == sourceIndex) continue;
                    double offset = targetIndex > sourceIndex && i > sourceIndex && i <= targetIndex
                        ? -rowStep
                        : targetIndex < sourceIndex && i >= targetIndex && i < sourceIndex
                            ? rowStep
                            : 0;
                    if (rows[i].RenderTransform is not TranslateTransform siblingTransform)
                    {
                        siblingTransform = new TranslateTransform();
                        rows[i].RenderTransform = siblingTransform;
                    }
                    siblingTransform.BeginAnimation(
                        TranslateTransform.YProperty,
                        new System.Windows.Media.Animation.DoubleAnimation(offset, TimeSpan.FromMilliseconds(135))
                        {
                            EasingFunction = new System.Windows.Media.Animation.CubicEase
                            {
                                EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                            }
                        });
                }
            }
            e.Handled = true;
        };

        handle.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (!dragging)
            {
                Mouse.Capture(null);
                return;
            }
            dragging = false;
            double currentOffset = rowTransform.Y;
            int finalIndex = targetIndex;
            Mouse.Capture(null);

            List<Grid> rows = getRows();
            if (finalIndex >= 0 && finalIndex != sourceIndex && sourceIndex < rows.Count)
            {
                // Reorder the existing row controls; no system-wide drag image is created.
                MoveVisualRow(listHost, rows, row, finalIndex);
                commitOrder(sourceIndex, finalIndex);
                currentOffset -= (finalIndex - sourceIndex) * rowStep;
            }

            foreach (Grid sibling in getRows())
            {
                if (ReferenceEquals(sibling, row)) continue;
                if (sibling.RenderTransform is TranslateTransform siblingTransform)
                {
                    siblingTransform.BeginAnimation(TranslateTransform.YProperty, null);
                    siblingTransform.Y = 0;
                }
            }

            row.Opacity = 1;
            row.Effect = null;
            Panel.SetZIndex(row, 0);
            rowTransform.BeginAnimation(
                TranslateTransform.YProperty,
                new System.Windows.Media.Animation.DoubleAnimation(currentOffset, 0, TimeSpan.FromMilliseconds(145))
                {
                    EasingFunction = new System.Windows.Media.Animation.CubicEase
                    {
                        EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                    }
                });
            e.Handled = true;
        };

        handle.LostMouseCapture += (_, _) =>
        {
            if (!dragging) return;
            dragging = false;
            row.Opacity = 1;
            row.Effect = null;
            Panel.SetZIndex(row, 0);
            rowTransform.BeginAnimation(
                TranslateTransform.YProperty,
                new System.Windows.Media.Animation.DoubleAnimation(rowTransform.Y, 0, TimeSpan.FromMilliseconds(120)));
            foreach (Grid sibling in getRows())
            {
                if (ReferenceEquals(sibling, row)) continue;
                if (sibling.RenderTransform is TranslateTransform siblingTransform)
                    siblingTransform.BeginAnimation(
                        TranslateTransform.YProperty,
                        new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(120)));
            }
        };
    }

    private static void MoveVisualRow(StackPanel host, List<Grid> rows, Grid movingRow, int targetIndex)
    {
        host.Children.Remove(movingRow);
        List<Grid> remaining = rows.Where(item => !ReferenceEquals(item, movingRow)).ToList();
        int insertAt = targetIndex >= remaining.Count
            ? host.Children.IndexOf(remaining[^1]) + 1
            : host.Children.IndexOf(remaining[targetIndex]);
        host.Children.Insert(insertAt, movingRow);
    }

    private bool IsGroupRemappingSessionActive =>
        _isGroupRemapping &&
        _remappingGroup is not null &&
        _groupSettingsPanel is not null &&
        _groupManagerContent is not null &&
        _groupManagerContent.Children.Contains(_groupSettingsPanel);

    private void ResetGroupRemapping(bool restoreGroupSelection)
    {
        EditorGroup? group = _remappingGroup;
        bool wasRemapping = _isGroupRemapping || group is not null;
        _isGroupRemapping = false;
        _remappingGroup = null;
        if (!wasRemapping) return;

        _isPointerDown = false;
        _isMarquee = false;
        _lastPaintedKey = null;
        _strokeUndoPushed = false;
        if (_marquee is not null) SelectionOverlayCanvas.Children.Remove(_marquee);
        _marquee = null;
        if (Mouse.Captured == KeyHitboxCanvas) Mouse.Capture(null);

        if (restoreGroupSelection && group is not null && _groups.Contains(group))
        {
            _selectedKeys.Clear();
            _selectedKeys.UnionWith(group.Keys);
            RenderSelection();
        }
    }

    private void ShowGroupsManager()
    {
        if(_groupManagerContent is null)return;
        // Rebuilding the manager also closes any inline editor and restores its saved key set.
        ResetGroupRemapping(restoreGroupSelection:true);
        _groupManagerContent.Visibility=Visibility.Visible;_groupManagerContent.Children.Clear();_groupSettingsPanel=null;
        StackPanel content=new();
        content.Children.Add(new TextBlock{Text="ГРУППЫ КЛАВИШ",Tag="Heading",Foreground=BrushFrom("#FF9DA4B2"),FontSize=10,Margin=new Thickness(0,0,0,4)});
        content.Children.Add(new TextBlock{Text="Перетащите группу за значок: верхние группы имеют приоритет.",Foreground=BrushFrom("#FF89919F"),FontSize=9,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,7)});
        if(_groups.Count==0)content.Children.Add(new TextBlock{Text="Групп пока нет. Выделите клавиши и нажмите «Новая группа».",Foreground=BrushFrom("#FF89919F"),FontSize=9,TextWrapping=TextWrapping.Wrap});
        foreach(EditorGroup group in _groups.ToArray())
        {
            Grid row=new(){Height=38,Margin=new Thickness(0,2,0,2),Tag=group};
            bool selected=group.Keys.Count>0&&group.Keys.All(_selectedKeys.Contains);
            Button choose=new(){Background=selected?BrushFrom("#FF3A414F"):Brushes.Transparent,BorderThickness=new Thickness(0),HorizontalContentAlignment=HorizontalAlignment.Left,Padding=new Thickness(27,0,42,0),Template=RoundedToolbarButtonTemplate(8),Cursor=Cursors.Hand,HorizontalAlignment=HorizontalAlignment.Stretch};
            StackPanel label=new(){Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
            label.Children.Add(new Ellipse{Width=10,Height=10,Fill=BrushFrom(group.ColorHex),Margin=new Thickness(0,0,8,0)});
            label.Children.Add(new TextBlock{Text=group.Name,Foreground=BrushFrom("#FFE3E5EA"),VerticalAlignment=VerticalAlignment.Center});
            label.Children.Add(new TextBlock{Text=$"  {group.Keys.Count}",Foreground=BrushFrom("#FF9DA4B2"),VerticalAlignment=VerticalAlignment.Center});
            choose.Content=label;choose.Click+=(_,_)=>{ResetGroupRemapping(restoreGroupSelection:false);_selectedKeys.Clear();_selectedKeys.UnionWith(group.Keys);RenderSelection();ToolStatusText.Text=$"Выделена группа «{group.Name}» ({group.Keys.Count} клавиш).";ShowGroupsManager();};
            row.Children.Add(choose);
            TextBlock dragHandle=new(){Text="⠿",FontFamily=new FontFamily("Segoe UI Symbol"),FontSize=17,Foreground=BrushFrom("#FF818A99"),Background=Brushes.Transparent,Width=20,TextAlignment=TextAlignment.Center,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(5,0,0,0),Cursor=Cursors.SizeAll,ToolTip="Перетащить для изменения приоритета"};
            Panel.SetZIndex(dragHandle,3);row.Children.Add(dragHandle);AttachGroupDragDrop(content,row,dragHandle,group);
            Button more=new(){Content="⋯",Foreground=BrushFrom("#FFB8BEC9"),Background=Brushes.Transparent,BorderThickness=new Thickness(0),Template=RoundedToolbarButtonTemplate(14),Cursor=Cursors.Hand,Width=28,Height=28,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,5,0)};
            Panel.SetZIndex(more,2);row.Children.Add(more);
            ContextMenu menu=CreateDarkContextMenu(more);
            MenuItem edit=new(){Header="Настроить"};edit.Click+=(_,_)=>ShowInlineGroupEditor(group);
            MenuItem delete=new(){Header="Удалить"};delete.Click+=(_,_)=>ShowModal("УДАЛИТЬ ГРУППУ?",new TextBlock{Text=$"Удалить группу «{group.Name}»? Клавиши останутся с текущими цветами.",Foreground=BrushFrom("#FFE3E5EA"),TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center},("Удалить",()=>{PushUndoSnapshot();_groups.Remove(group);RenderSelection();ScheduleProfileSave();CloseModal();ShowGroupsManager();}),("Отмена",CloseModal));
            menu.Items.Add(edit);menu.Items.Add(delete);more.Click+=(_,_)=>menu.IsOpen=true;content.Children.Add(row);
        }
        Button add=CreatePanelButton("＋  Новая группа");add.Margin=new Thickness(0,9,0,0);add.Click+=(_,_)=>ShowGroupEditor(null);content.Children.Add(add);
        Border frame=new(){Background=BrushFrom("#FF22252E"),BorderBrush=BrushFrom("#FF3A3F4A"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Padding=new Thickness(10),Child=content,Margin=new Thickness(0,14,0,0)};
        _groupManagerContent.Children.Add(frame);
        if(!_toolsPanelIsOpen)SetToolsPanelOpen(true);
    }

    private static ContextMenu CreateDarkContextMenu(Button target)
    {
        ContextMenu menu = new()
        {
            Placement=PlacementMode.Bottom, PlacementTarget=target,
            Background=Brushes.Transparent, BorderThickness=new Thickness(0), Padding=new Thickness(0), HasDropShadow=true
        };
        FrameworkElementFactory menuSurface = new(typeof(Border));
        menuSurface.SetValue(Border.BackgroundProperty, BrushFrom("#FF252832"));
        menuSurface.SetValue(Border.BorderBrushProperty, BrushFrom("#FF3A3F4A"));
        menuSurface.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        menuSurface.SetValue(Border.PaddingProperty, new Thickness(4));
        FrameworkElementFactory itemsPresenter = new(typeof(ItemsPresenter));
        menuSurface.AppendChild(itemsPresenter);
        menu.Template = new ControlTemplate(typeof(ContextMenu)) { VisualTree = menuSurface };
        Style itemStyle = new(typeof(MenuItem));
        itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, BrushFrom("#FFE3E5EA")));
        itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(14,8,28,8)));
        itemStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        itemStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
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
        if(_groups.Count>=MaxGroupsPerProfile){ToolStatusText.Text="Достигнут максимум: 15 групп клавиш в одном профиле.";return;}
        if(_selectedKeys.Count==0){ToolStatusText.Text="Сначала выделите клавиши, которые нужно объединить в группу.";return;}
        TextBlock warning=new(){Text="Название группы не должно превышать 30 символов.",Foreground=BrushFrom("#FFFF6666"),FontSize=10,TextAlignment=TextAlignment.Center,Visibility=Visibility.Collapsed,Margin=new Thickness(0,5,0,0)};
        TextBox name=CreatePlaceholderTextBox("Новая группа");name.MaxLength=30;name.TextChanged+=(_,_)=>warning.Visibility=name.Text.Length>=30?Visibility.Visible:Visibility.Collapsed;
        string color="#FF6D8CFF";
        StackPanel body=new();
        body.Children.Add(new TextBlock{Text="Название",Foreground=BrushFrom("#FFB8BEC9"),Margin=new Thickness(0,0,0,5)});body.Children.Add(name);body.Children.Add(warning);
        body.Children.Add(new TextBlock{Text="Цвет группы",Foreground=BrushFrom("#FFB8BEC9"),Margin=new Thickness(0,12,0,5)});body.Children.Add(BuildColorSwatches(value=>color=value,color));
        ShowModal("СОЗДАТЬ ГРУППУ",body,("Создать",()=>{string groupName=name.Text.Trim();if(groupName.Length==0)groupName="Новая группа";PushUndoSnapshot();_groups.Add(new EditorGroup{Name=groupName,ColorHex=color,Keys=new HashSet<string>(_selectedKeys,StringComparer.Ordinal)});RenderSelection();ScheduleProfileSave();CloseModal();ShowGroupsManager();}),("Отмена",CloseModal));
    }

    private void ShowInlineGroupEditor(EditorGroup group)
    {
        if(_groupManagerContent is null)return;
        ResetGroupRemapping(restoreGroupSelection:true);
        if(_groupSettingsPanel is not null)_groupManagerContent.Children.Remove(_groupSettingsPanel);
        StackPanel body=new(){Margin=new Thickness(10)};
        body.Children.Add(new TextBlock{Text="НАСТРОЙКИ ГРУППЫ",Tag="Heading",Foreground=BrushFrom("#FFE3E5EA"),Margin=new Thickness(0,0,0,8)});
        TextBlock warning=new(){Text="Название группы не должно превышать 30 символов.",Foreground=BrushFrom("#FFFF6666"),FontSize=10,TextAlignment=TextAlignment.Center,Visibility=Visibility.Collapsed,Margin=new Thickness(0,5,0,0)};
        TextBox name=CreatePlaceholderTextBox("Новая группа",group.Name);name.MaxLength=30;name.TextChanged+=(_,_)=>warning.Visibility=name.Text.Length>=30?Visibility.Visible:Visibility.Collapsed;
        string color=group.ColorHex;
        body.Children.Add(new TextBlock{Text="Название",Foreground=BrushFrom("#FFB8BEC9"),Margin=new Thickness(0,0,0,5)});body.Children.Add(name);body.Children.Add(warning);
        body.Children.Add(new TextBlock{Text="Цвет",Foreground=BrushFrom("#FFB8BEC9"),Margin=new Thickness(0,9,0,4)});body.Children.Add(BuildColorSwatches(value=>color=value,color));
        body.Children.Add(new TextBlock{Text="Клавиши группы выделены на схеме. Щёлкайте по клавишам, чтобы изменить состав.",Foreground=BrushFrom("#FF9DA4B2"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,9,0,0)});
        StackPanel actions=new(){Orientation=Orientation.Horizontal,Margin=new Thickness(0,10,0,0)};
        Button save=CreatePanelButton("Сохранить");save.Width=120;
        save.Click+=(_,_)=>{if(string.IsNullOrWhiteSpace(name.Text))name.Text="Новая группа";if(_selectedKeys.Count==0){ToolStatusText.Text="Выберите хотя бы одну клавишу.";return;}PushUndoSnapshot();group.Name=name.Text.Trim();group.ColorHex=color;group.Keys=new HashSet<string>(_selectedKeys,StringComparer.Ordinal);RenderSelection();ScheduleProfileSave();ShowGroupsManager();};
        Button cancel=CreatePanelButton("Отмена");cancel.Width=90;cancel.Margin=new Thickness(8,0,0,0);cancel.Click+=(_,_)=>ShowGroupsManager();
        actions.Children.Add(save);actions.Children.Add(cancel);body.Children.Add(actions);
        _groupSettingsPanel=new Border{Background=BrushFrom("#FF292D37"),BorderBrush=BrushFrom("#FF3A3F4A"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Margin=new Thickness(0,7,0,0),ClipToBounds=true,Child=body};
        _groupManagerContent.Children.Add(_groupSettingsPanel);
        _isGroupRemapping=true;_remappingGroup=group;_selectedKeys.Clear();_selectedKeys.UnionWith(group.Keys);RenderSelection();
        _groupSettingsPanel.Measure(new Size(Math.Max(200,_groupManagerContent.ActualWidth),double.PositiveInfinity));
        double h=_groupSettingsPanel.DesiredSize.Height;_groupSettingsPanel.Height=0;
        _groupSettingsPanel.BeginAnimation(FrameworkElement.HeightProperty,new System.Windows.Media.Animation.DoubleAnimation(0,h,TimeSpan.FromMilliseconds(180)){EasingFunction=new System.Windows.Media.Animation.CubicEase{EasingMode=System.Windows.Media.Animation.EasingMode.EaseOut}});
    }

    private static TextBox CreateRoundedTextBox(string text="")
    {
        TextBox box=new(){Text=text,Height=32,Padding=new Thickness(9,0,9,0),FontSize=12,Background=BrushFrom("#FF191B22"),Foreground=BrushFrom("#FFE3E5EA"),BorderThickness=new Thickness(0),CaretBrush=BrushFrom("#FFE3E5EA"),VerticalContentAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.NoWrap,AcceptsReturn=false,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,HorizontalScrollBarVisibility=ScrollBarVisibility.Hidden};
        ControlTemplate template=new(typeof(TextBox));FrameworkElementFactory border=new(typeof(Border));
        border.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(7));
        FrameworkElementFactory host=new(typeof(ScrollViewer));host.Name="PART_ContentHost";host.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty,ScrollBarVisibility.Disabled);host.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty,ScrollBarVisibility.Hidden);host.SetValue(FrameworkElement.MarginProperty,new Thickness(0));border.AppendChild(host);template.VisualTree=border;box.Template=template;return box;
    }

    private static TextBox CreatePlaceholderTextBox(string placeholder,string value="")
    {
        TextBox box=CreateRoundedTextBox(value);box.Tag=placeholder;
        ControlTemplate template=new(typeof(TextBox));FrameworkElementFactory grid=new(typeof(Grid));
        FrameworkElementFactory bg=new(typeof(Border));bg.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));bg.SetValue(Border.CornerRadiusProperty,new CornerRadius(7));grid.AppendChild(bg);
        FrameworkElementFactory host=new(typeof(ScrollViewer));host.Name="PART_ContentHost";host.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty,ScrollBarVisibility.Disabled);host.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty,ScrollBarVisibility.Hidden);host.SetValue(FrameworkElement.MarginProperty,new Thickness(0));grid.AppendChild(host);
        FrameworkElementFactory hint=new(typeof(TextBlock));hint.Name="Watermark";hint.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding("Tag"){RelativeSource=System.Windows.Data.RelativeSource.TemplatedParent});hint.SetValue(TextBlock.ForegroundProperty,BrushFrom("#FF747C8B"));hint.SetValue(FrameworkElement.MarginProperty,new Thickness(9,0,0,0));hint.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);hint.SetValue(UIElement.IsHitTestVisibleProperty,false);hint.SetValue(UIElement.VisibilityProperty,Visibility.Collapsed);grid.AppendChild(hint);
        template.VisualTree=grid;Trigger empty=new(){Property=TextBox.TextProperty,Value=""};empty.Setters.Add(new Setter(UIElement.VisibilityProperty,Visibility.Visible,"Watermark"));template.Triggers.Add(empty);box.Template=template;return box;
    }

    private static ComboBox CreateDarkComboBox(IEnumerable<string> values,string selected)
    {
        ComboBox combo=new(){ItemsSource=values,SelectedItem=selected,Height=32,Foreground=BrushFrom("#FFE3E5EA"),Background=BrushFrom("#FF292D37"),BorderThickness=new Thickness(0),Padding=new Thickness(9,4,9,4),IsTextSearchEnabled=false};
        Style itemStyle=new(typeof(ComboBoxItem));
        itemStyle.Setters.Add(new Setter(Control.ForegroundProperty,BrushFrom("#FFE3E5EA")));itemStyle.Setters.Add(new Setter(Control.BackgroundProperty,BrushFrom("#FF292D37")));itemStyle.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(9,7,9,7)));itemStyle.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(0)));
        Trigger hover=new(){Property=ComboBoxItem.IsHighlightedProperty,Value=true};hover.Setters.Add(new Setter(Control.BackgroundProperty,BrushFrom("#FF3A414D")));itemStyle.Triggers.Add(hover);combo.ItemContainerStyle=itemStyle;
        ControlTemplate template=new(typeof(ComboBox));FrameworkElementFactory root=new(typeof(Grid));
        FrameworkElementFactory surface=new(typeof(Border));surface.SetValue(Border.BackgroundProperty,BrushFrom("#FF292D37"));surface.SetValue(Border.CornerRadiusProperty,new CornerRadius(7));root.AppendChild(surface);
        FrameworkElementFactory selectedPresenter=new(typeof(ContentPresenter));selectedPresenter.SetValue(ContentPresenter.MarginProperty,new Thickness(9,0,28,0));selectedPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty,VerticalAlignment.Center);selectedPresenter.SetBinding(ContentPresenter.ContentProperty,new System.Windows.Data.Binding("SelectionBoxItem"){RelativeSource=System.Windows.Data.RelativeSource.TemplatedParent});root.AppendChild(selectedPresenter);
        FrameworkElementFactory arrow=new(typeof(TextBlock));arrow.SetValue(TextBlock.TextProperty,"⌄");arrow.SetValue(TextBlock.ForegroundProperty,BrushFrom("#FF9DA4B2"));arrow.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Right);arrow.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);arrow.SetValue(FrameworkElement.MarginProperty,new Thickness(0,0,9,0));root.AppendChild(arrow);
        FrameworkElementFactory toggle=new(typeof(ToggleButton));toggle.Name="PART_ToggleButton";toggle.SetValue(Control.BackgroundProperty,Brushes.Transparent);toggle.SetValue(Control.BorderThicknessProperty,new Thickness(0));toggle.SetBinding(ToggleButton.IsCheckedProperty,new System.Windows.Data.Binding("IsDropDownOpen"){RelativeSource=System.Windows.Data.RelativeSource.TemplatedParent,Mode=System.Windows.Data.BindingMode.TwoWay});root.AppendChild(toggle);
        FrameworkElementFactory popup=new(typeof(Popup));popup.Name="PART_Popup";popup.SetValue(Popup.PlacementProperty,PlacementMode.Bottom);popup.SetValue(Popup.AllowsTransparencyProperty,true);popup.SetBinding(Popup.IsOpenProperty,new System.Windows.Data.Binding("IsDropDownOpen"){RelativeSource=System.Windows.Data.RelativeSource.TemplatedParent});
        FrameworkElementFactory popupBg=new(typeof(Border));popupBg.SetValue(Border.BackgroundProperty,BrushFrom("#FF292D37"));popupBg.SetValue(Border.CornerRadiusProperty,new CornerRadius(7));popupBg.SetValue(Border.PaddingProperty,new Thickness(3));FrameworkElementFactory items=new(typeof(ItemsPresenter));popupBg.AppendChild(items);popup.AppendChild(popupBg);root.AppendChild(popup);template.VisualTree=root;combo.Template=template;
        combo.PreviewMouseWheel+=(_,e)=>{if(!combo.IsDropDownOpen)e.Handled=true;};
        return combo;
    }

    private void ApplyHexColorInput()
    {
        if(_colorHexText is null)return;
        string raw=new(_colorHexText.Text.Where(Uri.IsHexDigit).ToArray());
        if(raw.Length==3)raw=string.Concat(raw.Select(ch=>new string(ch,2)));
        if(raw.Length==6&&byte.TryParse(raw.AsSpan(0,2),System.Globalization.NumberStyles.HexNumber,null,out byte red)&&byte.TryParse(raw.AsSpan(2,2),System.Globalization.NumberStyles.HexNumber,null,out byte green)&&byte.TryParse(raw.AsSpan(4,2),System.Globalization.NumberStyles.HexNumber,null,out byte blue))
        {SetCurrentColor(Color.FromRgb(red,green,blue));_colorHexText.Text=$"#{red:X2}{green:X2}{blue:X2}";return;}
        _colorHexText.Text=$"#{_currentColor.R:X2}{_currentColor.G:X2}{_currentColor.B:X2}";ToolStatusText.Text="Введите HEX-цвет: 3 или 6 символов.";
    }

    private static StackPanel BuildColorSwatches(Action<string> choose, string selected)
    {
        string[] palette = { "#FFE85D5D", "#FFFFA94D", "#FFFFD166", "#FF65C18C", "#FF4DB6AC", "#FF5B9BFF", "#FF9B7BFF", "#FFE879B9", "#FFB8BEC9" };
        StackPanel row = new() { Orientation = Orientation.Horizontal };
        foreach (string color in palette)
        {
            Button swatch = new() { Width = 25, Height = 25, Margin = new Thickness(0,0,7,0), Padding = new Thickness(0), Background = BrushFrom(color), BorderBrush = BrushFrom(color == selected ? "#FFFFFFFF" : "#FF4A4F5B"), BorderThickness = new Thickness(color == selected ? 2 : 1), Cursor = Cursors.Hand, ToolTip = null };
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
        string[] files = GetOrderedProfileFiles();
        foreach (string file in files)
        {
            try
            {
                EditorProfileData data = ReadProfileFile(file);
                bool active = string.Equals(IOPath.GetFullPath(file), IOPath.GetFullPath(ProfilePath(_activeProfileName)), StringComparison.OrdinalIgnoreCase);
                string profileOrderName = IOPath.GetFileNameWithoutExtension(file);
                Grid row = new() { Height=40, Margin=new Thickness(0,2,0,2), Tag=profileOrderName };
                Button choose = new()
                {
                    Background=active ? BrushFrom("#FF3A414F") : Brushes.Transparent, BorderThickness=new Thickness(0),
                    HorizontalContentAlignment=HorizontalAlignment.Left, Padding=new Thickness(28,0,42,0),
                    Cursor=Cursors.Hand, Template=RoundedToolbarButtonTemplate(18),
                    HorizontalAlignment=HorizontalAlignment.Stretch
                };
                StackPanel label = new() { Orientation=Orientation.Horizontal, VerticalAlignment=VerticalAlignment.Center };
                label.Children.Add(new Ellipse { Width=10, Height=10, Fill=BrushFrom(data.ColorHex), Margin=new Thickness(0,0,9,0) });
                label.Children.Add(new TextBlock { Text=data.Name, Foreground=BrushFrom("#FFE3E5EA"), VerticalAlignment=VerticalAlignment.Center });
                choose.Content=label;
                choose.Click += (_,_) =>
                {
                    if (!SaveCurrentProfile()) return;
                    LoadProfile(file);
                    _profilesPopup!.IsOpen = false;
                    _profilesPopup = null;
                };
                row.Children.Add(choose);
                TextBlock dragHandle = new() { Text="⠿", FontFamily=new FontFamily("Segoe UI Symbol"), FontSize=17, Foreground=BrushFrom("#FF818A99"), Background=Brushes.Transparent, Width=20, TextAlignment=TextAlignment.Center, VerticalAlignment=VerticalAlignment.Center, HorizontalAlignment=HorizontalAlignment.Left, Margin=new Thickness(5,0,0,0), Cursor=Cursors.SizeAll, ToolTip="Перетащить для изменения порядка запуска" };
                Panel.SetZIndex(dragHandle,3); row.Children.Add(dragHandle);
                AttachProfileDragDrop(body,row,dragHandle);
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
                        ("Удалить",()=>{if(active){string? next=GetOrderedProfileFiles().FirstOrDefault(path=>!string.Equals(IOPath.GetFullPath(path),IOPath.GetFullPath(file),StringComparison.OrdinalIgnoreCase));if(next is not null)LoadProfile(next);}File.Delete(file);_profileOrder.RemoveAll(name=>string.Equals(name,profileOrderName,StringComparison.OrdinalIgnoreCase));SaveProfileOrder();CloseModal();ToggleProfilesFlyout();}),
                        ("Отмена",()=>{CloseModal();ToggleProfilesFlyout();}));
                };
                menu.Items.Add(configure); menu.Items.Add(remove); more.Click += (_,_) => menu.IsOpen=true;
                body.Children.Add(row);
            }
            catch(Exception ex){Debug.WriteLine("Profile list: "+ex.Message);}
        }
        Button create=CreatePanelButton("＋");create.FontSize=24;create.Height=42;create.Margin=new Thickness(0,4,0,4);create.ToolTip="Создать новый профиль";create.Template=RoundedToolbarButtonTemplate(10);create.Click+=(_,_)=>{_profilesPopup!.IsOpen=false;_profilesPopup=null;ShowProfileEditor(null,null);};body.Children.Add(create);
        body.Children.Add(new TextBlock{Text="Перетащите профиль за значок, чтобы изменить порядок. Верхний профиль загружается первым при запуске.",Foreground=BrushFrom("#FF89919F"),FontSize=9,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,5,0,7)});
        body.Children.Add(new Border{Height=1,Background=BrushFrom("#FF414652"),Margin=new Thickness(0,8,0,8)});
        Button import=CreatePanelButton("Импорт");import.Height=34;import.Template=RoundedToolbarButtonTemplate(10);import.Margin=new Thickness(0,0,0,2);import.Click+=(_,_)=>ImportProfileFromFile();body.Children.Add(import);
        Button export=CreatePanelButton("Экспорт");export.Height=34;export.Template=RoundedToolbarButtonTemplate(10);export.Margin=new Thickness(0,0,0,2);export.Click+=(_,_)=>ExportActiveProfile();body.Children.Add(export);
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
            EditorProfileData imported = ReadProfileFile(dialog.FileName, requireName: true);
            string target = ProfilePath(imported.Name);
            if (File.Exists(target)) { ToolStatusText.Text = "Профиль с таким названием уже существует."; return; }
            WriteAllTextAtomically(target, JsonSerializer.Serialize(imported, ProfileJsonOptions));
            string importedName = IOPath.GetFileNameWithoutExtension(target);
            if (!_profileOrder.Contains(importedName, StringComparer.OrdinalIgnoreCase)) _profileOrder.Add(importedName);
            SaveProfileOrder();
            if (_profilesPopup is not null) _profilesPopup.IsOpen = false;
            _profilesPopup = null; ToggleProfilesFlyout();
        }
        catch (Exception ex) { ToolStatusText.Text = "Не удалось импортировать профиль: " + ex.Message; }
    }

    private void ExportActiveProfile()
    {
        Microsoft.Win32.SaveFileDialog dialog = new() { Filter = "JSON profile (*.json)|*.json", FileName = SafeFileName(_activeProfileName) + ".json" };
        if (dialog.ShowDialog(this) == true)
        {
            try
            {
                if (!SaveCurrentProfile())
                {
                    ToolStatusText.Text = "Не удалось сохранить профиль перед экспортом.";
                    return;
                }
                WriteAllTextAtomically(dialog.FileName, File.ReadAllText(ProfilePath(_activeProfileName)));
                ToolStatusText.Text = "Профиль экспортирован.";
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Profile export failed: " + ex.Message);
                ToolStatusText.Text = "Не удалось экспортировать профиль.";
            }
        }
    }

    private void ShowProfileEditor(EditorProfileData? existing,string? oldPath)
    {
        TextBlock warning=new(){Text="Название профиля не должно превышать 30 символов.",Foreground=BrushFrom("#FFFF6666"),FontSize=10,TextAlignment=TextAlignment.Center,Visibility=Visibility.Collapsed,Margin=new Thickness(0,5,0,0)};
        TextBox name=CreatePlaceholderTextBox("Новый профиль",existing?.Name??"");name.MaxLength=30;name.TextChanged+=(_,_)=>warning.Visibility=name.Text.Length>=30?Visibility.Visible:Visibility.Collapsed;
        _dialogColor=existing?.ColorHex??"#FF6D8CFF";StackPanel colors=BuildColorSwatches(value=>_dialogColor=value,_dialogColor);
        StackPanel body=new();body.Children.Add(new TextBlock{Text="Название профиля",Foreground=BrushFrom("#FFB8BEC9"),Margin=new Thickness(0,0,0,5)});body.Children.Add(name);body.Children.Add(warning);
        body.Children.Add(new TextBlock{Text="Цвет профиля",Foreground=BrushFrom("#FFB8BEC9"),Margin=new Thickness(0,12,0,5)});colors.HorizontalAlignment=HorizontalAlignment.Center;body.Children.Add(colors);
        ShowModal(existing is null ? "СОЗДАТЬ ПРОФИЛЬ" : "НАСТРОЙКА ПРОФИЛЯ", body,
            (existing is null ? "Создать" : "Сохранить", () =>
            {
                string profileName = name.Text.Trim();
                if (profileName.Length == 0) profileName = "Новый профиль";
                string newPath = ProfilePath(profileName);

                if (existing is null && File.Exists(newPath))
                {
                    ToolStatusText.Text = "Профиль с таким названием уже существует.";
                    return;
                }
                if (existing is not null && oldPath is not null &&
                    !string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase) && File.Exists(newPath))
                {
                    ToolStatusText.Text = "Профиль с таким названием уже существует.";
                    return;
                }

                try
                {
                    if (existing is null)
                    {
                        _profileSaveTimer.Stop();
                        if (!SaveCurrentProfile())
                        {
                            ToolStatusText.Text = "Не удалось сохранить текущий профиль.";
                            return;
                        }

                        EditorProfileData newProfile = new()
                        {
                            Name = profileName,
                            ColorHex = _dialogColor,
                            KeyColors = new Dictionary<string, string>(StringComparer.Ordinal),
                            Groups = new List<EditorGroupData>()
                        };
                        WriteAllTextAtomically(newPath, JsonSerializer.Serialize(newProfile, ProfileJsonOptions));

                        _keyColors.Clear();
                        _groups.Clear();
                        _selectedKeys.Clear();
                        foreach (string key in _keyGlows.Keys) UpdateKeyVisual(key);
                        _activeProfileName = profileName;
                        _activeProfileColor = _dialogColor;
                        _lastHardwareFrameSignature = null;
                        RenderSelection();
                        ScheduleHardwareFrame();
                        RenameProfileOrderEntry(null, newPath);
                        ShowGroupsManager();
                    }
                    else
                    {
                        bool active = oldPath is not null &&
                            string.Equals(IOPath.GetFullPath(oldPath), IOPath.GetFullPath(ProfilePath(_activeProfileName)), StringComparison.OrdinalIgnoreCase);
                        string previousActiveName = _activeProfileName;
                        string previousActiveColor = _activeProfileColor;

                        existing.Name = profileName;
                        existing.ColorHex = _dialogColor;

                        if (active)
                        {
                            _activeProfileName = profileName;
                            _activeProfileColor = _dialogColor;
                            if (!SaveCurrentProfile())
                            {
                                _activeProfileName = previousActiveName;
                                _activeProfileColor = previousActiveColor;
                                ToolStatusText.Text = "Не удалось сохранить изменения профиля.";
                                return;
                            }
                        }
                        else
                        {
                            WriteAllTextAtomically(newPath, JsonSerializer.Serialize(existing, ProfileJsonOptions));
                        }

                        if (oldPath is not null &&
                            !string.Equals(IOPath.GetFullPath(oldPath), IOPath.GetFullPath(newPath), StringComparison.OrdinalIgnoreCase) &&
                            File.Exists(oldPath))
                            File.Delete(oldPath);

                        RenameProfileOrderEntry(oldPath, newPath);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Profile create or update failed: " + ex.Message);
                    ToolStatusText.Text = "Не удалось сохранить профиль.";
                    return;
                }

                UpdateProfileButton();
                CloseModal();
                ToggleProfilesFlyout();
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
        content.SetBinding(ContentPresenter.HorizontalAlignmentProperty, new System.Windows.Data.Binding("HorizontalContentAlignment") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        content.SetBinding(ContentPresenter.VerticalAlignmentProperty, new System.Windows.Data.Binding("VerticalContentAlignment") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        content.SetBinding(FrameworkElement.MarginProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
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

            _profileButton.Content = content;
            _profileButton.ToolTip = null;
            _profileButton.Width = 196;
        }
    }

    private void ShowModal(string title,UIElement body,params (string Text,Action Action)[] actions)
    {
        ModalOverlay.Children.Clear();Grid grid=new();grid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(48)});grid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});grid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(54)});
        Border card=new(){Width=360,MaxHeight=440,Background=BrushFrom("#FF252832"),BorderBrush=BrushFrom("#FF4A505D"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),Padding=new Thickness(20),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Child=grid};
        TextBlock modalTitle = CreateHeadingText(title);
        modalTitle.Foreground = BrushFrom("#FFE5E8EF");
        modalTitle.VerticalAlignment = VerticalAlignment.Center;
        modalTitle.HorizontalAlignment = HorizontalAlignment.Center;
        modalTitle.TextAlignment = TextAlignment.Center;
        grid.Children.Add(modalTitle);
        Border bodyBorder=new(){Child=body,Margin=new Thickness(0,8,0,8)};Grid.SetRow(bodyBorder,1);grid.Children.Add(bodyBorder);
        StackPanel footer=new(){Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
        foreach(var action in actions){Button button=CreatePanelButton(action.Text);button.MinWidth=100;button.Margin=new Thickness(5,0,5,0);button.Template=RoundedToolbarButtonTemplate(10);if(string.Equals(action.Text,"Удалить",StringComparison.OrdinalIgnoreCase)){button.Background=BrushFrom("#FF8F302F");button.Foreground=Brushes.White;}button.Click+=(_,_)=>action.Action();footer.Children.Add(button);}
        Grid.SetRow(footer,2);grid.Children.Add(footer);ModalOverlay.Children.Add(card);ModalOverlay.Visibility=Visibility.Visible;
    }

    private void CloseModal()
    {
        ModalOverlay.Visibility = Visibility.Collapsed;
        ModalOverlay.Children.Clear();
        Focus();
    }

    private void ReleasePointer()
    {
        // Losing window focus cancels inline remapping so it cannot intercept later tool input.
        if (_isGroupRemapping) ShowGroupsManager();
        _isPointerDown = false;
        _isMarquee = false;
        _strokeUndoPushed = false;
        _lastPaintedKey = null;
        Mouse.Capture(null);
        if (_marquee is not null) SelectionOverlayCanvas.Children.Remove(_marquee);
        _marquee = null;
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
