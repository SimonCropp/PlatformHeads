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

The assembly is signed, since DiffEngine's are.


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
