using System;
using System.Linq;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace PodsLink;

/// <summary>Change la sortie audio par défaut de Windows (API non documentée IPolicyConfig).</summary>
public static class AudioRouter
{
    static string? _previousId;   // dernière sortie par défaut qui n'était pas des AirPods

    static bool IsPods(MMDevice d) => d.FriendlyName.Contains("AirPods", StringComparison.OrdinalIgnoreCase);

    static MMDevice? Default(MMDeviceEnumerator en)
    {
        try { return en.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia); } catch { return null; }
    }

    /// <summary>Mémorise la sortie actuelle si ce ne sont pas les AirPods.</summary>
    public static void Remember()
    {
        try
        {
            using var en = new MMDeviceEnumerator();
            var cur = Default(en);
            if (cur != null && !IsPods(cur)) _previousId = cur.ID;
        }
        catch { }
    }

    /// <summary>Retourne false si aucune sortie AirPods active (pas encore connectés à Windows).</summary>
    public static bool RouteToPods()
    {
        using var en = new MMDeviceEnumerator();
        var pods = en.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).FirstOrDefault(IsPods);
        if (pods is null) return false;

        var cur = Default(en);
        if (cur != null && cur.ID != pods.ID)
        {
            if (!IsPods(cur)) _previousId = cur.ID;
            SetDefault(pods.ID);
        }
        return true;
    }

    /// <summary>Remet l'ancienne sortie, seulement si les AirPods sont encore la sortie par défaut.</summary>
    public static void Restore()
    {
        try
        {
            if (_previousId is null) return;
            using var en = new MMDeviceEnumerator();
            var cur = Default(en);
            if (cur is null || !IsPods(cur)) return;   // l'utilisateur a déjà changé : on ne touche à rien
            var prev = en.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).FirstOrDefault(d => d.ID == _previousId);
            if (prev != null) SetDefault(prev.ID);
        }
        catch { }
    }

    static void SetDefault(string id)
    {
        var cfg = (IPolicyConfig)new PolicyConfigClient();
        try { for (int role = 0; role < 3; role++) cfg.SetDefaultEndpoint(id, role); }
        finally { Marshal.ReleaseComObject(cfg); }
    }

    [ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
    class PolicyConfigClient { }

    [ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr fmt);
        [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string name, bool def, IntPtr fmt);
        [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string name);
        [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr endpointFmt, IntPtr mixFmt);
        [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string name, bool def, IntPtr defPeriod, IntPtr minPeriod);
        [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr period);
        [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr mode);
        [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr mode);
        [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string name, bool fx, IntPtr key, IntPtr pv);
        [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string name, bool fx, IntPtr key, IntPtr pv);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string name, int role);
        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string name, bool visible);
    }
}
