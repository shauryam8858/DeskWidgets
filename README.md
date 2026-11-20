# DeskWidgets

A lightweight, offline Windows 10 desktop dashboard: **digital clock + monthly calendar + countdown** in one
translucent, rounded, macOS-inspired glass panel. Built with C#, Windows Forms and .NET 8. No internet, no account,
no telemetry.

> **Honest status (please read):** this project was written and reviewed on a Linux machine that had no .NET SDK
> and no Windows. It has **never been compiled, run, or tested on Windows** by the author. The first GitHub Actions
> build is the first real compile. Section 9 lists exactly what is untested. Nothing here has been measured on your
> Dell Inspiron; every performance number below is an *engineering estimate*, not a measurement.

---

## 1. What you get

* Clock (12/24 hour, optional seconds, weekday + date), calendar (month navigation, today and selected-date
  highlight, Today button, Sunday- or Monday-first weeks) and a countdown (name, date, days/hours/minutes, completion message,
  edit, delete, saved permanently).
* Transparency slider 0-100 % with 25/50/75/100 presets and a live percentage.
* Drag to move, drag edges/corners to resize, 9 position presets + custom X/Y, margins, lock, Compact/Normal/Large sizes.
* Themes: Dark glass (default), Light glass, Midnight blue, plus custom background and accent colours.
* System tray icon with Show / Hide / Edit / Lock / Settings / Reset position / Exit.
* Optional "Start with Windows" (off by default, no administrator rights).
* Settings saved automatically to `%APPDATA%\DeskWidgets\settings.json`.

## 2. How the build works (nothing to install on your computers)

You never install .NET or Visual Studio. GitHub's free Windows computer builds the program for you:

1. You upload this folder to GitHub.
2. GitHub Actions runs `.github/workflows/build.yml`: installs the .NET 8 SDK, restores packages, builds, runs the
   automated tests, publishes a **self-contained single-file `DeskWidgets.exe`** (win-x64), and packages it as
   `DeskWidgets-win-x64.zip`.
3. You download the `DeskWidgets-win-x64` artifact, extract the ZIP, and copy `DeskWidgets.exe` to the Dell.

Self-contained means the .NET runtime is inside the exe, so the Dell needs nothing extra. The exe is large
(roughly 100-160 MB, estimated) because it carries the runtime and Windows Forms. Compression is deliberately off so
it starts faster and uses less memory on a 2 GB Celeron.

**About Windows 10 and .NET 8:** .NET 8 supports Windows 10 version 1607 and newer, 64-bit. That is a statement about
the runtime, not a guarantee that this app behaves identically on every Windows 10 setup. Keep Windows 10 updated. If
the exe refuses to start on the Dell, tell Claude the exact message.

## 3. Step-by-step: upload to GitHub (on your HP laptop)

1. Sign in at <https://github.com>. Click **+** (top right) -> **New repository**. Name it `DeskWidgets`.
   Choose **Private** (or Public), leave everything else untouched, click **Create repository**.
2. On the new empty repository page click **uploading an existing file**.
3. Unzip `DeskWidgets.zip` on your HP laptop. Open the unzipped `DeskWidgets` folder.
4. Select **everything inside** that folder (the `src` folder, the `tests` folder, `.github` folder, `DeskWidgets.sln`,
   `README.md`, `LICENSE`, `.gitignore`) and drag it into the GitHub upload box.
   * Important: the folder named **`.github`** starts with a dot and may be hidden. In File Explorer open the
     **View** tab and tick **Hidden items** so you can see it. If `.github` is missing, the build will not exist.
   * Drag **folders**, not only files, so the sub-folders are kept.
   * If the browser refuses to upload folders: install **GitHub Desktop** (free) and use *Add local repository /
     Publish repository* instead.
5. Scroll down and click **Commit changes**.
6. Check that your repository's main page shows `.github`, `src`, `tests`, `DeskWidgets.sln` and `README.md`.
   Click `.github` -> `workflows` -> you must see `build.yml`.

## 4. Step-by-step: run the build and download the exe

1. In your repository click the **Actions** tab. (If GitHub asks, click *I understand my workflows, go ahead and
   enable them*.)
2. Click **Build DeskWidgets** on the left, then **Run workflow** -> green **Run workflow** button.
   (It also runs by itself every time you upload changes.)
3. Wait about 3-8 minutes. A **green tick** = success. A **red cross** = failure.
4. Click the finished run. Scroll to **Artifacts** at the bottom, click **DeskWidgets-win-x64**. Extract
   `DeskWidgets-win-x64.zip` to find `DeskWidgets.exe`.
5. **If the run is red:** click the red step, copy the last 30-40 lines of the red error text, and paste them to
   Claude. Do not try to fix code yourself. This is expected on a first build of never-compiled code, and fixing
   compile errors is routine.

## 5. Install on the Dell Inspiron 11 (Windows 10)

1. Copy `DeskWidgets.exe` to the Dell (USB stick). Put it somewhere permanent, for example
   `C:\Users\<your name>\DeskWidgets\DeskWidgets.exe`. (Do not run it from the USB stick or Downloads if you want
   "Start with Windows", because that remembers the exact path.)
2. Double-click it. Windows SmartScreen may say "Windows protected your PC" because the exe is not code-signed:
   click **More info** -> **Run anyway**. (Only do this for a file you built yourself.)
3. The dashboard appears at the right-centre of the screen and a tray icon appears near the clock
   (click the small **^** arrow if it is hidden).
4. Click the gear icon on the dashboard, or right-click the tray icon -> **Open settings**.

## 6. Using it

| Action | How |
| --- | --- |
| Move | Drag any empty part of the dashboard |
| Resize | Drag an edge or corner (the cursor changes) |
| Change month | Arrows next to the month name; **Today** jumps back |
| Add/edit countdown | Click the countdown card, or the pencil icon |
| Delete countdown | The **x** icon on the countdown card |
| Settings | Gear icon, or tray -> Open settings |
| Show/hide | Tray menu, or **Ctrl+Alt+D** |
| Locked? | Tray -> Edit dashboard, or Settings -> Drag mode lets you move/resize anyway |

Every setting is saved automatically.

### Transparency (0-100 %)

The slider fades the glass panel linearly, and fades the text on a gentler curve so it stays readable
(25 % opacity -> text about 50 % visible). 0 % hides everything. **It can never trap you:** tray icon -> *Show dashboard*
raises opacity to 60 % if it was below 10 %, and Ctrl+Alt+D does the same. *Window opacity* (how visible the whole
panel is) is what this slider controls; the glass colour itself is fixed per theme.

### If the dashboard is off-screen or lost

Tray -> **Reset position**. On start-up and whenever the screen resolution changes, a dashboard that is not at least
partly visible is moved back to the primary screen automatically.

## 7. Desktop modes (Settings -> Position and layout -> Desktop mode)

| Mode | Behaviour | Status |
| --- | --- | --- |
| **Desktop** (default) | Sits just above the desktop icons and below every normal window. No taskbar button, never takes focus. | Untested |
| **WorkerW** | Attached to Explorer's hidden wallpaper window so it sits *behind* desktop icons (icons stay clickable on top). | Experimental, untested |
| **Normal window** | An ordinary window (no taskbar button) that other windows can cover. | Untested, simplest |
| **Always on top** | Floats above everything. | Untested |

The WorkerW trick relies on undocumented Explorer behaviour. It differs between Windows versions (it is known to work
differently on newer Windows 11 builds) and can break if Explorer restarts; DeskWidgets then falls back to Desktop mode and
tells you with a notification. **To switch modes:** Settings -> Position and layout -> Desktop mode. If anything looks wrong,
pick **Normal window**.

**WorkerW mouse warning:** in WorkerW mode the desktop icon window sits in front of the dashboard, so it may
receive all mouse clicks. The dashboard can then display but not respond to clicking, dragging or resizing. If so, switch
mode from the tray (Open settings -> Desktop mode) or use Reset position / Desktop mode.

Limitations: in Desktop mode the dashboard covers the desktop icons beneath it (WorkerW mode avoids that). Win+D
("show desktop") behaviour is not verified. Stacking order is re-applied every 3 seconds in case clicking the desktop changed it.

## 8. Uninstall

1. Tray icon -> **Exit**.
2. Optional clean-up: open **Command Prompt** in the folder containing the exe and run `DeskWidgets.exe --cleanup`.
   It removes the "Start with Windows" entry and deletes `%APPDATA%\DeskWidgets`.
   (Or untick *Start with Windows* in Settings, and delete that folder yourself.)
3. Delete `DeskWidgets.exe`. Nothing else was installed (no installer, no services, no admin rights).

## 9. Implemented features, limits, and what is untested

**Implemented (in code):** everything listed in section 1, plus: per-pixel-alpha layered window (soft shadow, clean
rounded edges), cached glass background (only text is redrawn each second, and only when the displayed text changes),
single instance (a second launch just shows the running one), atomic debounced settings saves, corrupt-settings recovery
(`settings.corrupt.json` is kept), crash log (`%APPDATA%\DeskWidgets\crash.log`), DPI-scaled drawing, multi-monitor aware
placement, Ctrl+Alt+D hotkey, automatic fonts/brush disposal.

**NOT verified - treat as untested until you try them on the Dell:**

* That the project compiles on the first try (never compiled by the author).
* Everything about real Windows behaviour: layered rendering, dragging/resizing feel, DPI changes between monitors,
  tray menu, hotkey, Start with Windows, all four desktop modes, Explorer restart handling.
* Visual quality (it was never seen on screen). Expect to iterate on spacing and fonts.
* Memory and CPU. The design aims for low idle use (no animations, one timer, no background threads, cached background,
  NLS instead of ICU, workstation non-concurrent GC). The goal of about 60 MB is an **aim, not a measurement**; a
  self-contained WinForms process may realistically sit higher. Check Task Manager -> Details -> DeskWidgets.exe on the Dell
  and report the numbers.
* Automated tests exist for calendar maths, countdown maths, settings serialization/repair and position geometry
  (`tests/DeskWidgets.Tests`). They run in GitHub Actions. They do not cover any window behaviour.

**Known simplifications:** the settings window uses standard Windows controls (sliders, date picker) coloured dark, so it
is consistent but not pixel-identical to the dashboard. The countdown counts down to 00:00 on the chosen date. Calendar years
1900-2200 are supported. Not code-signed. English UI only (month/day names follow Windows language).

## 10. Project layout

```
DeskWidgets/
  .github/workflows/build.yml     GitHub Actions build + test + publish
  DeskWidgets.sln                 Visual Studio solution
  src/DeskWidgets/
    Program.cs                    start-up, single instance, --cleanup
    Forms/                        DashboardForm (window), SettingsForm, CountdownDialog, UiStyle
    Controls/                     DashboardRenderer (all GDI+ drawing), ThemePalette, HitRegion
    Services/                     AppController, SettingsService, StartupService, DesktopIntegration (WorkerW)
    Models/                       AppSettings, SettingsSerializer
    Utilities/                    CalendarMath, CountdownMath, PositionMath, HexColor, NativeMethods, LayeredSurface
    Resources/app.ico
  tests/DeskWidgets.Tests/        xunit tests
```

## 11. Troubleshooting

| Problem | Try |
| --- | --- |
| Build red | Paste the last lines of the red step to Claude |
| Dashboard invisible | Tray -> Show dashboard (or Ctrl+Alt+D) |
| Off-screen | Tray -> Reset position |
| Behaves oddly on desktop | Settings -> Desktop mode -> Normal window |
| Nothing appears at all | Look in `%APPDATA%\DeskWidgets\crash.log`; send it to Claude |
| Reset everything | Settings -> General -> Reset all settings, or delete `%APPDATA%\DeskWidgets\settings.json` |

License: MIT.
