using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PodsLink;

static class Ui
{
    static Stream IconStream()
    {
        var a = Assembly.GetExecutingAssembly();
        return a.GetManifestResourceStream(a.GetManifestResourceNames().First(n => n.EndsWith("icon.ico")))!;
    }
    public static System.Drawing.Icon TrayIcon() { using var s = IconStream(); return new System.Drawing.Icon(s, 32, 32); }
    public static ImageSource WindowIcon()
    {
        using var s = IconStream();
        // prend la plus grande image de l'.ico (WPF choisit sinon la plus petite → flou)
        var dec = BitmapDecoder.Create(s, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var f = dec.Frames.OrderByDescending(x => x.PixelWidth).First();
        f.Freeze(); return f;
    }

    /// <summary>Logo dessiné en vectoriel : net à toutes les tailles / densités d'écran.</summary>
    public static Viewbox Logo(double size)
    {
        var c = new Canvas { Width = 256, Height = 256 };
        var grad = new LinearGradientBrush(Color.FromRgb(0x0A, 0x84, 0xFF), Color.FromRgb(0x5E, 0x5C, 0xE6), 90);
        void Add(Shape sh, double x, double y) { Canvas.SetLeft(sh, x); Canvas.SetTop(sh, y); c.Children.Add(sh); }
        Add(new Rectangle { Width = 256, Height = 256, RadiusX = 57, RadiusY = 57, Fill = grad }, 0, 0);
        foreach (var x in new[] { 56.25, 132.25 })
            Add(new Ellipse { Width = 67.5, Height = 67.5, Fill = Brushes.White }, x, 57.5);
        foreach (var x in new[] { 96.25, 142.25 })
            Add(new Rectangle { Width = 17.5, Height = 97.5, RadiusX = 8.75, RadiusY = 8.75, Fill = Brushes.White }, x, 100);
        Add(new Ellipse { Width = 41, Height = 41, Fill = Hex("#32D74B") }, 107.5, 160);
        c.Children.Add(new Polygon
        {
            Fill = Brushes.White,
            Points = System.Windows.Media.PointCollection.Parse("130,166.25 117,182.5 126.25,182.5 123.5,195 139,178 129.25,178")
        });
        return new Viewbox { Width = size, Height = size, Child = c };
    }

    public static SolidColorBrush Hex(string h)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(h)); b.Freeze(); return b;
    }
    public static SolidColorBrush Level(int? p) =>
        p is null ? Hex("#555560") : p <= 25 ? Hex("#FF453A") : p <= 50 ? Hex("#FF9F0A") : Hex("#32D74B");

    [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);

    /// <summary>Rend le logo vectoriel à la taille exacte voulue, sans redimensionnement flou.</summary>
    static IntPtr VectorHicon(int px)
    {
        var logo = Logo(px);
        logo.Measure(new Size(px, px)); logo.Arrange(new Rect(0, 0, px, px)); logo.UpdateLayout();
        var rtb = new RenderTargetBitmap(px, px, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(logo);
        var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb));
        using var ms = new MemoryStream(); enc.Save(ms); ms.Position = 0;
        using var bmp = new System.Drawing.Bitmap(ms);
        return bmp.GetHicon();
    }

    /// <summary>Icône de barre de titre (petite) et de barre des tâches (grande) nettes à tous les zooms.</summary>
    public static void VectorTitleIcon(Window w) =>
        w.SourceInitialized += (_, _) =>
        {
            var h = new WindowInteropHelper(w).Handle;
            SendMessage(h, 0x0080, (IntPtr)0, VectorHicon(GetSystemMetrics(49)));   // ICON_SMALL / SM_CXSMICON
            SendMessage(h, 0x0080, (IntPtr)1, VectorHicon(GetSystemMetrics(11)));   // ICON_BIG   / SM_CXICON
        };

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int v, int size);
    public static void DarkTitleBar(Window w) =>
        w.SourceInitialized += (_, _) => { int v = 1; DwmSetWindowAttribute(new WindowInteropHelper(w).Handle, 20, ref v, 4); };

    /// <summary>Barre de batterie aux coins arrondis.</summary>
    public static Grid Bar(int? p)
    {
        int v = Math.Clamp(p ?? 0, 0, 100);
        var g = new Grid { Height = 8 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(v, 0.001), GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(100 - v, 0.001), GridUnitType.Star) });
        var track = new Border { Background = Hex("#34343D"), CornerRadius = new CornerRadius(4) };
        Grid.SetColumnSpan(track, 2);
        g.Children.Add(track);
        g.Children.Add(new Border { Background = Level(p), CornerRadius = new CornerRadius(4) });
        return g;
    }
}
