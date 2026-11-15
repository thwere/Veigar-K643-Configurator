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
}
