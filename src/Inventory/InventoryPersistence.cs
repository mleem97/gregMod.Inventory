using System;
using System.Collections.Generic;
using System.Globalization;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace GregModInventory
{
    /// <summary>
    /// Save persistence for hotbar inventory via gregCore
    /// (GregSaveGuard sidecar "gregMod.Inventory").
    ///
    /// Format (one line, robust without JSON dependency):
    ///   v=1;active=2;slots=idx,typeInt,prefabID,count,len,inUse,ctype|...
    /// Floats invariant ("R"). Only filled slots are written.
    ///
    /// Load: sidecar load callback parks the payload; once the shop
    /// is available, slots are rebuilt (prefab lookup via
    /// ComputerShop.GetPrefabForItem — also works for MoreSpools IDs
    /// 100+ and Backplanes variants 9001+). Stash strays already in
    /// scene (y &gt; 4000, e.g. restored from vanilla save)
    /// are adopted by prefabID instead of respawned; rest destroyed.
    ///
    /// JIT isolation: ONLY RegisterWithCore() touches gregCore types and must
    /// ONLY be called behind GregHost.HasCore. Everything else is
    /// vanilla-only and also runs standalone.
    /// </summary>
    public static class InventoryPersistence
    {
        public const string SidecarId = "gregMod.Inventory";

        // Everything above this height counts as our stash area.
        // (InventorySlot.StashPosition = y 5000; patch threshold = 1000.)
        private const float StrayThresholdY = 4000f;

        private static string _pendingPayload;

        // Call ONLY if GregHost.HasCore is true!
        public static void RegisterWithCore()
        {
            gregCore.Infrastructure.Persistence.GregSaveGuard.RegisterSidecar(
                SidecarId, Serialize, StagePayload);
        }

        public static bool HasPendingPayload => !string.IsNullOrEmpty(_pendingPayload);

        // Called from sidecar load callback (gregCore) — parks only.
        // Actual spawn happens in TrySpawnPending (OnUpdate),
        // once shop is ready (order-independent).
        private static void StagePayload(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload)) return;
            _pendingPayload = payload;
            MelonLogger.Msg($"[Inventory] Save payload parked ({payload.Length} chars).");
        }

        // Call every frame from Core.OnUpdate (vanilla-only, standalone-safe).
        public static void TrySpawnPending()
        {
            if (string.IsNullOrEmpty(_pendingPayload)) return;
            var mgm = PlayerManager.instance != null ? MainGameManager.instance : null;
            var shop = mgm != null ? mgm.computerShop : null;
            if (shop == null) return;

            string payload = _pendingPayload;
            _pendingPayload = null; // One-shot: no endless retry.
            try
            {
                SpawnFromPayload(shop, payload);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Inventory] Restore failed: {ex.GetBaseException().Message}");
            }
        }

        // ── Serialize ────────────────────────────────────────────────────

        public static string Serialize()
        {
            try
            {
                var parts = new List<string>();
                parts.Add("v=1");
                parts.Add("active=" + Inventory.ActiveSlot);
                var slotParts = new List<string>();
                for (int i = 0; i < Inventory.MaxSlots; i++)
                {
                    try
                    {
                        var slot = Inventory.Slots[i];
                        if (slot == null || slot.IsEmpty()) continue;

                        int alive = 0;
                        float len = 0f, inUse = 0f, ctype = 0f;
                        foreach (var go in slot.StoredObjects)
                        {
                            if (go == null) continue;
                            alive++;
                            if (len == 0f && inUse == 0f)
                            {
                                try
                                {
                                    var spinner = go.GetComponent<CableSpinner>();
                                    if (spinner != null)
                                    {
                                        len = spinner.cableLenght;
                                        inUse = spinner.cableLenghtInUse;
                                        ctype = spinner.cableType;
                                    }
                                }
                                catch { }
                            }
                        }
                        if (alive == 0) continue;

                        slotParts.Add(string.Join(",",
                            i.ToString(CultureInfo.InvariantCulture),
                            ((int)slot.ItemType).ToString(CultureInfo.InvariantCulture),
                            slot.PrefabID.ToString(CultureInfo.InvariantCulture),
                            alive.ToString(CultureInfo.InvariantCulture),
                            len.ToString("R", CultureInfo.InvariantCulture),
                            inUse.ToString("R", CultureInfo.InvariantCulture),
                            ctype.ToString("R", CultureInfo.InvariantCulture)));
                    }
                    catch { }
                }
                parts.Add("slots=" + string.Join("|", slotParts.ToArray()));
                return string.Join(";", parts.ToArray());
            }
            catch
            {
                return "v=1;active=0;slots=";
            }
        }

        // ── Deserialisieren + Spawnen ────────────────────────────────────────

        private sealed class SlotDesc
        {
            public int SlotIndex;
            public int TypeInt;
            public int PrefabID;
            public int Count;
            public float Len;
            public float InUse;
            public float CType;
        }

        private static bool TryParse(string payload, out int active, out List<SlotDesc> slots)
        {
            active = 0;
            slots = new List<SlotDesc>();
            if (string.IsNullOrWhiteSpace(payload)) return false;
            try
            {
                foreach (var seg in payload.Split(';'))
                {
                    if (seg.StartsWith("active=", StringComparison.Ordinal))
                        int.TryParse(seg.Substring(7), NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out active);
                    else if (seg.StartsWith("slots=", StringComparison.Ordinal))
                    {
                        string body = seg.Substring(6);
                        if (string.IsNullOrEmpty(body)) continue;
                        foreach (var s in body.Split('|'))
                        {
                            var f = s.Split(',');
                            if (f.Length != 7) continue;
                            var d = new SlotDesc();
                            if (!int.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out d.SlotIndex)) continue;
                            if (!int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out d.TypeInt)) continue;
                            if (!int.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out d.PrefabID)) continue;
                            if (!int.TryParse(f[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out d.Count)) continue;
                            if (!float.TryParse(f[4], NumberStyles.Float, CultureInfo.InvariantCulture, out d.Len)) continue;
                            if (!float.TryParse(f[5], NumberStyles.Float, CultureInfo.InvariantCulture, out d.InUse)) continue;
                            if (!float.TryParse(f[6], NumberStyles.Float, CultureInfo.InvariantCulture, out d.CType)) continue;
                            if (d.SlotIndex < 0 || d.SlotIndex >= Inventory.MaxSlots) continue;
                            if (d.Count <= 0) continue;
                            slots.Add(d);
                        }
                    }
                }
                if (active < 0 || active >= Inventory.MaxSlots) active = 0;
                return true;
            }
            catch { return false; }
        }

        private static void SpawnFromPayload(Il2Cpp.ComputerShop shop, string payload)
        {
            if (!TryParse(payload, out int active, out var descs) || descs.Count == 0)
            {
                MelonLogger.Warning("[Inventory] Payload empty/invalid — nothing to restore.");
                return;
            }

            // Collect own objects (sweep guard), then clear slots.
            var owned = new HashSet<int>();
            for (int i = 0; i < Inventory.MaxSlots; i++)
            {
                var slot = Inventory.Slots[i];
                if (slot == null || slot.StoredObjects == null) continue;
                foreach (var go in slot.StoredObjects)
                {
                    if (go == null) continue;
                    try { owned.Add(go.GetInstanceID()); } catch { }
                }
                Inventory.Slots[i] = null;
            }

            // Strays: unparented UsableObjects above threshold that are
            // not ours (e.g. vanilla restore of our old stash).
            var strays = CollectStrays(owned);

            int restored = 0;
            foreach (var d in descs)
            {
                try
                {
                    var gos = new List<GameObject>();

                    // 1) Adopt: take matching strays by prefabID.
                    for (int i = strays.Count - 1; i >= 0 && gos.Count < d.Count; i--)
                    {
                        var stray = strays[i];
                        int pid = -1;
                        try
                        {
                            var u = stray.GetComponent<UsableObject>();
                            if (u != null) pid = u.prefabID;
                        }
                        catch { }
                        if (pid == d.PrefabID)
                        {
                            gos.Add(stray);
                            strays.RemoveAt(i);
                        }
                    }

                    // 2) Spawn rest fresh (prefab lookup via shop).
                    GameObject prefab = null;
                    if (gos.Count < d.Count)
                    {
                        try
                        {
                            prefab = shop.GetPrefabForItem(d.PrefabID,
                                (PlayerManager.ObjectInHand)d.TypeInt);
                        }
                        catch { prefab = null; }
                        if (prefab == null)
                        {
                            MelonLogger.Warning($"[Inventory] No prefab for id={d.PrefabID} " +
                                $"type={d.TypeInt} — slot {d.SlotIndex} skipped " +
                                $"({gos.Count}/{d.Count} adopted).");
                        }
                        else
                        {
                            while (gos.Count < d.Count)
                            {
                                try
                                {
                                    var fresh = UnityEngine.Object.Instantiate(prefab);
                                    if (fresh == null) break;
                                    fresh.SetActive(false);
                                    gos.Add(fresh);
                                }
                                catch { break; }
                            }
                        }
                    }

                    if (gos.Count == 0) continue;

                    // Apply cable state to all spinners.
                    foreach (var go in gos)
                    {
                        try
                        {
                            var spinner = go.GetComponent<CableSpinner>();
                            if (spinner != null)
                            {
                                spinner.cableLenght = d.Len;
                                spinner.cableLenghtInUse = d.InUse;
                                spinner.cableType = (int)d.CType;
                            }
                        }
                        catch { }
                    }

                    // Display name + icon (briefly enable for icon, stash
                    // disables again after).
                    string displayName = ((PlayerManager.ObjectInHand)d.TypeInt).ToString();
                    try
                    {
                        var u0 = gos[0].GetComponent<UsableObject>();
                        if (u0 != null)
                        {
                            if (u0.item != null && !string.IsNullOrEmpty(u0.item.itemName))
                                displayName = u0.item.itemName;
                        }
                    }
                    catch { }

                    Texture2D icon = null;
                    try
                    {
                        gos[0].SetActive(true);
                        icon = Inventory.GetItemIcon(new List<GameObject> { gos[0] });
                    }
                    catch { icon = null; }

                    var slot = new InventorySlot((PlayerManager.ObjectInHand)d.TypeInt,
                        gos.ToArray(), displayName, d.PrefabID, icon);
                    slot.Stash();
                    Inventory.Slots[d.SlotIndex] = slot;
                    restored++;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[Inventory] Slot {d.SlotIndex} restore error: {ex.Message}");
                }
            }

            // Destroy leftover strays with no payload home (invisible
            // leak at stash point), with log.
            int destroyed = 0;
            foreach (var stray in strays)
            {
                try
                {
                    if (stray == null) continue;
                    UnityEngine.Object.Destroy(stray);
                    destroyed++;
                }
                catch { }
            }
            if (destroyed > 0)
                MelonLogger.Msg($"[Inventory] Cleaned up {destroyed} homeless stash stray(s).");

            Inventory.ActiveSlot = active;
            MelonLogger.Msg($"[Inventory] Restore done: {restored}/{descs.Count} slot(s).");
        }

        private static List<GameObject> CollectStrays(HashSet<int> owned)
        {
            var result = new List<GameObject>();
            try
            {
                var all = UnityEngine.Object.FindObjectsOfType<UsableObject>();
                if (all == null) return result;
                foreach (var u in all)
                {
                    try
                    {
                        if (u == null) continue;
                        var go = u.gameObject;
                        if (go == null) continue;
                        if (go.transform.parent != null) continue;
                        if (go.transform.position.y < StrayThresholdY) continue;
                        if (!string.IsNullOrEmpty(go.name) && go.name.Contains("TemplateHolder")) continue;
                        int id = go.GetInstanceID();
                        if (owned.Contains(id)) continue;
                        result.Add(go);
                    }
                    catch { }
                }
            }
            catch { }
            return result;
        }
    }
}
