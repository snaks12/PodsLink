using System;
using System.Collections.Generic;
using System.Globalization;

namespace PodsLink;

/// <summary>Localisation EN / FR. Langue automatique (langue de Windows) ou choisie dans le menu.</summary>
public static class Loc
{
    public static string Mode { get; private set; } = "auto";   // auto | en | fr
    public static string Lang { get; private set; } = "en";     // en | fr effectif
    public static event Action? Changed;

    static readonly Dictionary<string, (string En, string Fr)> S = new()
    {
        ["left"] = ("Left", "Gauche"),
        ["right"] = ("Right", "Droite"),
        ["case"] = ("Case", "Étui"),
        ["inEar"] = ("● in ear", "● dans l'oreille"),
        ["popupNone"] = ("AirPods not detected (open the case nearby)", "AirPods non détectés (ouvre l'étui à proximité)"),
        ["subNone"] = ("No AirPods detected", "Aucun AirPods détecté"),
        ["subOne"] = ("1 AirPods nearby", "1 AirPods à proximité"),
        ["subMany"] = ("{0} AirPods nearby", "{0} AirPods à proximité"),
        ["empty"] = ("Open the case or take the earbuds out near your PC to see them here.",
                     "Ouvre l'étui ou sors les écouteurs à proximité du PC pour les voir apparaître ici."),
        ["sigStrong"] = ("strong signal", "signal fort"),
        ["sigMid"] = ("medium signal", "signal moyen"),
        ["sigWeak"] = ("weak signal", "signal faible"),
        ["nearest"] = ("closest", "le plus proche"),
        ["trayNone"] = ("PodsLink – AirPods not detected", "PodsLink – AirPods non détectés"),
        ["trayFmt"] = ("{0} · L {1} R {2} · Case {3}", "{0} · G {1} D {2} · Étui {3}"),
        ["menuShow"] = ("Show AirPods", "Afficher les AirPods"),
        ["menuPause"] = ("Auto-pause", "Pause automatique"),
        ["menuRoute"] = ("Switch audio to AirPods", "Basculer le son vers les AirPods"),
        ["menuStartup"] = ("Start with Windows", "Lancer au démarrage de Windows"),
        ["menuLang"] = ("Language", "Langue"),
        ["langAuto"] = ("Automatic", "Automatique"),
        ["menuQuit"] = ("Quit", "Quitter"),
        ["tPause"] = ("Pause when I take an earbud out", "Pause quand j'enlève une oreillette"),
        ["balloon"] = ("AirPods not connected to Windows: connect them (Settings > Bluetooth) to send audio to them.",
                       "AirPods non connectés à Windows : connecte-les (Paramètres > Bluetooth) pour y envoyer le son."),
    };

    public static string T(string key) =>
        S.TryGetValue(key, out var v) ? (Lang == "fr" ? v.Fr : v.En) : key;

    public static string T(string key, params object[] args) => string.Format(T(key), args);

    public static void Init()
    {
        Mode = Settings.GetString("Language", "auto");
        Resolve();
    }

    public static void SetMode(string mode)
    {
        Mode = mode;
        Settings.SetString("Language", mode);
        Resolve();
        Changed?.Invoke();
    }

    static void Resolve() =>
        Lang = Mode == "fr" || Mode == "en" ? Mode
             : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr" ? "fr" : "en";
}
