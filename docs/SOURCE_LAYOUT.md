# Source layout

All source lives under `src/Inventory/` with root namespace **`GregModInventory`**. No sub-namespaces.

## Tree

```
src/Inventory/
├── Core.cs              # MelonLoader entry point, input handling, drop logic
├── Inventory.cs         # Slot management, stash/restore, icon extraction
├── InventoryHud.cs      # Hotbar HUD rendering (IMGUI)
└── InventorySlot.cs     # Single slot: objects, transform state, stash/restore
```

## File descriptions

### `Core.cs`
MelonLoader entry point (`Core : MelonMod`). Applies Harmony patches on init. In `OnUpdate`: handles number keys 1–9 (jump to slot), H key (toggle HUD), scroll wheel (cycle slots). Manages hand-item icon tracking for freshly picked-up items. Handles custom drop logic for inventory-restored items — finds the `Drop` InputAction from multiple sources (`PlayerClass.inputctrl`, `RayLookAt`, scene `UsableObject`). Contains `CableSpinner_InteractOnClick_Patch` (Harmony) to block interaction with stashed cable spinners (Y > 1000).

### `Inventory.cs`
Static inventory manager. 9 slots. `SwitchToSlot` stashes current hand items and restores the target slot. `CycleSlot` wraps around. `StashHandItems` collects alive `GameObjects` from hand, renders their icon, teleports/stashes them, and clears `PlayerManager` state. `RestoreSlotItems` parents objects back to hand, restores transforms, calls `InteractOnClick` to reinitialize game state, and marks `HandItemsFromInventory = true`. `GetItemIcon` extracts albedo texture from renderer materials (`_BaseMap`, `_MainTex`, etc.).

### `InventoryHud.cs`
Static IMGUI hotbar renderer. Draws 9 slots at the bottom-center of the screen. Active slot gets a white border. Color-coded backgrounds: empty (dark), stashed (green), active (blue), active-with-item (bright green). Shows slot number (top-left), abbreviated item name, and count overlay (bottom-right, for stacks > 1). Hidden when game is paused or UI is open.

### `InventorySlot.cs`
Data class for a single inventory slot. Stores: `ObjectInHand` type, `GameObject[]` array, display name, prefab ID, icon texture, and saved local positions/rotations. `Stash()` disables physics and teleports cable spinners to Y=5000 (above stash threshold) or deactivates other items in place. `RestoreToHand()` re-parents to hand transform, restores local transforms, calls `InteractOnClick()` for game state reinitialization, and re-applies saved transform. `IsEmpty()` / `AliveCount()` check for destroyed objects.
