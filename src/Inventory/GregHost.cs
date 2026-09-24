using System;

namespace GregModInventory;

// Erkennt zur Laufzeit, ob gregCore vorhanden ist (ohne harte Abhaengigkeit
// zur Laufzeit: reiner Typname-Lookup, kein direkter Typzugriff).
// Mit Core: Save-Persistenz via GregSaveGuard-Sidecar. Ohne: rein fluechtiges
// Inventar (Standalone-Modus).
// WICHTIG: Methoden, die gregCore-Typen beruehren, duerfen NUR aufgerufen
// werden, wenn HasCore true ist (sonst JIT-TypeLoad bei fehlender DLL).
public static class GregHost
{
    private const string ProbeType = "gregCore.UI.GregNotificationManager, gregCore";
    private static bool? _hasCore;

    public static bool HasCore
    {
        get
        {
            if (_hasCore == null)
            {
                try { _hasCore = Type.GetType(ProbeType) != null; }
                catch { _hasCore = false; }
            }
            return _hasCore.Value;
        }
    }

    // Nur zu Testzwecken (z.B. Standalone-Verhalten erzwingen).
    public static void OverrideForTesting(bool? value)
    {
        _hasCore = value;
    }
}
