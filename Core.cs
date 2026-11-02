using System;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Media.Control;
using Windows.Security.Cryptography;

namespace PodsLink;

public sealed record PodsState(ulong Address, string Model, int? Left, int? Right, int? Case,
    bool LeftCharging, bool RightCharging, bool CaseCharging,
    bool LeftInEar, bool RightInEar, int Rssi, DateTime Seen)
{
    public int InEarCount => (LeftInEar ? 1 : 0) + (RightInEar ? 1 : 0);
    public int? MinBattery
    {
        get
        {
            int? m = null;
            foreach (var v in new[] { Left, Right })
                if (v is int x && (m is null || x < m)) m = x;
            return m;
        }
    }
}

/// <summary>Décode le message "Proximity Pairing" d'Apple (company 0x004C, type 0x07).</summary>
public static class PodsParser
{
    public static PodsState? Parse(byte[] d, int rssi, ulong address)
    {
        if (d.Length < 9 || d[0] != 0x07 || d[1] != 0x19) return null;

        int model = (d[3] << 8) | d[4];
        int status = d[5], pods = d[6], flags = d[7];
        bool flip = (status & 0x20) == 0;           // gauche/droite inversés
        int hi = pods >> 4, lo = pods & 0xF;
        int l = flip ? hi : lo, r = flip ? lo : hi;

        int cs = flags >> 4;                        // bits de charge
        bool chL = (cs & (flip ? 0x02 : 0x01)) != 0;
        bool chR = (cs & (flip ? 0x01 : 0x02)) != 0;
        bool chC = (cs & 0x04) != 0;

        int ear = status & 0x0F;                    // bits "dans l'oreille"
        bool earL = (ear & (flip ? 0x08 : 0x02)) != 0;
        bool earR = (ear & (flip ? 0x02 : 0x08)) != 0;

        return new PodsState(address, ModelName(model), Pct(l), Pct(r), Pct(flags & 0xF),
            chL, chR, chC, earL, earR, rssi, DateTime.Now);
    }

    // Valeur 0-10 = palier de 10 % : on estime le milieu du palier (0xF = inconnu)
    static int? Pct(int v) => v == 10 ? 100 : v < 10 ? v * 10 + 5 : null;

    static string ModelName(int m) => m switch
    {
        0x0220 => "AirPods",
        0x0F20 => "AirPods 2",
        0x1320 => "AirPods 3",
        0x1920 => "AirPods 4",
        0x1B20 => "AirPods 4 (ANC)",
        0x0E20 => "AirPods Pro",
        0x1420 => "AirPods Pro 2",
        0x2420 => "AirPods Pro 2 (USB-C)",
        0x0A20 => "AirPods Max",
        _ => "AirPods"
    };
}

public sealed class BleScanner
{
    public event Action<PodsState>? StateChanged;
    public int MinRssi { get; set; } = -75;   // ignore les appareils trop éloignés

    readonly BluetoothLEAdvertisementWatcher _w =
        new() { ScanningMode = BluetoothLEScanningMode.Active };

    public void Start()
    {
        _w.AdvertisementFilter.Advertisement.ManufacturerData.Add(
            new BluetoothLEManufacturerData { CompanyId = 0x004C });
        _w.Received += (_, e) =>
        {
            if (e.RawSignalStrengthInDBm < MinRssi) return;
            foreach (var sec in e.Advertisement.ManufacturerData)
            {
                if (sec.CompanyId != 0x004C) continue;
                CryptographicBuffer.CopyToByteArray(sec.Data, out byte[] bytes);
                var st = PodsParser.Parse(bytes, e.RawSignalStrengthInDBm, e.BluetoothAddress);
                if (st != null) StateChanged?.Invoke(st);
            }
        };
        _w.Start();
    }
}

public static class Media
{
    static bool _pausedByUs;

    static async Task<GlobalSystemMediaTransportControlsSession?> Session()
    {
        var m = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        return m.GetCurrentSession();
    }

    public static async Task PauseAsync()
    {
        try
        {
            var s = await Session();
            if (s?.GetPlaybackInfo().PlaybackStatus ==
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            {
                await s.TryPauseAsync();
                _pausedByUs = true;
            }
        }
        catch { }
    }

    public static async Task ResumeAsync()
    {
        if (!_pausedByUs) return;
        _pausedByUs = false;
        try { var s = await Session(); if (s != null) await s.TryPlayAsync(); }
        catch { }
    }
}

public static class PodsFmt
{
    public static string Pct(int? p) => p is null ? "–" : p >= 100 ? "100%" : $"~{p}%";
}
