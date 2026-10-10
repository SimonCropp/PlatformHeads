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

Layer 1, which is what is here, is build and packaging, and ships in three forms an app pins at one tag:

- `src/PlatformHeads/build/` is the NuGet package: MSBuild props and targets, no assembly. `PlatformHead` items name a head project for each runtime identifier, and packing publishes and bundles them. Packed into both `build` and `buildMultiTargeting`, since DiffEngine has several target frameworks and packs from its outer build.
- `cmake/PlatformHeads.cmake` is fetched by an app's `native/CMakeLists.txt` with `FetchContent`. It pins raylib and Dear ImGui and carries the raylib 6.0 option workarounds and `--as-needed`. The repository root must never gain a `CMakeLists.txt`, or `FetchContent_MakeAvailable` would start building it.
- `.github/workflows/build-native.yml` is a reusable workflow. `sample-native.yml` calls it for `sample/native`, which is how a change to any of the three is run for real before an app takes it. The macOS job has no sample and is first run by an app.

Layer 4 will be a Swift package, and SwiftPM needs `Package.swift` at the repository root.

## Things that are held together by tests

- The workflow carries `native/build-linux.sh` inside itself, word for word, because a called workflow runs in the caller's checkout and cannot tell which tag of this repository it was called at. `BuildScriptTests` fails when the two differ, so edit both.
- `CheckStepTests` runs the workflow's two check steps, as written in the file, against canned `objdump` output. `WorkflowFile` finds them by step name, so renaming a step means changing the test.
- `BundlingTests` copies `src/PlatformHeads.Tests/Fixture` out of the repository and packs it with a real publish for each head. The fixture finds the props and targets through the `PlatformHeadsBuild` environment variable, since each head is published by a `dotnet` of its own that inherits the environment and not the command line's properties.

## MSBuild notes

A property function handed item metadata is evaluated before the metadata is, so `$([System.IO.Path]::GetDirectoryName('%(Item.Path)'))` gets the literal text. `PlatformHeadsPublish` turns the items round instead, so the item is the head's project and `%(RootDir)%(Directory)` are its own.

## Code style

As BuildMonitor's: no namespaces, `var`, expression-bodied members, lambda parameters named `_`, no single-letter locals other than loop counters, no abbreviations, one type per file, and comments that say what failure a thing guards against.
