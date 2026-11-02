using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PodsLink;

public sealed class DevicesWindow : Window
{
    readonly StackPanel _list = new() { Margin = new Thickness(18, 4, 18, 8) };
    readonly StackPanel _footer = new() { Margin = new Thickness(18, 10, 18, 12) };
    readonly TextBlock _subtitle = new() { Foreground = Ui.Hex("#8E8E98"), FontSize = 12 };
    public bool AllowClose { get; set; }
    public event Action? Opening;

    public DevicesWindow()
    {
        Title = "PodsLink · v1.2"; Icon = Ui.WindowIcon();
        Width = 420; Height = 620; MinWidth = 360; MinHeight = 440;
        Background = Ui.Hex("#17171C"); FontFamily = new FontFamily("Segoe UI");
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Ui.DarkTitleBar(this); Ui.VectorTitleIcon(this);

        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(18, 16, 18, 8) };
        header.Children.Add(Ui.Logo(44));
        var titles = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(new TextBlock { Text = "PodsLink", Foreground = Brushes.White, FontSize = 20, FontWeight = FontWeights.SemiBold });
        titles.Children.Add(_subtitle);
        header.Children.Add(titles);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _list };
        var footer = new Border { BorderBrush = Ui.Hex("#2A2A33"), BorderThickness = new Thickness(0, 1, 0, 0), Child = _footer };
        Grid.SetRow(scroll, 1); Grid.SetRow(footer, 2);
        root.Children.Add(header); root.Children.Add(scroll); root.Children.Add(footer);
        Content = root;

        IsVisibleChanged += (_, _) => { if (IsVisible) Opening?.Invoke(); };
        Closing += (_, e) => { if (!AllowClose) { e.Cancel = true; Hide(); } };
    }

    public CheckBox AddToggle(string text, Action<bool> onChange)
    {
        var cb = new CheckBox { Content = text, Foreground = Ui.Hex("#C8C8D0"), FontSize = 13, Margin = new Thickness(0, 4, 0, 4) };
        cb.Checked += (_, _) => onChange(true);
        cb.Unchecked += (_, _) => onChange(false);
        _footer.Children.Add(cb);
        return cb;
    }

    public void Render(IEnumerable<PodsState> devices)
    {
        var items = devices.OrderByDescending(d => d.Rssi).ToList();
        _subtitle.Text = items.Count == 0 ? Loc.T("subNone")
            : items.Count == 1 ? Loc.T("subOne") : Loc.T("subMany", items.Count);
        _list.Children.Clear();
        if (items.Count == 0)
        {
            _list.Children.Add(new TextBlock
            {
                Text = Loc.T("empty"),
                Foreground = Ui.Hex("#8E8E98"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 20, 2, 0)
            });
            return;
        }
        for (int i = 0; i < items.Count; i++) _list.Children.Add(Card(items[i], i == 0));
    }

    static string Signal(int rssi) => rssi > -60 ? Loc.T("sigStrong") : rssi > -72 ? Loc.T("sigMid") : Loc.T("sigWeak");

    static Border Card(PodsState s, bool nearest)
    {
        var sp = new StackPanel();
        var head = new DockPanel();
        var sig = new TextBlock { Text = Signal(s.Rssi), Foreground = Ui.Hex("#8E8E98"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(sig, Dock.Right); head.Children.Add(sig);
        if (nearest)
        {
            var badge = new Border
            {
                Background = Ui.Hex("#1F3A5F"), CornerRadius = new CornerRadius(8), Padding = new Thickness(7, 2, 7, 2),
                Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = Loc.T("nearest"), Foreground = Ui.Hex("#5AA9FF"), FontSize = 11 }
            };
            DockPanel.SetDock(badge, Dock.Right); head.Children.Add(badge);
        }
        head.Children.Add(new TextBlock { Text = s.Model, Foreground = Brushes.White, FontSize = 17, FontWeight = FontWeights.SemiBold });
        sp.Children.Add(head);
        sp.Children.Add(Row(Loc.T("left"), s.Left, s.LeftCharging, s.LeftInEar));
        sp.Children.Add(Row(Loc.T("right"), s.Right, s.RightCharging, s.RightInEar));
        sp.Children.Add(Row(Loc.T("case"), s.Case, s.CaseCharging, false));
        return new Border
        {
            Background = Ui.Hex("#22222A"), BorderBrush = Ui.Hex("#2E2E38"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14), Padding = new Thickness(16, 14, 16, 16),
            Margin = new Thickness(0, 6, 0, 8), Child = sp
        };
    }

    static StackPanel Row(string label, int? p, bool charging, bool inEar)
    {
        var box = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        var line = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };
        var val = new TextBlock
        {
            Text = PodsFmt.Pct(p) + (charging && p is not null ? "  ⚡" : ""),
            Foreground = p is <= 25 ? Ui.Hex("#FF6B61") : Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = 14
        };
        DockPanel.SetDock(val, Dock.Right); line.Children.Add(val);
        var lab = new StackPanel { Orientation = Orientation.Horizontal };
        lab.Children.Add(new TextBlock { Text = label, Foreground = Ui.Hex("#B0B0BA"), FontSize = 13 });
        if (inEar) lab.Children.Add(new TextBlock { Text = "  " + Loc.T("inEar"), Foreground = Ui.Hex("#32D74B"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
        line.Children.Add(lab);
        box.Children.Add(line); box.Children.Add(Ui.Bar(p));
        return box;
    }
}
