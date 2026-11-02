using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Win32;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace PodsLink;

static class Startup
{
    const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string Name = "PodsLink";

    public static bool IsEnabled
    {
        get { using var k = Registry.CurrentUser.OpenSubKey(KeyPath); return k?.GetValue(Name) != null; }
    }

    public static void Set(bool on)
    {
        using var k = Registry.CurrentUser.OpenSubKey(KeyPath, true) ?? Registry.CurrentUser.CreateSubKey(KeyPath);
        if (on) k.SetValue(Name, $"\"{Environment.ProcessPath}\"");
        else k.DeleteValue(Name, false);
    }
}

static class Settings
{
    const string Key = @"Software\PodsLink";
    public static bool Get(string n)
    {
        using var k = Registry.CurrentUser.OpenSubKey(Key);
        return (k?.GetValue(n) as int?) != 0;   // vrai par défaut
    }
    public static string GetString(string n, string def)
    {
        using var k = Registry.CurrentUser.OpenSubKey(Key);
        return k?.GetValue(n) as string ?? def;
    }
    public static void SetString(string n, string v)
    {
        using var k = Registry.CurrentUser.CreateSubKey(Key);
        k.SetValue(n, v, RegistryValueKind.String);
    }
    public static void Set(string n, bool v)
    {
        using var k = Registry.CurrentUser.CreateSubKey(Key);
        k.SetValue(n, v ? 1 : 0, RegistryValueKind.DWord);
    }
}

static class Program
{
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);

    static Drawing.Icon MakeIcon(int? pct)
    {
        if (pct is null) return Ui.TrayIcon();   // logo quand rien n'est détecté
        using var bmp = new Drawing.Bitmap(32, 32);
        using var g = Drawing.Graphics.FromImage(bmp);
        g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = Drawing.Text.TextRenderingHint.AntiAlias;
        var bg = pct is null ? Drawing.Color.Gray : pct <= 25 ? Drawing.Color.IndianRed : Drawing.Color.FromArgb(0, 120, 215);
        using (var b = new Drawing.SolidBrush(bg)) g.FillEllipse(b, 0, 0, 31, 31);
        var text = pct is null ? "–" : pct >= 100 ? "100" : pct.ToString()!;
        using var f = new Drawing.Font("Segoe UI", text.Length == 3 ? 12 : 16, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Pixel);
        var sz = g.MeasureString(text, f);
        g.DrawString(text, f, Drawing.Brushes.White, (32 - sz.Width) / 2, (32 - sz.Height) / 2);
        var h = bmp.GetHicon();
        var icon = (Drawing.Icon)Drawing.Icon.FromHandle(h).Clone();
        DestroyIcon(h);
        return icon;
    }

    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "PodsLink-single-instance", out bool created);
        if (!created) return;
        Loc.Init();

        var app = new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
        var popup = new PopupWindow();
        var list = new DevicesWindow();
        var devices = new Dictionary<ulong, PodsState>();
        bool autoPause = Settings.Get("AutoPause");
        bool autoRoute = Settings.Get("AutoRoute");
        bool routing = false;
        var lastPopup = DateTime.MinValue;

        var tray = new Forms.NotifyIcon { Icon = MakeIcon(null), Text = Loc.T("trayNone"), Visible = true };

        PodsState? Primary() => devices.Values.OrderByDescending(d => d.Rssi).FirstOrDefault();
        static string F(int? p) => PodsFmt.Pct(p);

        void UpdateTray()
        {
            var p = Primary();
            var oldIcon = tray.Icon;
            tray.Icon = MakeIcon(p?.MinBattery);
            oldIcon?.Dispose();
            tray.Text = p is null
                ? Loc.T("trayNone")
                : Loc.T("trayFmt", p.Model, F(p.Left), F(p.Right), F(p.Case)) + (devices.Count > 1 ? $" (+{devices.Count - 1})" : "");
        }

        void ShowList()
        {
            list.Render(devices.Values);
            if (list.WindowState == System.Windows.WindowState.Minimized) list.WindowState = System.Windows.WindowState.Normal;
            list.Show(); list.Activate();
        }

        // Menu
        var menu = new Forms.ContextMenuStrip();
        var itemPause = new Forms.ToolStripMenuItem() { Checked = autoPause, CheckOnClick = true };
        itemPause.CheckedChanged += (_, _) => { autoPause = itemPause.Checked; Settings.Set("AutoPause", autoPause); };
        var itemRoute = new Forms.ToolStripMenuItem() { Checked = autoRoute, CheckOnClick = true };
        itemRoute.CheckedChanged += (_, _) => { autoRoute = itemRoute.Checked; Settings.Set("AutoRoute", autoRoute); };
        var itemStartup = new Forms.ToolStripMenuItem() { Checked = Startup.IsEnabled, CheckOnClick = true };
        itemStartup.CheckedChanged += (_, _) =>
        {
            try { Startup.Set(itemStartup.Checked); }
            catch (Exception ex) { Forms.MessageBox.Show(ex.Message, "PodsLink"); }
        };
        var itemShow = menu.Items.Add("", null, (_, _) => ShowList());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(itemPause);
        menu.Items.Add(itemRoute);
        menu.Items.Add(itemStartup);
        menu.Items.Add(new Forms.ToolStripSeparator());
        var itemQuit = menu.Items.Add("", null, (_, _) =>
        {
            tray.Visible = false; tray.Dispose();
            list.AllowClose = true; app.Shutdown();
        });
        tray.ContextMenuStrip = menu;

        // Mêmes réglages que le menu, accessibles dans la fenêtre
        var cbPause = list.AddToggle("", v => itemPause.Checked = v);
        var cbRoute = list.AddToggle("", v => itemRoute.Checked = v);
        var cbStart = list.AddToggle("", v => itemStartup.Checked = v);
        list.Opening += () =>
        {
            cbPause.IsChecked = itemPause.Checked; cbRoute.IsChecked = itemRoute.Checked; cbStart.IsChecked = itemStartup.Checked;
        };

        // Langue : automatique (langue de Windows), English ou Français, appliquée à chaud
        var itemLang = new Forms.ToolStripMenuItem();
        var langAuto = new Forms.ToolStripMenuItem();
        var langEn = new Forms.ToolStripMenuItem("English");
        var langFr = new Forms.ToolStripMenuItem("Français");
        langAuto.Click += (_, _) => Loc.SetMode("auto");
        langEn.Click += (_, _) => Loc.SetMode("en");
        langFr.Click += (_, _) => Loc.SetMode("fr");
        itemLang.DropDownItems.AddRange(new Forms.ToolStripItem[] { langAuto, langEn, langFr });
        menu.Items.Insert(menu.Items.Count - 1, itemLang);

        void ApplyLanguage()
        {
            itemShow.Text = Loc.T("menuShow"); itemPause.Text = Loc.T("menuPause");
            itemRoute.Text = Loc.T("menuRoute"); itemStartup.Text = Loc.T("menuStartup");
            itemLang.Text = Loc.T("menuLang"); langAuto.Text = Loc.T("langAuto"); itemQuit.Text = Loc.T("menuQuit");
            langAuto.Checked = Loc.Mode == "auto"; langEn.Checked = Loc.Mode == "en"; langFr.Checked = Loc.Mode == "fr";
            cbPause.Content = Loc.T("tPause"); cbRoute.Content = Loc.T("menuRoute"); cbStart.Content = Loc.T("menuStartup");
            UpdateTray();
            list.Render(devices.Values);
            if (popup.IsVisible) popup.Refresh(Primary());
        }
        Loc.Changed += ApplyLanguage;
        ApplyLanguage();
        tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) ShowList(); };

        async Task RouteAsync()
        {
            if (routing) return;
            routing = true;
            try
            {
                for (int i = 0; i < 10; i++)   // laisse à Windows le temps de connecter les AirPods
                {
                    bool ok = false;
                    try { ok = await Task.Run(AudioRouter.RouteToPods); } catch { }
                    if (ok) return;
                    await Task.Delay(1000);
                }
                tray.ShowBalloonTip(4000, "PodsLink",
                    Loc.T("balloon"),
                    Forms.ToolTipIcon.Info);
            }
            finally { routing = false; }
        }

        // Les AirPods (pods + étui) émettent depuis plusieurs adresses aléatoires qui changent à chaque
        // ouverture de l'étui : on reconnaît le même appareil par modèle, batteries proches et signal.
        static bool IsSame(PodsState a, PodsState b)
        {
            static bool Close(int? x, int? y) => x is null || y is null || Math.Abs(x.Value - y.Value) <= 10;
            return a.Model == b.Model
                   && Math.Abs((b.Seen - a.Seen).TotalSeconds) < 60
                   && Math.Abs(a.Rssi - b.Rssi) <= 40
                   && Close(a.Left, b.Left) && Close(a.Right, b.Right) && Close(a.Case, b.Case);
        }

        void OnState(PodsState s)
        {
            // Fusionne toutes les entrées correspondant au même appareil (quelle que soit l'adresse)
            var group = devices.Values
                .Where(d => d.Address == s.Address || IsSame(d, s))
                .OrderByDescending(d => d.Seen).ToList();
            var old = group.FirstOrDefault();
            foreach (var d in group) devices.Remove(d.Address);
            if (old is not null && s.Case is null && old.Case is not null)   // garde le dernier niveau d'étui connu
                s = s with { Case = old.Case, CaseCharging = old.CaseCharging };
            devices[s.Address] = s;
            while (devices.Count > 10)   // garde-fou : des paquets forgés ne peuvent pas faire gonfler la liste
                devices.Remove(devices.Values.OrderBy(d => d.Seen).First().Address);
            UpdateTray();
            if (list.IsVisible) list.Render(devices.Values);

            // Popup + pause auto : uniquement pour les AirPods les plus proches
            if (Primary()?.Address != s.Address) return;

            if (old is null || (s.Seen - old.Seen).TotalSeconds > 20)
            {
                if ((DateTime.Now - lastPopup).TotalSeconds > 5)   // anti-spam de la popup
                {
                    lastPopup = DateTime.Now;
                    popup.ShowState(s);
                }
            }
            else if (popup.IsVisible) popup.Refresh(s);

            if (autoPause && old is not null)
            {
                if (old.InEarCount == 2 && s.InEarCount < 2) _ = Media.PauseAsync();
                else if (old.InEarCount < 2 && s.InEarCount == 2) _ = Media.ResumeAsync();
            }

            // Son : AirPods en sortie par défaut dès qu'une oreillette est portée, retour à l'ancienne sortie quand elles sont retirées
            if (autoRoute)
            {
                if (s.InEarCount > 0 && (old is null || old.InEarCount == 0)) _ = RouteAsync();
                else if (s.InEarCount == 0 && old is not null && old.InEarCount > 0) _ = Task.Run(AudioRouter.Restore);
            }
        }

        var scanner = new BleScanner();
        scanner.StateChanged += s => app.Dispatcher.BeginInvoke(() => OnState(s));
        scanner.Start();

        // Retire les AirPods sans signal depuis 45 s
        var watchdog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        watchdog.Tick += (_, _) =>
        {
            var stale = devices.Where(kv => (DateTime.Now - kv.Value.Seen).TotalSeconds > 45).Select(kv => kv.Key).ToList();
            if (stale.Count > 0) { foreach (var k in stale) devices.Remove(k); UpdateTray(); }
            if (list.IsVisible) list.Render(devices.Values);
            if (autoRoute) _ = Task.Run(AudioRouter.Remember);
        };
        watchdog.Start();

        app.Run();
    }
}
