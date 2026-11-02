# VetTV Launcher: MSI test checklist

Machine: MSI Pulse GL66, Windows 11 build 26300.9818. Use only your own games, discs and BIOS dumps.
For every step, reply with: **step number, pass/fail, and the note asked for**. A photo of the screen is fine.

## A. Install and build (one time)
1. **Install** these on the MSI (nothing else is needed):
   - .NET 8 SDK x64: `winget install Microsoft.DotNet.SDK.8`
   - Git: `winget install Git.Git`
   - Windows App SDK 1.6 runtime: `winget install Microsoft.WindowsAppRuntime.1.6`
   Then close and reopen the terminal. Expect: `dotnet --list-sdks` shows an 8.0.x line. Report: that line.
2. **Get the code:** copy the `vettv-launcher` folder Grok Bot gives you to `C:\Dev\vettv-launcher` (it has local changes that aren't on GitHub yet), then `cd C:\Dev\vettv-launcher`.
3. **Run the tests:** `dotnet test VetTV.Launcher.sln`. Expect: "Passed!" with 0 failed. Report: the total count.
4. **Build the app (Community build, includes the PlayStation hub):**
   `dotnet build src\VetTV.Launcher.App\VetTV.Launcher.App.csproj -c Release -p:Platform=x64`
   Expect: "0 Error(s)". Report: the error/warning lines if it fails (this is the first-ever Windows build).
5. **Store build check:** run the same command with `-p:VetTVEdition=Store` added. Expect: 0 errors. Report: pass/fail.
6. **Start it:** run `src\VetTV.Launcher.App\bin\x64\Release\net8.0-windows10.0.22621.0\win-x64\VetTV.Launcher.App.exe` (from the step 4 build).
   Expect: full screen, a game grid, a status line like "N games". `VetTV.Launcher.Watchdog.exe` is in the same folder. Report: the status line text.

## B. Game libraries
Install at least one free game in each launcher first (and note which).
1. **Steam:** your installed Steam games appear. A on one starts it through Steam. Report: missing or extra games (e.g. tools/redistributables).
2. **Epic Games Launcher:** your installed Epic games appear (not DLC, not half-downloaded games). A starts the game through Epic. Report: does it start straight away or only open Epic?
3. **GOG Galaxy:** your installed GOG games appear (not DLC). A starts the game through Galaxy. Then quit Galaxy completely (tray icon > Exit) and try again. Report: works with Galaxy? Without Galaxy running?
4. **Heroic:** install Heroic (heroicgameslauncher.com), sign in, install one Epic and one GOG game (and an Amazon game if you have one) in Heroic only. Restart VetTV. Expect: they appear; A starts them through Heroic. Report: titles shown correctly? Did each one start?
5. **No duplicates:** install the same game in both Epic and Heroic (or GOG Galaxy and Heroic) if possible. Expect: it shows once and launches through Epic/GOG. Report: shown once?
6. **Soft failure:** uninstall or close any launcher; VetTV still opens and shows the others. Report: any "source error" in the status line.
7. **Controller:** D-pad/stick moves with auto-repeat, A launches, LB/RB switch tabs. Report: anything that feels wrong.

## C. PlayStation hub (Community build only)
Connect the USB DVD drive and note its letter (e.g. E:).
1. **Tab:** the Community build has a "PlayStation" tab; the Store build (A5) doesn't. Report: pass/fail.
2. **Emulators + .7z unpacking:** in the hub, press Install/Update for RetroArch, Beetle PSX, PCSX2 and RPCS3. Expect: a dialog with version + official URL each time; Cancel downloads nothing; Install unpacks into `%LOCALAPPDATA%\VetTV\Emulators`. Windows' built-in `tar` unpacks the `.7z` files. Report: which installed, and any ".7z"/"tar" error.
3. **BIOS hash check:** pick your PS1 BIOS file. Expect: "PS1 BIOS OK: scph…", or "not recognised" plus an MD5 for an unknown dump. Report: the exact text. (PS2 BIOS is set in PCSX2's own setup wizard.)
4. **Disc detection (raw CD read):** insert a PS1 disc, then a PS2 disc, then an audio CD. Expect within ~5 s: "PS1: <title> (SLUS-xxxxx)", "PS2: …", and "Not a PS1/PS2 game disc". Eject: "No disc". Also try starting VetTV with a disc already in. Report: the text shown for each.
5. **PS1 from disc (cdrom://):** PS1 disc in, press "Play disc". Expect: RetroArch + Beetle PSX starts full screen from the disc. Report: works / RetroArch closes / error. If it fails: in RetroArch set Beetle's renderer to OpenGL and retry; report that result too.
6. **PS1 copy fallback:** "Copy PS1 disc to image, then play". Expect: progress %, then `<serial>.bin/.cue` in `%LOCALAPPDATA%\VetTV\PlayStation\Images`, then the game starts. Use a disc with CD music if you have one. Report: copy time and whether music plays.
7. **PS2 from disc:** PS2 disc in, "Play disc". Expect: PCSX2 starts full screen from the drive. Play 5 minutes, then quit. Expect: back in VetTV. Report: stutter on loads?
8. **RPCS3:** install PS3 firmware using the "PS3 firmware (Sony)" link inside RPCS3. Add a folder with your own PS3 game dump. Expect: it appears with its title; A starts RPCS3 full screen. A PS3 disc in the drive shows "can't play from disc". Report: pass/fail and the RPCS3 version.
9. **Controller only:** go through the whole hub with the pad. Report: any button you can't reach.

## D. Start on sign-in
1. Turn "Start on sign-in" on, sign out and back in. Expect: VetTV opens. Turn it off, repeat. Expect: it doesn't. Report: pass/fail.

## E. Recovery (do this before any shell-mode test)
First make a second local admin account and a restore point (see docs/RECOVERY.md).
Shell mode needs Pro to switch on; these recovery paths work in every build.
1. **Exit to Desktop** button → Explorer/desktop appears. Report: pass/fail.
2. **Ctrl+Alt+Shift+F12** → same. Report: pass/fail.
3. **Hold View+Menu for 3 s** on the pad → same. Report: pass/fail.
4. **Watchdog:** with shell mode on, end VetTV in Task Manager 3 times within 2 minutes. Expect: Explorer comes back and shell mode is turned off. Report: pass/fail and `%LOCALAPPDATA%\VetTV\Launcher\watchdog.log`.
5. **Shift at sign-in** and the empty **`SAFE_MODE`** file in `%LOCALAPPDATA%\VetTV\Launcher` → Explorer starts instead of VetTV. Report: pass/fail for each.

## F. Xbox mode home-app picker (Windows feature, not a game library)
1. Open Settings > Gaming > Xbox mode (it may still be called "Full screen experience"). Report: does the page exist, and is there a "Choose home app" option or only an on/off toggle? A screenshot is best. (VetTV won't be listed yet: that needs the MSIX build in M5. This step only checks whether the MSI offers the picker.)
