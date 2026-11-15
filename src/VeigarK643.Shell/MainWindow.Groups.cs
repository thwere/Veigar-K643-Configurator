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
}
