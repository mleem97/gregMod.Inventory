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
                        GameObject[] objects;
                        int typeInt;
                        int prefabID;

                        var slot = Inventory.Slots[i];
                        if (slot != null && !slot.IsEmpty())
                        {
                            objects = slot.StoredObjects;
                            typeInt = (int)slot.ItemType;
                            prefabID = slot.PrefabID;
                        }
                        else if (i == Inventory.ActiveSlot)
                        {
                            // The active slot's item isn't in Slots[] while it's
                            // equipped (RestoreSlotItems clears it on switch) — it's
                            // live in the player's hand instead. Read it straight from
                            // PlayerManager so saving while holding your hotbar item
                            // doesn't silently drop it.
                            var pm = PlayerManager.instance;
                            if (pm == null || pm.objectInHand == PlayerManager.ObjectInHand.None) continue;
                            var handArray = pm.objectInHandGO;
                            if (handArray == null) continue;

                            var liveObjects = new List<GameObject>();
                            int livePrefab = -1;
                            foreach (var go in handArray)
                            {
                                if (go == null) continue;
                                liveObjects.Add(go);
                                if (livePrefab < 0)
                                {
                                    var u = go.GetComponent<UsableObject>();
                                    if (u != null) livePrefab = u.prefabID;
                                }
                            }
                            if (liveObjects.Count == 0) continue;

                            objects = liveObjects.ToArray();
                            typeInt = (int)pm.objectInHand;
                            prefabID = livePrefab;
                        }
                        else continue;

                        int alive = 0;
                        float len = 0f, inUse = 0f, ctype = 0f;
                        string rgb = "";
                        foreach (var go in objects)
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
                                        rgb = spinner.rgbColor ?? "";
                                    }
                                }
                                catch { }
                            }
                        }
                        if (alive == 0) continue;

                        slotParts.Add(string.Join(",",
                            i.ToString(CultureInfo.InvariantCulture),
                            typeInt.ToString(CultureInfo.InvariantCulture),
                            prefabID.ToString(CultureInfo.InvariantCulture),
                            alive.ToString(CultureInfo.InvariantCulture),
                            len.ToString("R", CultureInfo.InvariantCulture),
                            inUse.ToString("R", CultureInfo.InvariantCulture),
                            ctype.ToString("R", CultureInfo.InvariantCulture),
                            // Base64: the color string may itself contain separators.
                            rgb.Length > 0 ? Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(rgb)) : ""));
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
            public string RgbColor = "";
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
                            // 8th field: base64 CableSpinner.rgbColor (optional; older
                            // saves have 7 fields, or a non-base64 value from an
                            // earlier experiment, which is ignored).
                            if (f.Length != 7 && f.Length != 8) continue;
                            var d = new SlotDesc();
                            if (!int.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out d.SlotIndex)) continue;
                            if (!int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out d.TypeInt)) continue;
                            if (!int.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out d.PrefabID)) continue;
                            if (!int.TryParse(f[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out d.Count)) continue;
                            if (!float.TryParse(f[4], NumberStyles.Float, CultureInfo.InvariantCulture, out d.Len)) continue;
                            if (!float.TryParse(f[5], NumberStyles.Float, CultureInfo.InvariantCulture, out d.InUse)) continue;
                            if (!float.TryParse(f[6], NumberStyles.Float, CultureInfo.InvariantCulture, out d.CType)) continue;
                            if (f.Length == 8 && f[7].Length > 0)
                            {
                                try { d.RgbColor = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(f[7])); }
                                catch { d.RgbColor = ""; }
                            }
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

                    // 1b) The slot that was in hand at save time: the vanilla save
                    // also records the held item and restores it loose at the hand
                    // position, so the player got a duplicate. Adopting that copy
                    // doesn't work — its own Start() hasn't run yet and runs later
                    // in hand, which left it broken (it vanished). Delete it and
                    // spawn our own copy below; rgbColor etc. come from the payload.
                    if (d.SlotIndex == active)
                    {
                        int removed = 0;
                        foreach (var dup in FindHeldDuplicates(owned, d))
                        {
                            if (removed >= d.Count) break;
                            try { UnityEngine.Object.Destroy(dup); removed++; }
                            catch { /* already destroyed: nothing to remove */ }
                        }
                        if (removed > 0)
                            MelonLogger.Msg($"[Inventory] Removed {removed} vanilla duplicate(s) of the held item.");
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
                                    // Move to the stash position (world space, no
                                    // parent) BEFORE anything below briefly activates
                                    // it for icon capture. Left at the prefab's raw
                                    // spawn point, a loose active item sitting in
                                    // normal play space can get caught by a native
                                    // cleanup sweep (observed: destroyed ~1s later).
                                    fresh.transform.SetParent(null, false);
                                    fresh.transform.position = InventorySlot.StashPosition;
                                    // The prefab's own authored LOCAL transform is meant
                                    // for sitting in a rack/shop display, not for being
                                    // held — InventorySlot captures local position/
                                    // rotation as the "hand pose" below, so the raw
                                    // prefab's values would put a held item somewhere
                                    // odd (sometimes right in front of the camera).
                                    // Centering it gives every restored item the same
                                    // sane default once it's parented to the hand.
                                    fresh.transform.localPosition = Vector3.zero;
                                    fresh.transform.localRotation = Quaternion.identity;
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
                                // The game applies rgbColor from the reel's own Start(),
                                // which never runs here because the slot stashes
                                // (deactivates) the item first — apply it now.
                                string rgb = !string.IsNullOrEmpty(d.RgbColor) ? d.RgbColor : spinner.rgbColor;
                                if (!string.IsNullOrEmpty(rgb) && ColorUtility.TryParseHtmlString(rgb, out var col))
                                {
                                    try { spinner.ApplyColor(col, rgb); }
                                    catch (Exception ex) { MelonLogger.Warning($"[Inventory] ApplyColor failed: {ex.Message}"); }
                                }
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

                    MelonLogger.Msg($"[Inventory] Slot {d.SlotIndex} restored: '{displayName}' " +
                        $"prefabID={d.PrefabID} count={gos.Count} icon={(icon != null ? "yes" : "no")}.");
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
            // Every slot was stashed above, including the one that was in hand;
            // Core.OnUpdate puts it back in hand once gameplay is live.
            Inventory.PendingEquip = true;
            Inventory.PendingEquipAt = Time.unscaledTime + 1f;
            MelonLogger.Msg($"[Inventory] Restore done: {restored}/{descs.Count} slot(s).");
        }

        private const float HandAdoptRadius = 2.5f;

        // The vanilla load parents restored loose items under a "UsableObjects"
        // container, so "no parent" alone missed them. Anything else with a parent
        // (trolley cargo, racks, a hand) is not a loose item.
        private static bool IsLooseWorldItem(GameObject go)
        {
            var parent = go.transform.parent;
            return parent == null || parent.name == "UsableObjects";
        }

        // Vanilla copies of the held item. Cable reels are matched by their exact
        // saved length/in-use values (camera position isn't reliable during the
        // first load); other items by distance to the camera, nearest first.
        private static List<GameObject> FindHeldDuplicates(HashSet<int> owned, SlotDesc d)
        {
            var exact = new List<(GameObject go, float dist)>();
            var near = new List<(GameObject go, float dist)>();
            try
            {
                var cam = Camera.main;
                var all = UnityEngine.Object.FindObjectsOfType<UsableObject>();
                if (all != null)
                {
                    foreach (var u in all)
                    {
                        try
                        {
                            if (u == null || u.prefabID != d.PrefabID) continue;
                            var go = u.gameObject;
                            if (go == null || !IsLooseWorldItem(go)) continue;
                            if (owned.Contains(go.GetInstanceID())) continue;
                            float dist = cam != null
                                ? Vector3.Distance(go.transform.position, cam.transform.position)
                                : float.MaxValue;
                            var spinner = go.GetComponent<CableSpinner>();
                            if (spinner != null &&
                                Mathf.Abs(spinner.cableLenght - d.Len) < 0.01f &&
                                Mathf.Abs(spinner.cableLenghtInUse - d.InUse) < 0.01f)
                                exact.Add((go, dist));
                            else if (dist <= HandAdoptRadius)
                                near.Add((go, dist));
                        }
                        catch { /* object destroyed mid-scan (Il2Cpp throws on collected objects): skip it */ }
                    }
                }
            }
            catch { /* best-effort: worst case the vanilla duplicate stays in the world */ }
            // Exact (same length/in-use) matches first, nearest first — so a spare
            // identical spool elsewhere loses to the copy at the hand position.
            exact.Sort((a, b) => a.dist.CompareTo(b.dist));
            near.Sort((a, b) => a.dist.CompareTo(b.dist));
            var list = new List<GameObject>();
            foreach (var e in exact) list.Add(e.go);
            foreach (var n in near) list.Add(n.go);
            return list;
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
