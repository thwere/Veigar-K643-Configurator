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

}
