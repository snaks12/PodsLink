using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace PodsLink;

public sealed class PopupWindow : Window
{
    readonly TextBlock _title = new()
    {
        Foreground = Brushes.White, FontSize = 15, FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 0, 0, 10)
    };
    readonly TextBlock _l, _r, _c, _ll, _lr, _lc;
    readonly DispatcherTimer _hide = new() { Interval = TimeSpan.FromSeconds(6) };

    public PopupWindow()
    {
        WindowStyle = WindowStyle.None; AllowsTransparency = true;
        Background = Brushes.Transparent; Topmost = true; ShowInTaskbar = false;
        ShowActivated = false; ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;

        var grid = new Grid();
        (_l, _ll) = Col(grid, 0, Loc.T("left"));
        (_r, _lr) = Col(grid, 1, Loc.T("right"));
        (_c, _lc) = Col(grid, 2, Loc.T("case"));

        var panel = new StackPanel();
        panel.Children.Add(_title);
        panel.Children.Add(grid);
        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xEE, 0x1E, 0x1E, 0x24)),
            CornerRadius = new CornerRadius(14), Padding = new Thickness(20, 16, 20, 16),
            MinWidth = 360, Child = panel, Margin = new Thickness(24),
            Effect = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 4, Opacity = 0.5 }
        };
        _hide.Tick += (_, _) => { _hide.Stop(); Hide(); };
    }

    static (TextBlock value, TextBlock label) Col(Grid g, int i, string name)
    {
        g.ColumnDefinitions.Add(new ColumnDefinition());
        var v = new TextBlock { Foreground = Brushes.White, FontSize = 20, FontWeight = FontWeights.Bold,
                                HorizontalAlignment = HorizontalAlignment.Center };
        var l = new TextBlock { Foreground = Brushes.Gray, FontSize = 12, Text = name,
                                HorizontalAlignment = HorizontalAlignment.Center };
        var sp = new StackPanel();
        sp.Children.Add(v); sp.Children.Add(l);
        Grid.SetColumn(sp, i); g.Children.Add(sp);
        return (v, l);
    }

    static string Fmt(int? p, bool charging) => PodsFmt.Pct(p) + (charging && p is not null ? " ⚡" : "");

    static Brush V(int? p) => p is <= 25 ? Ui.Hex("#FF6B61") : Brushes.White;

    public void Refresh(PodsState? s)
    {
        if (s is null)
        {
            _title.Text = Loc.T("popupNone");
            _l.Text = _r.Text = _c.Text = "–";
            _ll.Text = Loc.T("left"); _lr.Text = Loc.T("right"); _lc.Text = Loc.T("case");
            return;
        }
        _title.Text = "🎧 " + s.Model;
        _l.Text = Fmt(s.Left, s.LeftCharging);
        _r.Text = Fmt(s.Right, s.RightCharging);
        _c.Text = Fmt(s.Case, s.CaseCharging);
        _l.Foreground = V(s.Left); _r.Foreground = V(s.Right); _c.Foreground = V(s.Case);
        _lc.Text = Loc.T("case");
        _ll.Text = Loc.T("left") + (s.LeftInEar ? " ●" : "");
        _lr.Text = Loc.T("right") + (s.RightInEar ? " ●" : "");
    }

    public void ShowState(PodsState? s)
    {
        Refresh(s);
        Show(); UpdateLayout();
        var wa = SystemParameters.WorkArea;
        Left = wa.Right - ActualWidth;
        Top = wa.Bottom - ActualHeight;
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
        _hide.Stop(); _hide.Start();
    }
}
