using Il2Cpp;
using UnityEngine;

namespace GregModInventory
{
    public static class InventoryHud
    {
        private const float SlotSize = 50f;
        private const float SlotSpacing = 4f;
        private const float BottomMargin = 40f;

        private static readonly Color EmptyColor = new Color(0.15f, 0.15f, 0.15f, 0.6f);
        private static readonly Color StashedColor = new Color(0.25f, 0.35f, 0.25f, 0.8f);
        private static readonly Color ActiveColor = new Color(0.2f, 0.5f, 0.8f, 0.9f);
        private static readonly Color ActiveWithItemColor = new Color(0.3f, 0.6f, 0.3f, 0.9f);
        private static readonly Color BorderColor = new Color(0.14f, 0.17f, 0.22f, 0.9f);

        private static Texture2D _tex;

        public static bool Visible = true;

        private static Texture2D Tex
        {
            get
            {
                if (_tex == null)
                {
                    _tex = new Texture2D(1, 1);
                    _tex.SetPixel(0, 0, Color.white);
                    _tex.Apply();
                }
                return _tex;
            }
        }

        private static void DrawRect(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Tex);
        }

        private static void DrawIcon(Rect rect, Texture2D tex)
        {
            if (tex == null) return;
            GUI.color = Color.white;
            GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit);
        }

        public static void Draw()
        {
            if (!Visible) return;

            var pm = PlayerManager.instance;
            if (pm == null) return;

            // Hide when paused or in UI mode (shop, computer, etc.)
            if (pm.isGamePaused) return;
            if (!pm.enabledPlayerMovement) return;

            float totalWidth = Inventory.MaxSlots * SlotSize + (Inventory.MaxSlots - 1) * SlotSpacing;
            float startX = (Screen.width - totalWidth) / 2f;
            float startY = Screen.height - BottomMargin - SlotSize;

            bool handHasItem = pm.objectInHand != PlayerManager.ObjectInHand.None;

            for (int i = 0; i < Inventory.MaxSlots; i++)
            {
                float x = startX + i * (SlotSize + SlotSpacing);
                var rect = new Rect(x, startY, SlotSize, SlotSize);

                bool isActive = i == Inventory.ActiveSlot;
                var slot = Inventory.Slots[i];
                bool hasStashedItem = slot != null && !slot.IsEmpty();
                bool showItem = isActive ? handHasItem : hasStashedItem;

                // Background
                Color bg;
                if (isActive && showItem) bg = ActiveWithItemColor;
                else if (isActive) bg = ActiveColor;
                else if (showItem) bg = StashedColor;
                else bg = EmptyColor;

                DrawRect(rect, bg);

                // Border on active slot
                if (isActive)
                {
                    float b = 2f;
                    DrawRect(new Rect(rect.x - b, rect.y - b, rect.width + b * 2, b), BorderColor);
                    DrawRect(new Rect(rect.x - b, rect.yMax, rect.width + b * 2, b), BorderColor);
                    DrawRect(new Rect(rect.x - b, rect.y, b, rect.height), BorderColor);
                    DrawRect(new Rect(rect.xMax, rect.y, b, rect.height), BorderColor);
                }

                GUI.color = Color.white;

                // Slot number (top-left corner)
                GUI.Label(new Rect(rect.x + 3, rect.y + 1, 15, 20), (i + 1).ToString());

                // Item info inside the slot
                if (showItem)
                {
                    Texture2D icon;
                    int count;
                    string name;

                    if (isActive && handHasItem)
                    {
                        count = pm.numberOfObjectsInHand;
                        name = pm.objectInHand.ToString();
                        icon = Inventory.HandIcon;
                    }
                    else
                    {
                        count = slot.AliveCount();
                        name = slot.DisplayName;
                        icon = slot.Icon;
                    }

                    // Icon area: leave top 16px for slot number, bottom 16px for count
                    var iconRect = new Rect(rect.x + 4, rect.y + 14, rect.width - 8, rect.height - 26);

                    /*if (icon != null)
                    {
                        DrawIcon(iconRect, icon);
                    }
                    else
                    {*/
                        // Fallback: abbreviated name
                        if (name.Length > 8) name = name.Substring(0, 7) + "..";
                        GUI.Label(new Rect(rect.x + 2, rect.y + 17, rect.width - 4, 20), name);
                    //}

                    // Count overlay (bottom-right, only if > 1)
                    if (count > 1)
                    {
                        string countStr = $"x{count}";
                        // Dark shadow for readability over icon
                        GUI.color = new Color(0, 0, 0, 0.6f);
                        GUI.DrawTexture(new Rect(rect.x + 3, rect.yMax - 17, rect.width - 6, 14), Tex);
                        GUI.color = Color.white;
                        GUI.Label(new Rect(rect.x + 4, rect.yMax - 18, rect.width - 8, 16), countStr);
                    }
                }
            }
        }
    }
}
