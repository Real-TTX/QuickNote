# Projekt-Blueprint: Desktop-App mit C#, Avalonia, eigenem Setup und CI/CD

Das sind die wesentlichen Elemente aus QuickNote, als Vorlage für spätere Projekte. Die Dateien in diesem Repo sind die Referenzimplementierung. Die Pfade unten zeigen jeweils hin.

---

## 1. Repo-Grundgerüst

```
VERSION                      # nur "major.minor", z. B. 1.0
Directory.Build.props        # Versionierung + gemeinsame Einstellungen für alle Projekte
.editorconfig / .gitattributes / .gitignore (artifacts/ ignorieren)
src/<App>.Core               # Modelle + Business-Layer (kein UI-Framework!)
src/<App>.App                # Avalonia-UI
src/<App>.Setup              # eigener Installer (Avalonia)
tests/<App>.Core.Tests       # xUnit, testet Business + Berechtigungen
tools/IconGen                # SVG -> PNG/ICO/ICNS
assets/logo, assets/icons    # Logo-Quellen (SVG) und generierte Icons
build/Build-Windows.ps1      # Setup + Portable
build/build-macos.sh         # .app + Portable + DMG
.github/workflows/build.yml  # CI/CD
docs/                        # u. a. dieser Blueprint
```

## 2. Versionierung (Vorgabe)

| Kanal | Branch | Format |
|---|---|---|
| Release | `main` | `<major>.<minor>.<buildnumber>-<builddate>` |
| Nightly | `dev` | `nightly-<buildnumber>-<builddate>` |
| Local | lokal / PR | `local-<builddate>` |

- Die Logik steckt komplett in `Directory.Build.props` und wird über `-p:QnChannel=release|nightly|local -p:QnBuildNumber=<n>` gesteuert. Ohne Parameter entsteht automatisch `local-<datum>`.
- `Version` (numerisch, z. B. `1.0.42`) und `InformationalVersion` (der Anzeige-String) sind getrennt. Windows braucht eine numerische Version, angezeigt wird der String.
- `IncludeSourceRevisionInInformationalVersion=false`, sonst hängt .NET `+<commit-hash>` an.
- Die Build-Nummer ist `github.run_number`, sie zählt automatisch hoch.
- Die App liest ihre Version über `AssemblyInformationalVersionAttribute` (siehe `VersionInfo.cs`) und zeigt sie an. Das Setup macht es genauso.

## 3. Git-Regeln (gelten für alle Projekte)

- Branches: `main` (Release) und `dev` (Nightly). Entwickelt wird auf `dev`, für ein Release wird nach `main` gemergt.
- **Commits nur unter dem eigenen Namen.** Keine `Co-Authored-By`-Zeilen, keine fremden Namen in Commits oder Branch-Namen.
- `user.name` und `user.email` pro Repo setzen (`git config user.name ...`), damit nichts unter einer falschen Identität landet.
- Pushen nur auf ausdrücklichen Wunsch.

## 4. Architektur-Regeln (C#)

- **Microsoft C# Coding Guidelines**, File-scoped Namespaces, sprechende Namen für Methoden und Variablen, wenig Verschachtelung (Early Return statt `if`-Pyramiden).
- **Modelle sind reine Daten**, ohne Logik und ohne Datei- oder Datenbankzugriff.
- **Business-Layer nur aus statischen Klassen**, jede mit genau einer Aufgabe (`NoteLibrary`, `NoteContent`, `NoteEncryption`, `SettingsStore` …).
- **Keine Datenbank- oder Dateizugriffe in Modellen oder UI.** ViewModels rufen ausschließlich den Business-Layer auf.
- **Berechtigungen immer im Business-Layer prüfen** (`AccessGuard`), auch wenn die UI den Button schon ausblendet. Muster:
  - Jede schreibende Business-Methode beginnt mit `AccessGuard.EnsureXyz(...)`.
  - Die App fasst nur ihre eigenen Dateitypen an (Whitelist der Endungen).
  - Für geschützte Ressourcen gilt: Entsperrt-Status liegt im Business-Layer (`EncryptionSession`), nicht in der UI.
- **Dialoge** stellt das Fenster als Delegates bereit (`Func<string, Task<bool>> Confirm`). Das ViewModel entscheidet nur, *wann* ein Dialog nötig ist, und bleibt so testbar.
- **Isoliertes Testprofil** über eine Umgebungsvariable (`QUICKNOTE_PROFILE`). So lässt sich die App testen, ohne echte Einstellungen oder Daten anzufassen.

## 5. Avalonia-Muster

- Version **pinnen** (hier 11.3.x). Avalonia 12 ist ein neuer Major mit API-Brüchen, also bewusst entscheiden, nicht `*` nehmen.
- Ein Style-File (`Styles/<App>Styles.axaml`) enthält Farben als Brushes, Icons als `StreamGeometry` (Lucide/Feather-Pfade, Stroke-Icons) und Button-Klassen (`primary`, `ghost`, `icononly`).
- **Bindings auf möglicherweise `null`-Objekte** (z. B. `ActiveDocument.X`) brauchen bei `IsVisible` immer `FallbackValue=False`, sonst ist das Element sichtbar.
- **Responsives Layout:** `SplitView` mit `DisplayMode` Inline (breit) bzw. Overlay (schmal). Die Breite meldet das Fenster über `BoundsProperty` an das ViewModel.
- **Tastenkürzel** im Code-Behind mit `PlatformSettings.HotkeyConfiguration.CommandModifiers`. Damit gilt automatisch Strg auf Windows und ⌘ auf macOS.
- **Single Instance** per Named Pipe (`SingleInstance.cs`): Der zweite Start übergibt seine Dateien an den ersten und beendet sich.
- **macOS „Öffnen mit“** kommt nicht über `args`, sondern über `IActivatableLifetime` und `FileActivatedEventArgs`.
- Drag & Drop in 11.3 über `e.DataTransfer` mit `TryGetFiles()` (das alte `e.Data` ist obsolet).
- `Markdown.Avalonia` funktioniert ohne `StyleInclude`. Das Include aus der Doku bricht den Build.

## 6. Logo und Icons

- Ein Logo in **mehreren Detailstufen** als SVG: `full` ab 128 px mit Verläufen und Schatten, `medium` für 32 bis 64 px, `small` für 16 bis 24 px mit wenigen, kräftigen Formen. Dazu eine Wordmark und ein eigenes **Datei-Icon** für den eigenen Dateityp.
- `tools/IconGen` rendert jede Zielgröße aus der passenden Stufe (SkiaSharp + Svg.Skia).
- **ICO-Lektion:** Einträge bis 128 px als **klassische 32-Bit-BMP** (mit AND-Maske) schreiben, nur 256 px als PNG. Reine PNG-ICOs zeigt Windows nicht überall an. Das war der Grund für „Die Exe hat kein Icon“.
- ICNS für macOS aus PNG-Chunks (`icp4` … `ic10`).
- Exe-Icon über `<ApplicationIcon>` setzen, Fenster-Icon über `Icon="avares://…/x.ico"`.
- Das Datei-Icon bleibt eine **separate Datei** neben der Exe (`ExcludeFromSingleFile="true"`), damit die Registry darauf zeigen kann.

## 7. Eigenes Setup (Windows)

- Ein Avalonia-Fenster ohne Systemrahmen (`ExtendClientAreaToDecorationsHint`), links ein Markenpanel mit Verlauf, rechts Seiten: Optionen, Fortschritt, Fertig.
- **Payload:** die veröffentlichte App als Zip, eingebettet als `EmbeddedResource` (`-p:PayloadPath=…`).
- **Pro Benutzer** nach `%LocalAppData%\Programs\<App>`, ohne Adminrechte. Die Registry liegt komplett unter `HKCU`.
- Das Setup kopiert sich selbst als `Uninstall.exe` und trägt sich unter `Software\Microsoft\Windows\CurrentVersion\Uninstall\<App>` ein. `/uninstall` startet den Entfernen-Modus.
- Dateitypen:
  - Eigene Endung: per ProgId mit Datei-Icon und `shell\open\command` übernehmen.
  - Fremde Endungen (`.md`, `.txt`): nur über `OpenWithProgids` ins „Öffnen mit“-Menü, **nicht kapern**.
  - Danach `SHChangeNotify(SHCNE_ASSOCCHANGED)`, damit der Explorer sofort neue Icons zeigt.
- **Sicherheit:** Einen vorhandenen Zielordner nur löschen, wenn er die eigene Exe enthält. Fremde Ordner nie anfassen.
- Verknüpfungen über `WScript.Shell` mit gesetzter `IconLocation`.

## 8. Build und CI/CD

- `Build-Windows.ps1`:
  1. `dotnet publish` self-contained, Single-File, `IncludeNativeLibrariesForSelfExtract`, `EnableCompressionInSingleFile`
  2. Portable-Zip
  3. Das Zip als Payload ins Setup
- `build-macos.sh`: `.app`-Bundle mit `Info.plist` (Dokumenttypen, Icons), Ad-hoc-`codesign`, Portable-Zip über `ditto`, DMG über `hdiutil` mit Link auf `/Applications`.
- Workflow-Jobs:
  1. `prepare`: berechnet Kanal und Version aus Branch und Event
  2. `test`
  3. `windows` und `macos` (Matrix arm64/x64)
  4. `publish`: `gh release create`, auf `main` als Release, auf `dev` als Pre-Release
- `permissions: contents: write` ist für Releases nötig.
- Lokal unter Windows: `powershell -ExecutionPolicy Bypass -File build/Build-Windows.ps1`.
- Bei neuen Repos prüfen, ob GitHub Actions aktiviert ist. Der erste Push hat hier keinen Lauf ausgelöst.

## 9. Verschlüsselung (wiederverwendbar)

- AES-256-GCM, Schlüssel per PBKDF2-SHA256 mit 600 000 Iterationen. Dateiformat: `Magic | Iterationen | Salt | Nonce | Tag | Ciphertext`, der Magic-String dient zusätzlich als AAD.
- Für jedes Speichern eine **neue zufällige Nonce**. Den abgeleiteten Schlüssel nach dem Entsperren im Speicher cachen, damit Autosave nicht jedes Mal 600 000 Iterationen rechnet. Das **Kennwort selbst nie** speichern.
- Ein falsches Kennwort zeigt sich als Tag-Mismatch. Daraus wird eine eigene `InvalidPasswordException`.
- Verschlüsselte Dateien **atomar** schreiben: erst `.tmp`, dann `File.Move(overwrite)`.
- Das Entsperren (PBKDF2) mit `Task.Run` vom UI-Thread fernhalten.

## 10. Verifikation vor „fertig“

- Unit-Tests für Business-Layer **und** Berechtigungen. Auch die Negativfälle testen: gesperrt, falsches Kennwort, fremder Dateityp.
- Die **veröffentlichte** Exe starten, nicht nur `dotnet run`.
- UI-Check ohne Klicken: Die App mit Testprofil und Datei-Argumenten starten und das Fenster per `PrintWindow` fotografieren (`build/Capture-Window.ps1`). Das klappt auch, wenn das Fenster verdeckt ist.
- Prüfen, ob das Icon wirklich in der Exe steckt: `[System.Drawing.Icon]::ExtractAssociatedIcon(...)`.

## 11. Checkliste für ein neues Projekt

1. Skelett aus Abschnitt 1 anlegen, `VERSION` und `Directory.Build.props` kopieren und Präfix bzw. Namen anpassen.
2. `git config user.name/user.email` setzen, Branches `main` und `dev` anlegen.
3. Logo-SVGs in drei Stufen anlegen, `IconGen` laufen lassen.
4. Core: Modelle, statische Business-Klassen, `AccessGuard`, Tests.
5. App: Styles, MainWindow, ViewModels, Dialog-Delegates, Shortcuts, Testprofil-Variable.
6. Setup: Installer-Projekt mit Payload, Registry-Einträge, Uninstall.
7. Build-Skripte und Workflow übernehmen, Namen anpassen.
8. Veröffentlichten Build starten, Screenshots prüfen, Icons prüfen.
9. README mit Versionierung, Bauen und Tastenkürzeln.
