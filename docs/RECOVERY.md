# VetTV Launcher — Shell mode recovery

Shell mode sets **`HKCU\Software\Microsoft\Windows NT\CurrentVersion\Winlogon\Shell`** to
`"<install dir>\VetTV.Launcher.Watchdog.exe"` for **your user only**. Explorer is not removed; it just isn't started at sign-in.
The machine-wide default (`HKLM\...\Winlogon\Shell = explorer.exe`) is never touched.

## Built-in ways back (in order of preference)
1. **Exit to Desktop** button (or press Menu on the pad to jump to it, then A). Starts Explorer for this session; shell mode stays on for the next sign-in.
2. **Recovery hotkey:** `Ctrl + Alt + Shift + F12` (keyboard) or **hold View + Menu for 3 seconds** (gamepad). Same as Exit to Desktop and works even if the UI focus is stuck.
3. **Turn shell mode off:** switch off "Shell mode" in the launcher. The registry value is removed (or your previous custom shell restored) and Explorer is the shell again from the next sign-in.
4. **Watchdog (automatic):** if the launcher crashes, fails to start, or exits within 10 s, the watchdog restarts it. After **3 failures within 2 minutes** it **restores the registry and starts explorer.exe**. Log: `%LOCALAPPDATA%\VetTV\Launcher\watchdog.log`.
5. **Safe mode at sign-in:** hold **Shift** while signing in, or create the empty file `%LOCALAPPDATA%\VetTV\Launcher\SAFE_MODE`. The watchdog then restores Explorer instead of starting the launcher.

## Manual recovery (if everything above fails)
1. Press **Ctrl + Alt + Del** → **Task Manager** → **Run new task** → type `explorer.exe` → OK. The desktop comes back for this session.
2. Undo shell mode permanently: Run new task → `cmd` (no admin needed) and run:
   ```
   reg delete "HKCU\Software\Microsoft\Windows NT\CurrentVersion\Winlogon" /v Shell /f
   ```
   Sign out and back in.
3. If you can't sign in to a desktop at all: sign in as **another (admin) account**, open `regedit`, **File → Load Hive…** `C:\Users\<you>\NTUSER.DAT` (name it `VetTV`), delete `HKEY_USERS\VetTV\Software\Microsoft\Windows NT\CurrentVersion\Winlogon\Shell`, then **Unload Hive**.
4. Last resort: Windows Recovery (hold Shift + Restart) → Troubleshoot → System Restore, or Safe Mode and step 3.

**Before first enabling shell mode on the MSI:** make sure a second local admin account exists and create a restore point.
