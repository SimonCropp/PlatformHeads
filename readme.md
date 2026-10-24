# PlatformHeads

The build and packaging half of a .NET app that has one head per platform: a WinForms head on Windows, and a native library behind a C ABI on macOS (Swift and AppKit) and Linux (raylib and Dear ImGui), each started by one shared loop.

[DiffEngine](https://github.com/VerifyTests/DiffEngine)'s viewer and [BuildMonitor](https://github.com/SimonCropp/BuildMonitor) are both built this way, and each had its own copy of everything here. A fix made to one copy did not reach the other, so this is the one copy.

An app takes four things from it, all at the same tag: a library its loop is built from, and three for building and packaging the heads.


## The kernel

The `PlatformHeads.Core` NuGet package, in the `PlatformHeads` namespace. It is the managed plumbing every head's loop shares, and nothing an app draws:

 * `SessionHost<TState>` holds the one mutable reference to an immutable state. Reads take no lock; mutations are serialised, a mutation that mutates again throws, and `Changed` is raised only for a new state.
 * `ScreenCache<TState, TScreen>` builds a screen once per state, and once more each time the clock ticks over where the screen shows the time, so a head can stop at the reference when nothing changed.
 * `IHeadWindow<TScreen, TInput>` is the contract a head's window keeps: present a whole screen, idle until woken, poll the input. An app's own window interface derives from it. `OpenWindow<TWindow>` opens one.
 * `Pacing`, `LoopWake<TState>` and `ScreenCache.UntilTick` are how long a loop idles between frames and what wakes it.
 * `ClipboardPump` offers a copy for a few frames, since a Windows clipboard another process holds refuses a set outright.
 * `WindowPlacement` is where a window was left, and `PlacementSettler` decides when a native window that reports where it is on every poll has stopped moving.
 * `NativeResolver` finds a head's native library under `runtimes/{rid}/native`, and never loads a glibc build into a musl process.
 * `SessionEndSignals` hears a macOS or Linux session ending by the SIGTERM and SIGHUP it ends a process with, which .NET otherwise ends it on there and then, with no `finally` run and no `ProcessExit` raised. A second signal waits for the first one's ending, since systemd sends both at once.

The assembly is signed, since DiffEngine's are.


## The WinForms shell

The `PlatformHeads.WindowsForms` NuGet package, for the Windows head. `HeadForm` is the form the head draws in, pumped by the app's loop with `Application.DoEvents` rather than `Application.Run`:

 * It opens where it was left, moved onto a display that can show it (`WindowFit.Restorable`), or at its first size scaled to the display and centred for it.
 * It says where it is (`Placement`, or `TakeSettled` for an app that saves as the window settles), with a maximised window remembered as maximised through a minimise, and `Raise` and `ShowRestored` bring it back that way.
 * Closing it is a request the app answers (`TakeCloseRequest`, `CloseForReal`), except at shutdown and End Task, and `SessionEnding` runs before Windows can end the process.
 * `WaitForFrame` is the head's `IHeadWindow.Wait`: it idles on the message queue and the loop's wake together (`MessageWait`), a frame at most while something on it animates, and not at all while input is already waiting.
 * `ModalFrame` keeps frames coming while user32 holds the thread in its own loop, for a move or a resize, or whatever a subclass enters with `EnterModal`.

`FormsApp.Configure` is the process wide setup, once, and `FormsClipboard.Set` the clipboard half of `SetClipboard`.

The head is `net10.0`, not `net10.0-windows`, since it is published per runtime identifier from a platform neutral package, and names WinForms itself with `UseWindowsForms` and the usual workaround for the SDK's check of it. So the package does not declare the WinForms framework reference: declared by a package, the SDK refuses a `net10.0` project that takes it (NETSDK1136).


## The MSBuild targets

The `PlatformHeads` NuGet package. The project that ships the app lists its heads, and packing it publishes each for its runtime identifier and bundles them:

```xml
<ItemGroup>
  <PackageReference Include="PlatformHeads" PrivateAssets="all" />
  <PlatformHead Include="win-x64" Project="..\MyApp.Windows\MyApp.Windows.csproj" Apphost="MyApp.Tray.exe" />
  <PlatformHead Include="linux-x64" Project="..\MyApp.Native\MyApp.Native.csproj" Apphost="MyApp.Tray" Native="true" />
  <PlatformHead Include="osx-arm64" Project="..\MyApp.Native\MyApp.Native.csproj" Apphost="MyApp.Tray" Native="true" />
</ItemGroup>
<PropertyGroup>
  <PlatformHeadsPackagePath>tools\$(TargetFramework)\any\heads</PlatformHeadsPackagePath>
</PropertyGroup>
```

 * `PlatformHeadsPackagePath` is the folder of the package the heads go in, each under its runtime identifier.
 * `PlatformHeadsOutputPath`, where set, also copies them beside the build output, so a build run from `bin` finds a head the way an installed package does.
 * `Native="true"` says the head's renderer is a native library, and a head with none committed for its runtime identifier is warned of (`PH0001`).
 * A head whose apphost was not staged fails the pack (`PH0002`).

A head with native libraries committed under `runtimes\{rid}\native` sets `PlatformHeadsNativeRuntimes` to `true`, and they are carried into its output.

The project that lists the heads still needs a `ProjectReference` to each, with `ReferenceOutputAssembly="false"`, `Private="false"` and `ExcludeAssets="all"`, for build ordering alone: each head is published by a `dotnet` of its own, and without the reference a solution build can have two processes building one head at once.


## The tray pieces

What a Windows tray needs whatever it lists:

 * `MenuOnOpening.Rebuild(menu, build)` builds a tray menu's items every time it opens, beside the ones already on it, takes them off as it closes and disposes them at the next open. A strip that starts empty still opens on the first click.
 * `SessionEndWindow` hears that the session is ending, for an app that is only a notify icon and so has no form to be closed.
 * `IssueLink` builds a new GitHub issue's URL with the title and body encoded, and says whether a message is being raised for the first time this run. `LinkOpener.TryOpen` opens it on Windows, macOS or Linux.


## The tray

A tray icon on every platform, as a model the app hands over whole, so each platform only draws it. The model is in `PlatformHeads.Core`:

 * `TrayModel<TIcon>` is which of the app's icons to show, the tooltip, and the menu. `TIcon` is the app's own enum, and each back end is given the pictures for all of it when it opens.
 * `TrayMenuItem` is an entry: an id it reports when chosen, a label, enabled, a separator, a glyph, an optional check, and an optional submenu. Items are equal by value, submenus included.
 * `ITray<TIcon>` takes a model with `Apply`, pops a `TrayNotification` with `Notify`, and hands over what the user did with `Poll`, as a `TrayInput`.
 * `DesktopNotification` pops one through `notify-send` or `osascript`, for a tray with no balloons of its own.
 * `RunAtLogin.ForPlatform(LoginItem)` is the Run key on Windows, a launch agent on macOS, and an XDG autostart entry on Linux.

One back end a platform:

 * Windows: `NotifyIconTray<TIcon>` in `PlatformHeads.WindowsForms`, which builds its menu each time it opens.
 * Linux: `StatusNotifierTray<TIcon>` in `PlatformHeads.Linux`, a StatusNotifierItem with a dbusmenu over D-Bus, in managed code. It shows on KDE, Cinnamon and XFCE, and on GNOME with the AppIndicator extension. With no host on the bus, `Open` says so and returns null.
 * macOS: `StatusItem` in the Swift package, which the app's native library drives.


## The CMake module

`cmake/PlatformHeads.cmake`, fetched by tag. It pins raylib and Dear ImGui, switches off the raylib options that 6.0 wrongly defaults to on, and builds the head's library with what it needs to load on a stock machine:

```cmake
include(FetchContent)
FetchContent_Declare(
  platformheads
  GIT_REPOSITORY https://github.com/SimonCropp/PlatformHeads.git
  GIT_TAG <tag>
  GIT_SHALLOW ON)
FetchContent_MakeAvailable(platformheads)
include(${platformheads_SOURCE_DIR}/cmake/PlatformHeads.cmake)

platformheads_defaults()
platformheads_fetch(FORMATS PNG)
platformheads_add_library(myapp_ui SOURCES src/myapp.cpp)
```

`platformheads_add_library` also compiles in the native runtime, `native/runtime`, and puts its header on the library's include path: `#include "platformheads/runtime.h"`. It is what a raylib head does underneath its drawing, in namespace `ph`, as a toolkit and not a framework, so the app keeps its own present, poll and capture and calls these where it did the same itself:

 * The window: `OpenWindow` makes it hidden and shows it where it was left, `SetHidden` and `Focus` bring a minimised one back, `TakeCloseRequest` clears raylib's latched close flag, `GetPlacement` reports the bounds a maximised window goes back to, and `OnDisplayLost` is called as the X server goes, which is how a logoff ends the head, before Xlib ends the process with `exit(1)`.
 * Input: GLFW's callbacks set over raylib's, so a press and release between two frames are both seen. `PopKey` hands keys over one at a time, `LetterOf` reads a control chord by the layout's letter, `FeedImGui` gives ImGui what happened in order, and `TakeWheelNotches` keeps a touchpad's fractions.
 * Which frames are built and drawn: `Arrived`, `CanLeaveAlone`, `Show` (drawn only when the draw lists differ from those on the screen), `Rest` and `MarkStale`.
 * Drawing and fonts: `RenderDrawData` with ImGui's dynamic textures, `AddEmbeddedFont`, and `UseSystemFonts`, which finds the characters the embedded font lacks through fontconfig, loaded at run time, on a thread of its own.
 * Strings and labels: `Slice` and `Copy`, bounds checked in sixty four bits, and labels with `##` in them drawn as they are.


## The Swift package

`PlatformHeadsMac`, the same for an AppKit head: a set of pieces the app's own package glues in, depended on at the same tag as the rest, and linked into the library the managed side loads:

```swift
dependencies: [
    .package(url: "https://github.com/SimonCropp/PlatformHeads", exact: "<tag>")
],
targets: [
    .target(
        name: "MyApp",
        dependencies: [.product(name: "PlatformHeadsMac", package: "PlatformHeads")])
]
```

 * `HeadWindow` makes the application and the window when `NSApplication.run` is never called, `EventPump` dispatches AppKit's events and waits out the frame, returning early for input and waiting longer for a window nobody can see, and `TerminateGuard` turns a quit from the Dock or a logout into the app's own.
 * `NapGuard` keeps App Nap off, which would coalesce the timers a head is driven by.
 * `KnobScroller` follows a drag of its knob as ordinary events rather than in AppKit's own loop, and `ScrollerTravel` counts a scroller in rows, so its travel is the managed side's clamp.
 * `WheelNotches` and `Steps` tell a wheel from a trackpad and keep the fractions, and `ToolTipRegions` registers tips again only when they change, which is what lets their delay elapse.
 * `StringBlob` reads the ABI's string blob bounds checked, `Capture` writes a png at a pinned scale for the pixel snapshots, `Clipboard` sets text, and `WindowPlacement` turns AppKit's frames into the top-left bounds every head reports.
 * `StatusItem` is a tray app's menu bar item: a left click is the icon's, and the menu, built from `StatusMenuItem`s with submenus and checks, is the right button's or a control-click's.


## The build-native workflow

A reusable workflow that builds the native libraries, checks what the Linux ones will need of a machine, and opens a pull request with the binaries:

```yaml
jobs:
  native:
    uses: SimonCropp/PlatformHeads/.github/workflows/build-native.yml@<tag>
    permissions:
      contents: write
      pull-requests: write
    with:
      library: myapp_ui
      linux-runtimes: src/MyApp.Native/runtimes
      swift-package: native/swift
      mac-runtimes: src/MyApp.Native/runtimes
```

The Linux library is built in a `manylinux_2_28` container, so it needs no glibc newer than 2.28. To build it the same way by hand, from an app's repository root:

```bash
docker run --rm -v "$PWD:/src" -w /src -e LIBRARY=myapp_ui -e NATIVE=native "quay.io/pypa/manylinux_2_28_$(uname -m)" bash /path/to/PlatformHeads/native/build-linux.sh
```


## Icon

https://thenounproject.com/icon/crash-test-dummy-2523803/
