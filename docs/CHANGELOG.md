# Changelog

## v1.0.1

- Fork of [leoms1408/datacenter-inventory](https://github.com/leoms1408/datacenter-inventory) integrated into gregMod ecosystem
- Custom drop handling for inventory-restored items (`ManualDrop`)
- CableSpinner stash via teleport (Y=5000) with Harmony patch for `InteractOnClick` blocking
- `InteractOnClick` call on restore to reinitialize game-internal state
- Cached `InputController` for drop detection across multiple sources
- `EnsureDropActionEnabled` to re-enable disabled Drop InputAction
- Icon extraction from material texture slots (`_BaseMap`, `_MainTex`, `_BaseColorMap`, etc.)
- H key to toggle HUD visibility
- Slot cleanup for destroyed objects
