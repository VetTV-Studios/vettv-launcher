# VetTV Launcher

A full-screen, gamepad-first game launcher for Windows (WinUI 3 / .NET 8) by VetTV Studios.

**Status:** M1 preview: Steam and Xbox installed libraries, controller navigation, launching games, start on sign-in.

## Features (free, open source)
- One library for Steam (local `libraryfolders.vdf` + `appmanifest_*.acf`) and Xbox / Microsoft Store installed games. Epic, GOG and emulator folders are coming in M2.
- Controller-first navigation (D-pad/stick with auto-repeat, A to launch, B to go back).
- Start on sign-in.
- Shell-mode **recovery** is always included: if you use VetTV Pro's shell mode, the open-source watchdog, recovery hotkey (Ctrl+Alt+Shift+F12 or hold View+Menu for 3 s) and "Exit to Desktop" always get you back to Explorer. See [docs/RECOVERY.md](docs/RECOVERY.md).

VetTV Launcher Pro (a $4.99 Microsoft Store add-on with a 7-day free trial) adds shell mode and more. It is not part of this repository.

## Build
- **Core + tests (any OS):** `dotnet test VetTV.Launcher.sln` (.NET 8 SDK).
- **App (Windows):** open `src/VetTV.Launcher.App` in Visual Studio 2022 17.8+ with the Windows App SDK / WinUI workload and build x64.

## Layout
- `src/VetTV.Launcher.Core`: models, library providers, input mapping, shell recovery, start on sign-in.
- `src/VetTV.Launcher.Watchdog`: shell-mode watchdog that falls back to Explorer.
- `src/VetTV.Launcher.App`: the WinUI 3 app.
- `tests/`: xUnit tests.

## Security
Store sign-in tokens are kept in Windows Credential Locker, never in code or logs.

## License
Code: Apache-2.0 (see [LICENSE](LICENSE) and [NOTICE](NOTICE)). The VetTV name, logos and artwork are not covered by the license. Inspired by launchers like Playnite; contains no Playnite code or art.
