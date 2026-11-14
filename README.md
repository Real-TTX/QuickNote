# QuickNote

Eine bewusst einfache Notiz-Software für Windows und macOS (C#, Avalonia UI). Optisch angelehnt an Todoist: warm, ruhig, drei Spalten.

## Funktionen

- Notizen sind intern **Markdown** (`.md`, mit kleinem Front-Matter-Block für Titel, Tags, Favorit, Archiv).
- **Export** als Markdown, HTML oder Text.
- **Verschlüsselung mit Kennwort**: verschlüsselte Notizen werden als `.txts` gespeichert (AES-256-GCM, PBKDF2-SHA256 mit 600 000 Iterationen). Beim Öffnen wird das Kennwort abgefragt. Das Kennwort wird nie gespeichert.
- Notizbücher (Ordner), Favoriten, Archiv, Suche, Autosave.
- Notizen liegen als einfache Dateien in `Dokumente/QuickNote` (überschreibbar mit der Umgebungsvariable `QUICKNOTE_VAULT`).

## Aufbau

| Projekt | Inhalt |
|---|---|
| `src/QuickNote.Core` | Modelle (ohne Logik) und Business-Layer (nur statische Klassen: `NoteStore`, `NoteEncryption`, `NoteExporter`, `AccessGuard`, …) |
| `src/QuickNote.App` | Avalonia-UI (MVVM). Keine Speicherzugriffe in UI oder Modellen |
| `src/QuickNote.Setup` | Eigener, gestalteter Windows-Installer (Avalonia), bettet die App ein |
| `tests/QuickNote.Core.Tests` | Unit-Tests |
| `tools/IconGen` | Erzeugt PNG/ICO/ICNS aus den Logo-SVGs |
| `assets/logo` | Logo in drei Detailstufen (full, medium, small) und Wordmark |

Berechtigungsprüfungen (`AccessGuard`) laufen immer im Business-Layer, nicht nur in der UI.

## Versionierung

| Kanal | Branch | Format | Beispiel |
|---|---|---|---|
| Release | `main` | `<major>.<minor>.<build>-<builddate>` | `1.0.42-20261114` |
| Nightly | `dev` | `nightly-<build>-<builddate>` | `nightly-42-20261114` |
| Local | alles andere | `local-<builddate>` | `local-20261114` |

`major.minor` stehen in der Datei `VERSION`, die Build-Nummer ist die Nummer des GitHub-Actions-Laufs.

## Lokal bauen

```bash
dotnet test tests/QuickNote.Core.Tests
dotnet run --project src/QuickNote.App
```

Windows (Setup + Portable):

```powershell
./build/Build-Windows.ps1
```

macOS (`.app`, Portable-Zip, DMG):

```bash
build/build-macos.sh local 0 osx-arm64
```

Logos/Icons neu erzeugen: `dotnet run --project tools/IconGen -- .`

## CI/CD

`.github/workflows/build.yml` testet, baut Setup, Portable und DMG und veröffentlicht bei Pushes auf `main` (Release) und `dev` (Pre-Release/Nightly) ein GitHub-Release.

## Hinweis zu macOS

Das DMG ist nur ad-hoc signiert (kein Apple-Developer-Konto). Beim ersten Start: Rechtsklick auf die App, „Öffnen“.
