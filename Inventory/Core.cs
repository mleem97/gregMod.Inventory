using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;

[assembly: MelonInfo(typeof(GregModInventory.Core), "gregMod.Inventory", "1.0.1", "leoms1408 / mleem97")]
[assembly: MelonGame("Waseku", "Data Center")]

namespace GregModInventory
{
    public class Core : MelonMod
    {
        private float _lastScrollTime;
        private const float ScrollCooldown = 0.15f;
        private PlayerManager.ObjectInHand _lastHandItem = PlayerManager.ObjectInHand.None;

        // Track whether current hand items were restored by us
        public static bool HandItemsFromInventory;

        // Cached references for drop handling
        private InputAction _dropAction;
        public static InputController CachedInputCtrl;

        public override void OnInitializeMelon()
        {
            Instance = this;
            HarmonyInstance.PatchAll();
            LoggerInstance.Msg("gregMod.Inventory v1.0.1 loaded. Based on Inventory by leoms1408.");
        }

        public override void OnUpdate()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            var pm = PlayerManager.instance;
            if (pm == null) return;

            if (pm.isGamePaused) return;
            if (!pm.enabledPlayerMovement) return;

            Inventory.CleanupSlots();

            // Render icon for freshly picked-up items (not from our inventory)
            if (!HandItemsFromInventory)
            {
                if (pm.objectInHand == PlayerManager.ObjectInHand.None)
                {
                    Inventory.HandIcon = null;
                    _lastHandItem = PlayerManager.ObjectInHand.None;
                }
                else if (pm.objectInHand != _lastHandItem)
                {
                    _lastHandItem = pm.objectInHand;
                    var handArray = pm.objectInHandGO;
                    if (handArray != null && handArray.Length > 0 && handArray[0] != null)
                    {
                        var tmp = new System.Collections.Generic.List<GameObject> { handArray[0] };
                        Inventory.HandIcon = Inventory.GetItemIcon(tmp);
                    }
                }
            }

            // Handle drop for inventory-restored items.
            // We do this FULLY manually since the game's native drop callbacks
            // aren't connected for items we restored from inventory.
            if (HandItemsFromInventory)
            {
                if (IsDropPressed(pm))
                    ManualDrop(pm);
            }

            // Number keys 1-9: jump to slot
            if (kb.digit1Key.wasPressedThisFrame) Inventory.SwitchToSlot(0);
            else if (kb.digit2Key.wasPressedThisFrame) Inventory.SwitchToSlot(1);
            else if (kb.digit3Key.wasPressedThisFrame) Inventory.SwitchToSlot(2);
            else if (kb.digit4Key.wasPressedThisFrame) Inventory.SwitchToSlot(3);
            else if (kb.digit5Key.wasPressedThisFrame) Inventory.SwitchToSlot(4);
            else if (kb.digit6Key.wasPressedThisFrame) Inventory.SwitchToSlot(5);
            else if (kb.digit7Key.wasPressedThisFrame) Inventory.SwitchToSlot(6);
            else if (kb.digit8Key.wasPressedThisFrame) Inventory.SwitchToSlot(7);
            else if (kb.digit9Key.wasPressedThisFrame) Inventory.SwitchToSlot(8);

            // H: toggle HUD
            if (kb.hKey.wasPressedThisFrame)
                InventoryHud.Visible = !InventoryHud.Visible;

            // Scroll wheel: cycle hotbar
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.y.ReadValue();
                if (scroll != 0 && Time.time - _lastScrollTime > ScrollCooldown)
                {
                    _lastScrollTime = Time.time;
                    int direction = scroll > 0 ? -1 : 1;
                    Inventory.CycleSlot(direction);
                }
            }
        }

        /// <summary>
        /// Check the game's Drop input action.
        /// Tries multiple sources to find the InputController.
        /// </summary>
        private bool IsDropPressed(PlayerManager pm)
        {
            if (_dropAction == null)
            {
                InputController ic = null;

                // Try 1: from Player
                if (pm.playerClass != null && pm.playerClass.inputctrl != null)
                    ic = pm.playerClass.inputctrl;

                // Try 2: from RayLookAt via FPC
                if (ic == null && pm.fpc != null && pm.fpc.m_RayLookAt != null && pm.fpc.m_RayLookAt.inputctrl != null)
                    ic = pm.fpc.m_RayLookAt.inputctrl;

                // Try 3: from any UsableObject in hand (saved during stash)
                if (ic == null)
                    ic = CachedInputCtrl;

                // Try 4: find any UsableObject in scene
                if (ic == null)
                {
                    var anyUsable = Object.FindObjectOfType<UsableObject>();
                    if (anyUsable != null && anyUsable.inputctrl != null)
                        ic = anyUsable.inputctrl;
                }

                if (ic != null)
                {
                    _dropAction = ic.Player.Drop;
                }
            }

            if (_dropAction != null)
                return _dropAction.WasPressedThisFrame();

            return false;
        }

        /// <summary>
        /// Drop hand items by calling the game's own DropObject().
        /// Native drop callbacks were unsubscribed during stash, so this
        /// is the only drop path. DropObject() handles unparenting, physics,
        /// interaction state, and everything needed to pick items up again.
        /// </summary>
        private void ManualDrop(PlayerManager pm)
        {
            var handArray = pm.objectInHandGO;
            if (handArray == null) return;

            for (int i = 0; i < handArray.Length; i++)
            {
                var go = handArray[i];
                if (go == null) continue;

                var usable = go.GetComponent<UsableObject>();
                if (usable != null)
                {
                    usable.DropObject();

                    // DropObject may not fully restore physics for items that
                    // went through our stash/restore cycle. Force it.
                    if (usable.rb != null)
                    {
                        usable.rb.isKinematic = false;
                        usable.rb.useGravity = true;
                    }
                }

                handArray[i] = null;
            }

            pm.objectInHand = PlayerManager.ObjectInHand.None;
            pm.numberOfObjectsInHand = 0;
            HandItemsFromInventory = false;

            // Re-enable Drop action — DropObject() disables it (nothing in hand),
            // but we need it active for future inventory drops.
            EnsureDropActionEnabled();
        }

        /// <summary>
        /// Make sure the Drop InputAction is enabled so WasPressedThisFrame works.
        /// </summary>
        public static void EnsureDropActionEnabled()
        {
            if (Instance?._dropAction != null && !Instance._dropAction.enabled)
                Instance._dropAction.Enable();
        }

        public static Core Instance { get; private set; }

        public override void OnGUI()
        {
            InventoryHud.Draw();
        }
    }

    /// <summary>
    /// Block InteractOnClick on CableSpinners that are stashed (Y > 1000).
    /// When objectInHands is true the cable is in the player's hand → allow.
    /// When Y is below the stash threshold the cable is on the ground → allow.
    /// Otherwise it is in our stash → block.
    /// </summary>
    [HarmonyPatch(typeof(CableSpinner), nameof(CableSpinner.InteractOnClick))]
    static class CableSpinner_InteractOnClick_Patch
    {
        static bool Prefix(CableSpinner __instance)
        {
            if (__instance.objectInHands) return true;
            if (__instance.transform.position.y < 1000f) return true;
            return false;
        }
    }
}
