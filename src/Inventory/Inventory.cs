using System.Collections.Generic;
using Il2Cpp;
using UnityEngine;

namespace GregModInventory
{
    public static class Inventory
    {
        public const int MaxSlots = 9;

        public static readonly InventorySlot[] Slots = new InventorySlot[MaxSlots];
        public static int ActiveSlot = 0;

        // Icon for whatever item is currently in the player's hand
        public static Texture2D HandIcon;

        /// <summary>
        /// Switch to a different hotbar slot. Automatically stashes current hand
        /// items and restores the target slot's items.
        /// </summary>
        public static void SwitchToSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxSlots) return;
            if (slotIndex == ActiveSlot) return;

            var pm = PlayerManager.instance;
            if (pm == null) return;

            // 1) Stash whatever is currently in hand into the old active slot
            StashHandItems(ActiveSlot);

            // 2) Switch
            ActiveSlot = slotIndex;

            // 3) Restore the new slot's items into hand
            RestoreSlotItems(ActiveSlot);
        }

        /// <summary>
        /// Set by a save restore (which stashes every slot, including the one
        /// that was in hand). Consumed from Core.OnUpdate once gameplay is live:
        /// during the load the hand position isn't usable yet, so equipping
        /// right away left the item invisible until the next slot switch.
        /// </summary>
        internal static bool PendingEquip { get; set; }

        /// <summary>Earliest time (unscaled) the pending re-equip may run.</summary>
        internal static float PendingEquipAt { get; set; }

        /// <summary>
        /// Put the active slot's items into the player's hand if the hand is empty.
        /// </summary>
        public static void EquipActiveSlot()
        {
            var pm = PlayerManager.instance;
            if (pm == null || pm.objectInHand != PlayerManager.ObjectInHand.None) return;
            var slot = Slots[ActiveSlot];
            RestoreSlotItems(ActiveSlot);
            if (slot != null && pm.objectInHand != PlayerManager.ObjectInHand.None)
            {
                _watchSlot = slot;
                _watchIndex = ActiveSlot;
                _watchUntil = Time.unscaledTime + 5f;
            }

            // Restored cables have no icon: it was captured before their materials
            // were set up. Core.OnUpdate retries from the live hand object for a
            // short while (the materials are only ready a frame or more later).
            if (HandIcon == null) IconRetryFrames = 120;
        }

        private static InventorySlot _watchSlot;
        private static int _watchIndex;
        private static float _watchUntil;

        /// <summary>
        /// Call every frame. Shortly after a load the game can reset the hand
        /// state to empty after we re-equipped, leaving the item attached to the
        /// hand but untracked (not shown in the hotbar, can't be dropped). If the
        /// items are still at the hand position, put them back in hand.
        /// </summary>
        public static void WatchEquip()
        {
            if (_watchSlot == null) return;
            if (Time.unscaledTime > _watchUntil) { _watchSlot = null; return; }

            var pm = PlayerManager.instance;
            if (pm == null || pm.objectInHand != PlayerManager.ObjectInHand.None) return;

            var handPos = pm.objectInHandPositionGO != null ? pm.objectInHandPositionGO.transform : null;
            bool stillAtHand = false;
            if (handPos != null && _watchSlot.StoredObjects != null)
            {
                foreach (var go in _watchSlot.StoredObjects)
                    if (go != null && go.transform.parent == handPos) { stillAtHand = true; break; }
            }
            if (!stillAtHand) { _watchSlot = null; return; } // dropped or switched away

            Slots[_watchIndex] = _watchSlot;
            ActiveSlot = _watchIndex;
            _watchSlot = null;
            RestoreSlotItems(_watchIndex);
            MelonLoader.MelonLogger.Msg("[Inventory] Hand state was reset after load; item put back in hand.");
        }

        /// <summary>Frames left to retry capturing HandIcon after a re-equip.</summary>
        internal static int IconRetryFrames { get; set; }

        /// <summary>One icon capture attempt from the objects in hand.</summary>
        public static void TryCaptureHandIcon()
        {
            var pm = PlayerManager.instance;
            if (pm == null || pm.objectInHandGO == null) return;
            var objects = new List<GameObject>();
            foreach (var go in pm.objectInHandGO)
                if (go != null) objects.Add(go);
            if (objects.Count > 0) HandIcon = GetItemIcon(objects);
        }

        /// <summary>
        /// Cycle to the next/previous slot (direction +1 or -1).
        /// </summary>
        public static void CycleSlot(int direction)
        {
            int next = ((ActiveSlot + direction) % MaxSlots + MaxSlots) % MaxSlots;
            SwitchToSlot(next);
        }

        /// <summary>
        /// Take whatever the player is holding and stash it into the given slot.
        /// </summary>
        private static void StashHandItems(int slotIndex)
        {
            var pm = PlayerManager.instance;
            if (pm == null) return;

            // Clean up slot if its objects were destroyed (placed in world etc.)
            if (Slots[slotIndex] != null && Slots[slotIndex].IsEmpty())
                Slots[slotIndex] = null;

            if (pm.objectInHand == PlayerManager.ObjectInHand.None) return;

            var handArray = pm.objectInHandGO;
            if (handArray == null) return;

            // Collect alive objects from hand
            var objects = new List<GameObject>();
            string displayName = "Item";
            int prefabID = -1;

            for (int i = 0; i < handArray.Length; i++)
            {
                var go = handArray[i];
                if (go == null) continue;
                objects.Add(go);

                if (prefabID < 0)
                {
                    var usable = go.GetComponent<UsableObject>();
                    if (usable != null)
                    {
                        prefabID = usable.prefabID;
                        if (usable.item != null && !string.IsNullOrEmpty(usable.item.itemName))
                            displayName = usable.item.itemName;
                        else
                            displayName = pm.objectInHand.ToString();

                        // Cache InputController for drop detection
                        if (usable.inputctrl != null)
                            Core.CachedInputCtrl = usable.inputctrl;
                    }
                }
            }

            if (objects.Count == 0) return;

            // Render icon before stashing (object still at its world position)
            var icon = GetItemIcon(objects);
            HandIcon = null;

            var slot = new InventorySlot(pm.objectInHand, objects.ToArray(), displayName, prefabID, icon);

            // Teleport objects far away (no SetActive toggling)
            slot.Stash();

            // Clear hand state
            for (int i = 0; i < handArray.Length; i++)
                handArray[i] = null;
            pm.objectInHand = PlayerManager.ObjectInHand.None;
            pm.numberOfObjectsInHand = 0;
            Core.HandItemsFromInventory = false;

            Slots[slotIndex] = slot;
        }

        /// <summary>
        /// Restore a slot's items into the player's hand.
        /// </summary>
        private static void RestoreSlotItems(int slotIndex)
        {
            var pm = PlayerManager.instance;
            if (pm == null) return;

            var slot = Slots[slotIndex];
            if (slot == null || slot.IsEmpty())
            {
                Slots[slotIndex] = null;
                return;
            }

            // Collect alive objects
            var alive = new List<GameObject>();
            foreach (var go in slot.StoredObjects)
                if (go != null) alive.Add(go);

            if (alive.Count == 0)
            {
                Slots[slotIndex] = null;
                return;
            }

            // Manual restore: parent to hand, set saved local transform, rigidbody.
            var handPos = pm.objectInHandPositionGO != null
                ? pm.objectInHandPositionGO.transform
                : null;
            slot.RestoreToHand(handPos);

            // Update PlayerManager hand state
            var handArray = pm.objectInHandGO;
            if (handArray != null)
            {
                for (int i = 0; i < handArray.Length; i++)
                    handArray[i] = i < alive.Count ? alive[i] : null;
            }
            pm.objectInHand = slot.ItemType;
            pm.numberOfObjectsInHand = alive.Count;

            // Mark that current hand items came from inventory (need custom drop handling)
            Core.HandItemsFromInventory = true;

            // Restore cached icon for the HUD
            HandIcon = slot.Icon;

            // Ensure Drop InputAction is enabled (DropObject disables it)
            Core.EnsureDropActionEnabled();

            // Slot is now in hand, clear it
            Slots[slotIndex] = null;
        }

        /// <summary>
        /// Gets the albedo texture from the first renderer on the object.
        /// Reads directly from the material — no rendering required.
        /// </summary>
        private static readonly string[] TexSlots = { "_BaseMap", "_MainTex", "_BaseColorMap", "_Albedo", "_AlbedoMap", "_Diffuse" };

        public static Texture2D GetItemIcon(List<GameObject> objects)
        {
            if (objects == null || objects.Count == 0) return null;

            foreach (var obj in objects)
            {
                if (obj == null) continue;
                var renderers = obj.GetComponentsInChildren<Renderer>();
                if (renderers == null) continue;
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r == null) continue;
                    var mat = r.material;
                    if (mat == null) continue;

                    // Try mainTexture first, then known URP/HDRP slots
                    var tex = mat.mainTexture?.TryCast<Texture2D>();
                    if (tex != null) return tex;

                    foreach (var slot in TexSlots)
                    {
                        try
                        {
                            tex = mat.GetTexture(slot)?.TryCast<Texture2D>();
                            if (tex != null) return tex;
                        }
                        catch { }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Clean up destroyed objects from all slots.
        /// </summary>
        public static void CleanupSlots()
        {
            for (int i = 0; i < MaxSlots; i++)
            {
                if (Slots[i] != null && Slots[i].IsEmpty())
                {
                    MelonLoader.MelonLogger.Warning(
                        $"[Inventory] CleanupSlots: slot {i} ('{Slots[i].DisplayName}') emptied — " +
                        "its stored GameObject(s) were destroyed/null.");
                    Slots[i] = null;
                }
            }
        }
    }
}
