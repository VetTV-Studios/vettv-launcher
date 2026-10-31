# MSI GL66 test plan (Windows build 26340)
Build: Visual Studio 2022 17.8+, x64, `src/VetTV.Launcher.App` (Community build is the default; Store build: `/p:VetTVEdition=Store`).
Before shell-mode tests: create a second local admin account and a restore point (see docs/RECOVERY.md).

## A. M1 core
1. App builds; `VetTV.Launcher.Watchdog.exe` is in the output folder.
2. Opens full screen; Steam and Xbox installed games appear; source errors (if any) shown in status.
3. Controller: D-pad/stick moves with auto-repeat; A launches a Steam game and an Xbox game; LB/RB switch tabs.
4. Start on sign-in: toggle on → sign out/in → launcher opens. Toggle off → doesn't.
5. Recovery (shell mode needs Pro or a Store build to *enable*; recovery works in all builds): Exit to Desktop; Ctrl+Alt+Shift+F12; hold View+Menu 3 s; kill the launcher 3× within 2 min → Explorer returns and registry is restored; Shift held at sign-in; `SAFE_MODE` file.

## B. PlayStation hub (Community build only)
Setup: USB DVD drive connected; note its drive letter. Use only your own discs and your own BIOS dumps.
1. **Store build check:** build with `/p:VetTVEdition=Store` → no "PlayStation" tab. Community build → tab present.
2. **Emulator manager:** for RetroArch, Beetle PSX, PCSX2, RPCS3 press Install/Update → a dialog shows version + official URL; Cancel downloads nothing; Install downloads, verifies checksum (if published), extracts to `%LOCALAPPDATA%\VetTV\Emulators`. Record whether Windows `tar` extracts the `.7z` files on build 26340 (VERIFY).
3. **BIOS:** choose your PS1 BIOS → "PS1 BIOS OK: scph550x" (or "not recognised" with md5 for other dumps). Copied to RetroArch `system\`. For PS2, set the BIOS inside PCSX2's setup wizard.
4. **Disc detection:** with the hub open, insert a PS2 disc → within ~5 s the title/serial appears (e.g. "PS2: Final Fantasy X (SLUS-20312)"); eject → "No disc". Repeat with a PS1 disc, an audio CD ("Not a PS1/PS2 game disc"), and with a disc already inserted before launching the app. Test once without PCSX2 installed (serial only, no title).
5. **PS2 from disc:** A on "Play disc" → PCSX2 starts fullscreen from the drive (`-disc E:`). Play 5 min, note stutter on loads; quit → back to the launcher.
6. **PS1 from disc:** "Play disc" → RetroArch + Beetle PSX with `cdrom://e:/drive1.cue`. Record: works / RetroArch quits (hub should then suggest the copy fallback) / other error. If the HW renderer is Vulkan and the disc fails, switch Beetle to OpenGL and retry.
7. **PS1 image-copy fallback:** "Copy PS1 disc to image, then play" → progress %, creates `%LOCALAPPDATA%\VetTV\PlayStation\Images\<serial>.bin/.cue`, then starts. Test a multi-track disc (CD audio) and confirm music plays. Note copy time.
8. **PS3:** add a folder with your own disc dump (`<Game>\PS3_GAME\...`) → game appears with its title; install PS3 firmware via the "PS3 firmware (Sony)" link + RPCS3; launch → RPCS3 `--no-gui --fullscreen`. Insert a PS3 disc → hub says it can't play it from disc. PSN installs in RPCS3's `dev_hdd0\game` appear too.
9. Controller-only pass through the whole hub (focus reaches every button; A activates).
Report each item as pass/fail with notes and the emulator versions.
