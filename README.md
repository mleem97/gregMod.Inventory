# gregMod.Inventory

> 9-slot hotbar inventory system for **Data Center** — carry more items, switch with scroll wheel or number keys.

[![Discord](https://img.shields.io/discord/1392073682133848075?style=for-the-badge&logo=discord&logoColor=white&label=Discord)](https://discord.gg/greg)
[![gregFramework](https://img.shields.io/badge/gregFramework-Website-blue?style=for-the-badge)](https://gregframework.eu)
[![License](https://img.shields.io/badge/License-Apache%202.0-green?style=for-the-badge)](./LICENSE)
[![Version](https://img.shields.io/badge/Version-1.0.2-orange?style=for-the-badge)]()
[![GameVersion](https://img.shields.io/badge/Game%20Version-1.1.0-yellow?style=for-the-badge)]()
[![Unity](https://img.shields.io/badge/Unity-6000.4.12f1-black?style=for-the-badge&logo=unity&logoColor=white)]()

## Links

- **Repository:** [github.com/mleem97/gregMod.Inventory](https://github.com/mleem97/gregMod.Inventory)
- **Discord / Support:** [discord.gg/greg](https://discord.gg/greg)
- **Website:** [gregframework.eu](https://gregframework.eu)

## Overview

**gregMod.Inventory** adds a 9-slot hotbar inventory to **Data Center**. Carry multiple items and switch between them using the scroll wheel or number keys 1–9.

> **Fork** of [leoms1408/datacenter-inventory](https://github.com/leoms1408/datacenter-inventory) by [leoms1408](https://github.com/leoms1408), integrated into the gregMod ecosystem.

## Features

- 9-slot hotbar at the bottom of the screen for quick item switching
- Scroll wheel to cycle through hotbar slots
- Number keys 1–9 to jump directly to a slot
- H key to toggle the HUD on/off
- Items are stashed/restored with full transform and physics state
- CableSpinner support with Harmony patch for stash detection
- Custom drop handling for inventory-restored items

## Installation

1. Install **MelonLoader** (v0.7.2+) for **Data Center**
2. Copy the release DLL into the mod folder:

   ```text
   Game/Mods/gregMod.Inventory.dll
   ```

3. Start the game

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| **1–9** | Jump to hotbar slot |
| **Scroll wheel** | Cycle through slots |
| **H** | Toggle HUD on/off |

## Notes

- Inventory contents are **not saved** — switching slots is session-only; items are lost on quit

## Dependencies

- **MelonLoader** (v0.7.2+)

### Build only

- **Il2CppInterop**
- **Harmony**
- Unity / game interop assemblies from the local Data Center installation

## Build from Source

Requirements:

- .NET 6 SDK
- local Data Center / MelonLoader installation

> **Note:** This mod was built on Linux using Proton-GE 10-34. The `references/` directory contains the required game and MelonLoader DLLs. When building on a different system, adjust the `<HintPath>` entries in the `.csproj` to point to your local MelonLoader and game interop assemblies.

Build:

```bash
git clone https://github.com/mleem97/gregMod.Inventory.git
cd gregMod.Inventory
dotnet build -c Release
```

Release output:

```text
bin/Release/net6.0/gregMod.Inventory.dll
```

## Project Structure

```
gregMod.Inventory/
├── src/Inventory/              # Source code
│   ├── Core.cs                 # MelonLoader entry point, input handling, drop logic
│   ├── Inventory.cs            # Slot management, stash/restore, icon extraction
│   ├── InventoryHud.cs          # Hotbar HUD rendering (IMGUI)
│   └── InventorySlot.cs         # Single slot: objects, transform state, stash/restore
├── references/                 # Game & MelonLoader interop DLLs
├── gregMod.Inventory.csproj    # Project file
├── LICENSE                     # Apache 2.0
└── README.md
```

## Credits

| Role | Contributor |
|------|-------------|
| **Original Author** | [leoms1408](https://github.com/leoms1408) |
| **gregMod Fork** | [mleem97](https://github.com/mleem97) ([TeamGreg Modding](https://github.com/teamGregModding)) |

## License

This project is licensed under the **Apache License 2.0**. See [`LICENSE`](./LICENSE).

Original code by [leoms1408](https://github.com/leoms1408) — [MIT License](https://github.com/leoms1408/datacenter-inventory).

## 🚀 Join the gregFramework Team!

Building the ultimate modding framework for Data Center is a massive undertaking. gregFramework is currently maintained by a passionate core team of three, and we are looking for fellow creators to help us scale this mission!

**Your place in the team:** We won't throw you into the deep end. Depending on your individual strengths and skills, we will match you with the right areas of the project so you can contribute exactly where you have the most fun.

**🌍 Language Requirement:** A solid grasp of written English is required (without relying on machine translation). Being comfortable speaking English in voice chats is a huge plus, but we completely respect those who prefer to stick to text!

**We are looking for motivated volunteers to join our crew across several roles:**

- 💻 **Code Wizards** (C#, Rust, Lua, TS, GO) — Build and expand the core framework and mod packages
- 🎨 **Asset Creators** (3D Models, hardware assets) — Bring the framework to life visually
- 📚 **Technical Writers** — Craft wiki entries, maintain documentation, and write user guides
- 🎮 **Alpha Testers** — Hunt down bugs, stress-test the framework, and provide critical feedback
- ⚙️ **System Guardians** — Maintain our Linux servers, Docker containers, and infrastructure
- 🤝 **Community Managers** — Foster our Discord community, gather feedback, and keep the energy high

Interested in joining the project? Everyone is absolutely welcome! Send us an email at **apply@gregframework.eu**, shoot a quick DM, or drop a message on [Discord](https://discord.gg/greg).

---

**gregFramework — powered by the community.**
