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
}
