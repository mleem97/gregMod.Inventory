# Architecture — gregMod.Inventory

> gregMod.Inventory** adds a 9-slot hotbar inventory to **Data Center**. Carry multiple items and switch between them using the scroll wheel or number keys 1–9.

## Components

- **Getting started:** see [QUICKSTART.md](../QUICKSTART.md).
- **Scripts:** [`scripts/`](../scripts/) — automation around build/test/release.
- **Tests:** [`tests/`](../tests/) — test cases and fixtures.
- **References:** [`references/`](../references/) — external references and material.
- **Examples:** [`examples/`](../examples/) — runnable minimal examples.

## Data flows

- **Saving:** `InventoryPersistence.Serialize()` → gregCore sidecar
  (`GregSaveGuard.RegisterSidecar("gregMod.Inventory")`, file next to the save).
  Format: `v=1;active=N;slots=idx,typ,prefabID,stueck,len,inUse,ctyp|...`.
  Without gregCore: no save (volatile).
- **Loading:** sidecar callback parks the payload → `TrySpawnPending()` (per frame,
  once `computerShop` is available) rebuilds slots: stray adoption by prefabID
  (y > 4000), rest via `ComputerShop.GetPrefabForItem` + `Instantiate`.
- **Stash/Restore:** `InventorySlot.Stash()` (disabled, spinners additionally
  to y=5000) ↔ `RestoreToHand()` (parent/transform/`objectInHands` manually —
  deliberately WITHOUT `InteractOnClick()`, which duplicated vanilla UI/hand copies).
