# Architektur — gregMod.Inventory

> gregMod.Inventory** adds a 9-slot hotbar inventory to **Data Center**. Carry multiple items and switch between them using the scroll wheel or number keys 1–9.

## Komponenten

- **Einstieg:** siehe [QUICKSTART.md](../QUICKSTART.md).
- **Skripte:** [`scripts/`](../scripts/) — Automatisierung rund um Build/Test/Release.
- **Tests:** [`tests/`](../tests/) — Testfälle und Fixtures.
- **Referenzen:** [`references/`](../references/) — externe Referenzen und Material.
- **Beispiele:** [`examples/`](../examples/) — lauffähige Minimalbeispiele.

## Datenflüsse

- **Speichern:** `InventoryPersistence.Serialize()` → gregCore-Sidecar
  (`GregSaveGuard.RegisterSidecar("gregMod.Inventory")`, Datei neben dem Save).
  Format: `v=1;active=N;slots=idx,typ,prefabID,stueck,len,inUse,ctyp|...`.
  Ohne gregCore: kein Save (fluechtig).
- **Laden:** Sidecar-Callback parkt das Payload → `TrySpawnPending()` (pro Frame,
  sobald `computerShop` da ist) baut Slots neu: Streuner-Adoption per prefabID
  (y > 4000), Rest via `ComputerShop.GetPrefabForItem` + `Instantiate`.
- **Stash/Restore:** `InventorySlot.Stash()` (deaktiviert, Spinner zusaetzlich
  auf y=5000) ↔ `RestoreToHand()` (Parent/Transform/`objectInHands` manuell —
  bewusst OHNE `InteractOnClick()`, das Vanilla-UI/Hand-Kopien duplizierte).
