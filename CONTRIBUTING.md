# Contributing — gregMod.Inventory

Repo: [https://github.com/mleem97/gregMod.Inventory](https://github.com/mleem97/gregMod.Inventory) · Lizenz: Apache-2.0 · Verhaltenskodex: [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

## Workflow

1. Issue oder Idee kurz beschreiben (was/warum).
2. Branch vom aktuellen `main`: `feat/<kurzname>`, `fix/<kurzname>`, `docs/<kurzname>`.
3. Kleine, reviewbare Commits (Conventional Commits).
4. Vor dem PR: bauen + testen (siehe [QUICKSTART.md](QUICKSTART.md)), Doku (`README.md`, `docs/`) und `CHANGELOG.md` (Unreleased) aktualisieren.
5. PR mit Beschreibung, Screenshots/Logs bei UI-/Verhaltensänderungen.

## Regeln

- Keine Secrets, keine Binärdateien ohne Not (dann via Releases, nicht ins Repo).
- Keine generierten Artefakte committen (`bin/`, `obj/`, `dist/`, `node_modules/`, `.next/` …).
- Sicherheitsthemen NICHT als Issue, sondern per [SECURITY.md](SECURITY.md) melden.
