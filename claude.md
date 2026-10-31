# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Solution

`src/PlatformHeads.slnx`

## Build and Test Commands

```bash
# Build (from repo root). Also packs: ProjectDefaults sets GeneratePackageOnBuild in Release.
dotnet build src --configuration Release

# Run all tests, which is what CI runs
dotnet test --solution src/PlatformHeads.slnx --configuration Release --no-build --no-restore

# Or run the test project directly, which takes a filter after it
src/PlatformHeads.Tests/bin/Release/net10.0/PlatformHeads.Tests.exe --treenode-filter "/*/*/ClassName/*"

# Build the sample head the way the workflow does. Docker Desktop on this machine shares only D:\Code
docker run --rm -v "$PWD:/src" -w /src -e LIBRARY=sample_ui -e NATIVE=sample/native "quay.io/pypa/manylinux_2_28_$(uname -m)" bash native/build-linux.sh
```

**Test runner:** TUnit on Microsoft.Testing.Platform. Filters are treenode paths after the executable, or after `--` with `dotnet test`. `--nologo` makes a run report zero tests.

## What this is

The shared half of the head architecture DiffEngineViewer (`D:\Code\VerifyTests\DiffEngine`) and BuildMonitor (`D:\Code\BuildMonitor`) both use: an immutable state projected into a `Screen`, drawn by a WinForms head on Windows and by a native library behind an app's own C ABI on macOS and Linux. Each app had its own copy of the plumbing, and fixes made in one did not reach the other. The plumbing moves here a layer at a time. What an app draws, and the structs of its ABI, stay in the app.

Layer 2 is the managed kernel, `src/PlatformHeads.Core`, packed as `PlatformHeads.Core`: `SessionHost<TState>`, `ScreenCache<TState, TScreen>`, `IHeadWindow<TScreen, TInput>` and `OpenWindow<TWindow>`, `Pacing` and `LoopWake<TState>`, `ClipboardPump`, `WindowPlacement`, `PlacementSettler` and `NativeResolver`. Each was a copy in both apps, or in BuildMonitor and missing from DiffEngine. The frame loop itself stays in each app, since most of it is the app's own. Unlike the apps' code it is in a namespace, `PlatformHeads`, and public, since it is a library; an app takes it with a global using. It is signed with the `key.snk` most of these repositories share, because DiffEngine is signed. The tests are in `src/PlatformHeads.Tests/Core`.

Layer 3 is the WinForms shell, `src/PlatformHeads.WindowsForms`, packed as `PlatformHeads.WindowsForms`: `HeadForm`, the base of each app's form, with `WindowFit`, `MessageWait`, `FormsApp` and `FormsClipboard`. It is `net10.0` with `UseWindowsForms` and the `TargetPlatformIdentifier` workaround the heads have, built with the WindowsDesktop SDK, and keeps the WinForms framework reference out of its package (`KeepWindowsFormsOutOfThePackage`): declared by a package, the SDK refuses a `net10.0` head that takes it, and moving `TargetPlatformIdentifier` before that check does not help, since MSBuild decides whether to run a target before its `BeforeTargets`. Its tests are `src/PlatformHeads.WindowsForms.Tests`, `net10.0-windows`, which the `Release-NotWindows` configuration leaves out, and which `build.yml` uses off Windows. A wait's length cannot be tested on a thread with windows, since any message for the thread ends it: `MessageWaitTests` waits on a thread of its own.

Layer 1 is build and packaging, and ships in three forms an app pins at one tag:

- `src/PlatformHeads/build/` is the NuGet package: MSBuild props and targets, no assembly. `PlatformHead` items name a head project for each runtime identifier, and packing publishes and bundles them. Packed into both `build` and `buildMultiTargeting`, since DiffEngine has several target frameworks and packs from its outer build.
- `cmake/PlatformHeads.cmake` is fetched by an app's `native/CMakeLists.txt` with `FetchContent`. It pins raylib and Dear ImGui and carries the raylib 6.0 option workarounds and `--as-needed`. The repository root must never gain a `CMakeLists.txt`, or `FetchContent_MakeAvailable` would start building it.
- `.github/workflows/build-native.yml` is a reusable workflow. `sample-native.yml` calls it for `sample/native`, which is how a change to any of the three is run for real before an app takes it. The macOS job has no sample and is first run by an app.

Layer 4 is the native runtime, in two halves an app pins at the same tag as the rest:

- `native/runtime` is C++, what a raylib and Dear ImGui head does underneath its drawing, in namespace `ph` behind `include/platformheads/runtime.h`: the window, GLFW's input as it happened, which frames are built and drawn, the renderer, the fonts and fontconfig, and the string and label helpers. `platformheads_add_library` compiles it into the app's library and links `Threads` and `dl`. A toolkit and not a framework: each app keeps its own present, poll, capture and state, since most of each is its own, and calls these where it did the same itself. It was DiffEngine's, the more worked out of the two, and BuildMonitor took it. The sample compiles and links it, since `platformheads_add_library` adds it to every library, so `sample-native.yml` builds it on the manylinux floor for both Linux architectures. That workflow's `run` job also runs it, on the runner's own Ubuntu under Xvfb: `sample/native/run/run.sh` builds `sample_run` (the CMake option `SAMPLE_RUN`, off for build-native), which opens the runtime's window, rests through a second of frames and closes it, then opens it again and kills the X server under it, failing unless `OnDisplayLost` was called and Xlib ended the process with 1. Run it with a callback that is never registered and it fails. Everything else the runtime does, the drawing, the input and the fonts, the apps' pixel tests are what run, in the `ubuntu:24.04` container their `unix` jobs describe, with `-DFETCHCONTENT_SOURCE_DIR_PLATFORMHEADS` pointed at a copy of this working tree.
- `PlatformHeadsMac` is the Swift package an AppKit head depends on, at the repository root because SwiftPM looks for `Package.swift` nowhere else, with its sources and tests under `native/swift`. Pieces each app glues in, as the C++ is: `HeadWindow`, `EventPump`, `TerminateGuard`, `NapGuard`, `KnobScroller` and `ScrollerTravel`, `WheelNotches` and `Steps`, `ToolTipRegions`, `StringBlob`, `Capture`, `Clipboard`, `WindowPlacement`, and `StatusItem` with `StatusMenuItem` from layer 6. AppKit is only on macOS, so the `swift` job of `build.yml` is the only place it compiles before an app takes a tag, and `publish` waits on it. An app's dependency on it is its sixth pin.

Layer 5 is what both apps' Windows trays do, and is small, because that is all they share: BuildMonitor's tray is a model drawn by three back ends, DiffEngineTray is a WinForms app with an imperative menu, and the DiffEngine viewer has none. In `PlatformHeads.WindowsForms`, `MenuOnOpening` builds a `ContextMenuStrip`'s items on every open beside the ones it keeps, takes them off as it closes and disposes them at the next open, and lets an empty strip's first open through; `SessionEndWindow` hears a session ending for an app with no form, as `HeadForm.SessionEnding` does for one with. In `PlatformHeads.Core`, `IssueLink` encodes a new issue's title and body and remembers which messages were raised this run, and `LinkOpener` opens a URL on any desktop and says, rather than throws, when it could not. BuildMonitor's `TrayMenu` is a type of its own, which is why the menu one is not called that: a type in an app's global namespace wins over the package's.

Layer 6 is the tray on every platform, which was BuildMonitor's and is shared so DiffEngineTray can run on macOS and Linux. The model is in `PlatformHeads.Core`: `TrayModel<TIcon>` (an app's own icon enum, its tooltip and its menu), `TrayMenuItem` (with `Checked` and `Children`, and equal by value, submenus included, because a back end compares the menu it is handed with the one it shows), `TrayInput`, `TrayNotification` and `ITray<TIcon>`, beside `DesktopNotification` (`notify-send` and `osascript`) and run at login (`IRunAtLogin`, `RunAtLogin.ForPlatform(LoginItem)`, `WindowsRunKey`, `XdgAutostart`, `LaunchAgent`). The model is in Core rather than in a tray package so the Windows heads, DiffEngine's viewer among them, never carry D-Bus. The back ends are one a platform: `NotifyIconTray<TIcon>` in `PlatformHeads.WindowsForms`, with `TrayMenuItems` building its ToolStrip items, a theme and style hook for an app that colours its menus, `LeftClickOpensMenu` for an app whose menu is all its icon has, and `Handle`, the icon's own window, for an app that registers hot keys against it (DiffEngineTray's, read from a field WinForms does not promise, which `NotifyIconTrayTests` holds to); `TrayNotificationKind.Warning` is the third balloon icon; `StatusNotifierTray<TIcon>` in a package of its own, `PlatformHeads.Linux`, a StatusNotifierItem and dbusmenu over `Tmds.DBus.Protocol`, which is strong-name signed, so it can be referenced from here; and `StatusItem` in PlatformHeadsMac, which an app's native library drives, since the managed side has no AppKit. An app passes in its names and pictures (`NotifyIconOptions`, `StatusNotifierOptions`). `StatusNotifierTrayTests` runs the Linux tray over a private `dbus-daemon` with `StandInWatcher` as the panel, and fails without `dbus-daemon` rather than skipping, which is why `build.yml` installs `dbus`. `DbusMenuLayout` is public so its numbering is tested without a bus.

Layer 7 is updating an app installed as a dotnet tool, which was BuildMonitor's and is shared so DiffEngineTray can update on macOS and Linux. In `PlatformHeads.Core`, `ToolUpdate` starts a shell and the app exits once `Start` returns. The shell waits up to thirty seconds for the app to exit, runs `dotnet tool update {package} --tool-path {the shim's folder} --prerelease`, writes `ok` or `failed` and the output to the app's outcome file, and starts the shim. `ToolUpdateOutcome` reads that back once, for the app it started to report, with `Reason` the last line `dotnet tool update` wrote.

- On Windows the shell is Windows PowerShell, run from the temp folder. Before it updates, it stops what still runs from the store or from `AlsoStopUnder` (an app's copy of its head).
- On Linux it is `/bin/sh` under `setsid`.
- On macOS it is `/bin/bash` with `set -m`, the script a background job, so it is in a process group of its own: launchd ends a login item's process group as the item exits.
- Its arguments are positional, so no path is quoted into it.
- `--tool-path` rather than `--global`. The folder of a global install is a tool path too, and the update treats it as `--global` does (seen on Windows with a temp `DOTNET_CLI_HOME`, and on Linux), and it is the only form that reaches an install made with `--tool-path`.
- The dotnet is the muxer of the runtime the app runs on (`ToolUpdate.Dotnet`), since a launch agent's PATH is `/usr/bin:/bin:/usr/sbin:/sbin`.

Both of those macOS facts were seen on a GitHub macOS runner, by launch agents that started a child and exited. A plain child was ended, and one under `set -m` was not. So was the child of a plist with `AbandonProcessGroup`, which `LaunchAgent` now writes, so the windows and tools an app started also outlive it. A plist written before that keeps its group until "Run at startup" is set again; the update's own process group covers that. `ToolUpdateTests` runs each platform's shell for real, with scripts standing in for dotnet and the shim, which is why `build.yml` has a `macos` job that runs only those tests.

A session ending is heard four ways, since nothing an app runs on a clean exit runs when one ends it: `HeadForm.SessionEnding` and `SessionEndWindow` on Windows; `SessionEndSignals` in Core for the SIGTERM and SIGHUP systemd sends a session's processes, which .NET otherwise ends a process on with no `finally` and no `ProcessExit`; and `ph::OnDisplayLost` in the runtime for the X server going, which is what usually comes first, since Xlib's own handler then calls `exit(1)`. It is looked up with `dlopen` and `RTLD_NOLOAD` rather than linked, and the libX11 of the manylinux container is older than `XSetIOErrorExitHandler`, so the process still ends once the app's callback returns. macOS has no X server to lose, and `TerminateGuard` turns a logout into the app's own quit. `SessionEndSignalsTests` sends SIGCONT, whose default does nothing, and skips on Windows; `sample-native.yml`'s `run` job kills Xvfb under the sample to hold `OnDisplayLost` to its callback.

## Things that are held together by tests

- The workflow carries `native/build-linux.sh` inside itself, word for word, because a called workflow runs in the caller's checkout and cannot tell which tag of this repository it was called at. `BuildScriptTests` fails when the two differ, so edit both.
- `CheckStepTests` runs the workflow's two check steps, as written in the file, against canned `objdump` output. `WorkflowFile` finds them by step name, so renaming a step means changing the test.
- `CollectStepTests` runs the macOS job's collect step over a stand-in for what `swift build` leaves, with `lipo` and `strip` stood in for, and the propose job's paths step. A Swift package can make more than one library, a tray's beside a window's, and `mac-libraries` names the others, each with the runtimes folder of the head that loads it: one build, each library collected into its own head, and one pull request with them all. The collect step runs only on macOS, so nothing else runs it before an app's first build. Both run under `Bash`, Git's on Windows.
- `BundlingTests` copies `src/PlatformHeads.Tests/Fixture` out of the repository and packs it with a real publish for each head. The fixture finds the props and targets through the `PlatformHeadsBuild` environment variable, since each head is published by a `dotnet` of its own that inherits the environment and not the command line's properties.

## MSBuild notes

A property function handed item metadata is evaluated before the metadata is, so `$([System.IO.Path]::GetDirectoryName('%(Item.Path)'))` gets the literal text. `PlatformHeadsPublish` turns the items round instead, so the item is the head's project and `%(RootDir)%(Directory)` are its own.

## Code style

As BuildMonitor's, except that the kernel is in a namespace: `var`, expression-bodied members, lambda parameters named `_`, no single-letter locals other than loop counters, no abbreviations, one type per file, and comments that say what failure a thing guards against.
