using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VeigarK643.LayoutEditor;

public partial class MainWindow : Window
{
    private const double ArtworkWidth = 599;
    private const double ArtworkHeight = 222;
    private readonly ObservableCollection<KeyBindingConfig> _keys = [];
    private readonly List<Rectangle> _visuals = [];
    private KeyBindingConfig? _selectedKey;
    private KeyArea? _selectedArea;
    private Rectangle? _draft;
    private Point _drawStart;
    private bool _captureNextKey;

    public MainWindow()
    {
        InitializeComponent();
        KeysList.ItemsSource = _keys;
        LoadStarterKeys();
        RefreshVisuals();
    }

    private void LoadStarterKeys()
    {
        foreach (string key in new[] { "Escape", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12", "A", "S", "D", "W", "Space", "LeftShift", "RightShift", "LeftCtrl", "RightCtrl", "LeftAlt", "RightAlt" })
            _keys.Add(new KeyBindingConfig { Key = key });
        KeysList.SelectedIndex = 0;
    }

    private void Capture_Click(object sender, RoutedEventArgs e)
    {
        _captureNextKey = true;
        StatusText.Text = "Ожидаю нажатие клавиши… Нажми нужную клавишу на клавиатуре.";
        CaptureButton.Content = "Ожидание…";
        Focus();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_captureNextKey)
            return;

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.None || key == Key.DeadCharProcessed)
            return;

        string name = key.ToString();
        KeyNameBox.Text = name;
        _captureNextKey = false;
        CaptureButton.Content = "Нажми клавишу…";
        SelectOrCreateKey(name);
        StatusText.Text = $"Клавиша {name} выбрана.";
        e.Handled = true;
    }

    private void AddKey_Click(object sender, RoutedEventArgs e)
    {
        string name = KeyNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusText.Text = "Укажи имя клавиши или воспользуйся захватом.";
            return;
        }
        SelectOrCreateKey(name);
    }

    private void SelectOrCreateKey(string name)
    {
        KeyBindingConfig? binding = _keys.FirstOrDefault(x => string.Equals(x.Key, name, StringComparison.OrdinalIgnoreCase));
        if (binding is null)
        {
            binding = new KeyBindingConfig { Key = name };
            _keys.Add(binding);
        }
        KeysList.SelectedItem = binding;
        _selectedKey = binding;
        RefreshAreas();
        RefreshVisuals();
    }

    private void KeysList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedKey = KeysList.SelectedItem as KeyBindingConfig;
        RefreshAreas();
        RefreshVisuals();
    }

    private void RemoveKey_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedKey is null) return;
        _keys.Remove(_selectedKey);
        _selectedKey = null;
        _selectedArea = null;
        RefreshAreas();
        RefreshVisuals();
    }

    private void RefreshAreas()
    {
        AreasList.ItemsSource = null;
        AreasList.ItemsSource = _selectedKey?.Areas;
        _selectedArea = null;
        ClearAreaFields();
    }

    private void AreasList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedArea = AreasList.SelectedItem as KeyArea;
        if (_selectedArea is null)
        {
            ClearAreaFields();
            RefreshVisuals();
            return;
        }
        XBox.Text = F(_selectedArea.X);
        YBox.Text = F(_selectedArea.Y);
        WidthBox.Text = F(_selectedArea.Width);
        HeightBox.Text = F(_selectedArea.Height);
        RefreshVisuals();
    }

    private void KeyboardCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_selectedKey is null)
        {
            StatusText.Text = "Сначала выбери клавишу слева.";
            return;
        }
        _drawStart = Clamp(e.GetPosition(KeyboardCanvas));
        _draft = new Rectangle
        {
            Stroke = Brushes.DeepSkyBlue,
            StrokeThickness = 1.5,
            Fill = new SolidColorBrush(Color.FromArgb(55, 60, 145, 255)),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(_draft, _drawStart.X);
        Canvas.SetTop(_draft, _drawStart.Y);
        KeyboardCanvas.Children.Add(_draft);
        KeyboardCanvas.CaptureMouse();
    }

    private void KeyboardCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draft is null || e.LeftButton != MouseButtonState.Pressed) return;
        Point current = Clamp(e.GetPosition(KeyboardCanvas));
        Canvas.SetLeft(_draft, Math.Min(_drawStart.X, current.X));
        Canvas.SetTop(_draft, Math.Min(_drawStart.Y, current.Y));
        _draft.Width = Math.Abs(current.X - _drawStart.X);
        _draft.Height = Math.Abs(current.Y - _drawStart.Y);
    }

    private void KeyboardCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draft is null) return;
        Point current = Clamp(e.GetPosition(KeyboardCanvas));
        double x = Math.Min(_drawStart.X, current.X);
        double y = Math.Min(_drawStart.Y, current.Y);
        double width = Math.Abs(current.X - _drawStart.X);
        double height = Math.Abs(current.Y - _drawStart.Y);
        KeyboardCanvas.ReleaseMouseCapture();
        KeyboardCanvas.Children.Remove(_draft);
        _draft = null;

        if (width < 2 || height < 2) return;
        var area = new KeyArea { X = x, Y = y, Width = width, Height = height };
        _selectedKey!.Areas.Add(area);
        AreasList.SelectedItem = area;
        StatusText.Text = $"Добавлена область для {_selectedKey.Key}: {F(x)}, {F(y)}, {F(width)} × {F(height)}.";
        RefreshVisuals();
    }

    private void ApplyArea_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedKey is null || _selectedArea is null) return;
        if (!TryNumber(XBox.Text, out double x) || !TryNumber(YBox.Text, out double y) ||
            !TryNumber(WidthBox.Text, out double width) || !TryNumber(HeightBox.Text, out double height) ||
            width <= 0 || height <= 0)
        {
            StatusText.Text = "Проверь координаты: нужны числа, ширина и высота больше нуля.";
            return;
        }
        _selectedArea.X = Math.Clamp(x, 0, ArtworkWidth);
        _selectedArea.Y = Math.Clamp(y, 0, ArtworkHeight);
        _selectedArea.Width = Math.Clamp(width, 1, ArtworkWidth - _selectedArea.X);
        _selectedArea.Height = Math.Clamp(height, 1, ArtworkHeight - _selectedArea.Y);
        RefreshAreas();
        RefreshVisuals();
        StatusText.Text = "Координаты области обновлены.";
    }

    private void RemoveArea_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedKey is null || _selectedArea is null) return;
        _selectedKey.Areas.Remove(_selectedArea);
        _selectedArea = null;
        RefreshAreas();
        RefreshVisuals();
    }

    private void RefreshVisuals()
    {
        foreach (Rectangle rectangle in _visuals) KeyboardCanvas.Children.Remove(rectangle);
        _visuals.Clear();
        foreach (KeyBindingConfig key in _keys)
        {
            foreach (KeyArea area in key.Areas)
            {
                bool selected = ReferenceEquals(key, _selectedKey) && ReferenceEquals(area, _selectedArea);
                var rectangle = new Rectangle
                {
                    Width = area.Width,
                    Height = area.Height,
                    Stroke = selected ? Brushes.White : Brushes.DeepSkyBlue,
                    StrokeThickness = selected ? 2.2 : 1.2,
                    Fill = new SolidColorBrush(Color.FromArgb(selected ? (byte)105 : (byte)65, 45, 125, 240)),
                    IsHitTestVisible = false,
                    ToolTip = $"{key.Key}: X={F(area.X)}, Y={F(area.Y)}, W={F(area.Width)}, H={F(area.Height)}"
                };
                Canvas.SetLeft(rectangle, area.X);
                Canvas.SetTop(rectangle, area.Y);
                KeyboardCanvas.Children.Add(rectangle);
                _visuals.Add(rectangle);
            }
        }
        KeysList.Items.Refresh();
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var config = new EditorConfig
        {
            Registration = new RegistrationConfig
            {
                Enabled = RegistrationEnabledCheck.IsChecked == true,
                KeyDown = KeyDownCheck.IsChecked == true,
                KeyUp = KeyUpCheck.IsChecked == true,
                IgnoreAutoRepeat = IgnoreRepeatCheck.IsChecked == true,
                Scope = "foreground-window"
            },
            Image = new ImageConfig { Width = (int)ArtworkWidth, Height = (int)ArtworkHeight, File = "key.png" },
            Keys = _keys.ToList()
        };
        var dialog = new SaveFileDialog
        {
            Title = "Экспорт конфигурации клавиатуры",
            FileName = "veigar-k643-layout.json",
            Filter = "JSON config (*.json)|*.json",
            DefaultExt = ".json"
        };
        if (dialog.ShowDialog() != true) return;
        File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(config, JsonOptions()));
        StatusText.Text = $"Конфигурация сохранена: {dialog.FileName}";
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Открыть конфигурацию", Filter = "JSON config (*.json)|*.json" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            EditorConfig? config = JsonSerializer.Deserialize<EditorConfig>(File.ReadAllText(dialog.FileName), JsonOptions());
            if (config is null) throw new InvalidDataException("Файл пуст.");
            _keys.Clear();
            foreach (KeyBindingConfig key in config.Keys) _keys.Add(key);
            RegistrationEnabledCheck.IsChecked = config.Registration.Enabled;
            KeyDownCheck.IsChecked = config.Registration.KeyDown;
            KeyUpCheck.IsChecked = config.Registration.KeyUp;
            IgnoreRepeatCheck.IsChecked = config.Registration.IgnoreAutoRepeat;
            KeysList.SelectedIndex = _keys.Count > 0 ? 0 : -1;
            RefreshVisuals();
            StatusText.Text = $"Загружено клавиш: {_keys.Count}.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось прочитать JSON: {ex.Message}", "Ошибка импорта", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static bool TryNumber(string value, out double number) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) ||
        double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out number);

    private static string F(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    private static Point Clamp(Point point) => new(Math.Clamp(point.X, 0, ArtworkWidth), Math.Clamp(point.Y, 0, ArtworkHeight));

    private void ClearAreaFields()
    {
        XBox.Text = "0";
        YBox.Text = "0";
        WidthBox.Text = "30";
        HeightBox.Text = "30";
    }
}

public sealed class EditorConfig
{
    public int SchemaVersion { get; set; } = 1;
    public RegistrationConfig Registration { get; set; } = new();
    public ImageConfig Image { get; set; } = new();
    public List<KeyBindingConfig> Keys { get; set; } = [];
}

public sealed class RegistrationConfig
{
    public bool Enabled { get; set; } = true;
    public bool KeyDown { get; set; } = true;
    public bool KeyUp { get; set; } = true;
    public bool IgnoreAutoRepeat { get; set; } = true;
    public string Scope { get; set; } = "foreground-window";
}

public sealed class ImageConfig
{
    public int Width { get; set; } = 599;
    public int Height { get; set; } = 222;
    public string File { get; set; } = "key.png";
}

public sealed class KeyBindingConfig
{
    public string Key { get; set; } = "";
    public List<KeyArea> Areas { get; set; } = [];
    public override string ToString() => $"{Key}  ·  областей: {Areas.Count}";
}

public sealed class KeyArea
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public override string ToString() => $"X {X:0.#} · Y {Y:0.#} · {Width:0.#}×{Height:0.#}";
}
