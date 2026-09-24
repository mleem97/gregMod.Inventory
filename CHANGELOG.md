# Changelog — gregMod.Inventory

Format: [Keep a Changelog](https://keepachangelog.com/de/1.0.0/). Version: siehe [`VERSION`](VERSION).

## [Unreleased]

### Added

- Save-Persistenz via gregCore (`GregSaveGuard`-Sidecar `gregMod.Inventory`):
  Slot-Typ, prefabID, Stueckzahl und Kabel-Status (Laenge/verbrauchter Teil/Typ)
  ueberleben Save/Load. Restore per `ComputerShop.GetPrefabForItem` (funktioniert
  auch fuer MoreSpools-IDs 100+ und Backplanes-Varianten); Vanilla-restaurierte
  Stash-Streuner (y > 4000) werden per prefabID adoptiert. Ohne gregCore bleibt
  das Inventar fluechtig (Standalone, Warnung im Log).

### Fixed

- Kein `InteractOnClick()` mehr beim Slot-Restore: Der Vanilla-Pickup-Handler
  hat pro Restore UI-Elemente/Hand-Kopien nacherzeugt (dupliziertes Vanilla-UI
  + Item-Vermehrung pro Slot-Wechsel). Hand-Status wird jetzt direkt gesetzt.
- Gestashte CableSpinner werden auch deaktiviert (vorher aktiv bei y=5000:
  Update-/Raycast-/Save-Nebenwirkungen, Klon-Haufen an einem Punkt).

### Added

- Einheitliches Open-Source-Layout (README, Docs, Badges) nach gregCore-Vorbild.

## [0.1.0] — 2026-09-22

- Initialer standardisierter Stand.
