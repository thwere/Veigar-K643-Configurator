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
