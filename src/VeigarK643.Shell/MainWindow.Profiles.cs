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
}
