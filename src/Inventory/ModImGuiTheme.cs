using UnityEngine;

// Mod-lokaler Mirror von gregCore.UI.GregImGuiTheme (kanonisch dort). (Himmelblau/Atollblau, modern minimal).
// Deliberately decoupled (no gregCore needed): dummy-safe factories
// (parameterlose Ctors + Feldzuweisung). Bei Theme-Aenderungen hier nachziehen.
internal static class ModImGuiTheme
{
    internal static readonly Color PanelBg = new Color(0.08f, 0.10f, 0.13f, 0.98f);
    internal static readonly Color TitleBg = new Color(0.06f, 0.08f, 0.11f, 0.90f);
    internal static readonly Color Border = new Color(0.14f, 0.17f, 0.22f, 0.80f);
    internal static readonly Color Text = new Color(0.92f, 0.94f, 0.96f);
    internal static readonly Color Dim = new Color(0.62f, 0.66f, 0.72f);
    internal static readonly Color Atoll = new Color(0.04f, 0.64f, 0.75f);
    internal static readonly Color AtollText = new Color(0.02f, 0.07f, 0.12f);
    internal static readonly Color Sky = new Color(0.53f, 0.81f, 0.92f);
    internal static readonly Color Gold = new Color(1.00f, 0.85f, 0.28f);
    internal static readonly Color Danger = new Color(1.00f, 0.32f, 0.32f);
    internal static readonly Color Success = new Color(0.30f, 0.70f, 0.35f);

    internal static GUIStyle Box(Texture2D bg = null)
    {
        var s = new GUIStyle();
        s.normal.background = bg;
        s.normal.textColor = Text;
        s.border = Inset(8);
        s.padding = Inset(12);
        return s;
    }

    internal static GUIStyle Label(int size = 13, bool bold = false, Color? color = null)
    {
        var s = new GUIStyle();
        s.fontSize = size;
        s.normal.textColor = color ?? Text;
        try { s.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; } catch { }
        return s;
    }

    internal static GUIStyle Button(bool primary = false)
    {
        var s = new GUIStyle();
        s.fontSize = 13;
        try { s.fontStyle = FontStyle.Bold; } catch { }
        s.alignment = TextAnchor.MiddleCenter;
        s.border = Inset(6);
        s.padding = Inset(8);
        if (primary)
        {
            s.normal.background = Tex(Atoll);
            s.normal.textColor = AtollText;
            s.hover.background = Tex(Sky);
            s.hover.textColor = AtollText;
        }
        else
        {
            s.normal.background = Tex(new Color(0.11f, 0.13f, 0.17f));
            s.normal.textColor = Text;
            s.hover.background = Tex(new Color(0.16f, 0.19f, 0.25f));
            s.hover.textColor = Color.white;
        }
        return s;
    }

    internal static RectOffset Inset(int v)
    {
        var r = new RectOffset();
        r.left = v; r.right = v; r.top = v; r.bottom = v;
        return r;
    }

    private static readonly System.Collections.Generic.Dictionary<Color, Texture2D> _texCache =
        new System.Collections.Generic.Dictionary<Color, Texture2D>();

    internal static Texture2D Tex(Color c)
    {
        Texture2D t;
        if (_texCache.TryGetValue(c, out t) && t != null) return t;
        t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        try
        {
            t.SetPixel(0, 0, c);
            t.Apply();
        }
        catch { }
        try { UnityEngine.Object.DontDestroyOnLoad(t); } catch { }
        _texCache[c] = t;
        return t;
    }
}
