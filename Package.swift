// swift-tools-version: 5.9
import PackageDescription

// What every AppKit head does underneath its drawing, as the C++ runtime under native/runtime is for
// the raylib heads: a toolkit an app's own Swift package depends on at one tag, and links into the
// library its managed side loads. At the root because SwiftPM looks for a package nowhere else.
let package = Package(
    name: "PlatformHeads",
    platforms: [.macOS(.v12)],
    products: [
        .library(name: "PlatformHeadsMac", targets: ["PlatformHeadsMac"]),
        // The tray's C ABI, platformheads_tray.h, which PlatformHeads.Mac calls. An app links it
        // into a library of its own, which then exports it.
        .library(name: "PlatformHeadsMacTray", targets: ["PlatformHeadsMacTray"]),
        // A library made of it as an app makes one, which nothing ships: what the macOS job of
        // build.yml runs PlatformHeads.Mac's tests against.
        .library(name: "platformheads_tray", type: .dynamic, targets: ["PlatformHeadsTrayLibrary"])
    ],
    targets: [
        .target(
            name: "PlatformHeadsMac",
            path: "native/swift/Sources/PlatformHeadsMac",
            linkerSettings: [
                .linkedFramework("AppKit"),
                .linkedFramework("CoreGraphics"),
                .linkedFramework("ImageIO")
            ]),
        // Exists only to import the tray's ABI. Swift does not guarantee struct layout, so the
        // structs have to come from the C header rather than being redeclared in Swift.
        .target(
            name: "CPlatformHeadsTray",
            path: "native/swift/Sources/CPlatformHeadsTray"),
        .target(
            name: "PlatformHeadsMacTray",
            dependencies: ["CPlatformHeadsTray", "PlatformHeadsMac"],
            path: "native/swift/Sources/PlatformHeadsMacTray",
            linkerSettings: [
                .linkedFramework("AppKit")
            ]),
        .target(
            name: "PlatformHeadsTrayLibrary",
            dependencies: ["PlatformHeadsMacTray"],
            path: "native/swift/Sources/PlatformHeadsTrayLibrary"),
        .testTarget(
            name: "PlatformHeadsMacTests",
            dependencies: ["PlatformHeadsMac"],
            path: "native/swift/Tests/PlatformHeadsMacTests"),
        .testTarget(
            name: "PlatformHeadsMacTrayTests",
            dependencies: ["PlatformHeadsMacTray", "CPlatformHeadsTray"],
            path: "native/swift/Tests/PlatformHeadsMacTrayTests")
    ])
