using System;

namespace GregModInventory;

// Detects at runtime whether gregCore is present (no hard dependency
// at runtime: type-name lookup only, no direct type access).
// With core: save persistence via GregSaveGuard sidecar. Without: purely volatile
// inventory (standalone mode).
// IMPORTANT: methods touching gregCore types must ONLY be called
// if HasCore is true (else JIT TypeLoad without DLL).
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

    // Testing only (e.g. force standalone behavior).
    public static void OverrideForTesting(bool? value)
    {
        _hasCore = value;
    }
}
