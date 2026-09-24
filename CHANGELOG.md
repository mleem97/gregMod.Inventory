# Changelog — gregMod.Inventory

Format: [Keep a Changelog](https://keepachangelog.com/de/1.0.0/). Version: see [`VERSION`](VERSION).

## [Unreleased]

### Added

- Save persistence via gregCore (`GregSaveGuard` sidecar `gregMod.Inventory`):
  slot type, prefabID, piece count, and cable status (length/consumed part/type)
  survive save/load. Restore via `ComputerShop.GetPrefabForItem` (also works
  for MoreSpools IDs 100+ and Backplanes variants); vanilla-restored
  stash strays (y > 4000) are adopted by prefabID. Without gregCore,
  the inventory remains volatile (standalone, warning in the log).

### Fixed

- No more `InteractOnClick()` on slot restore: the vanilla pickup handler
  recreated UI elements/hand copies per restore (duplicated vanilla UI
  + item duplication per slot switch). The hand state is now set directly.
- Stashed CableSpinners are now deactivated as well (previously active at y=5000:
  update/raycast/save side effects, clone pile at a single point).

### Added

- Unified open-source layout (README, docs, badges) following the gregCore template.

## [0.1.0] — 2026-09-22

- Initial standardized baseline.
