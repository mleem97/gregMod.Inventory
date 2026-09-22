# AGENTS.md — Hinweise für KI-Agenten (gregMod.Inventory)

Repo: [https://github.com/mleem97/gregMod.Inventory](https://github.com/mleem97/gregMod.Inventory) · Lizenz: Apache-2.0 · Version: siehe `VERSION`.

## Pflichten

1. **Erst lesen:** `README.md`, `docs/INDEX.md`, `CONTRIBUTING.md` — danach erst ändern.
2. **Keine Secrets committen** (Keys, Tokens, `.env`). Key-Nutzung nur via Umgebungsvariablen.
3. **Historie erhalten:** kein `push --force`, kein History-Rewrite ohne Auftrag.
4. **Änderungen belegen:** vor dem Fertigmelden bauen/testen, was das Repo hergibt (`QUICKSTART.md`).
5. **Doku synchron halten:** bei neuen Features `README.md` + `docs/` + `CHANGELOG.md` (Unreleased) aktualisieren.
6. **Konventionen:** Conventional Commits (`feat:`, `fix:`, `docs:`, `chore:` …), eine logische Änderung pro Commit.
7. **Bei Unsicherheit:** anhalten und fragen statt raten — insbesondere bei Deletes, Migrations, CI.

## Layout

Siehe [README.md](README.md) → Repository Layout. Zentrale Anlaufstellen: `docs/INDEX.md`, `scripts/`, `tests/`.
