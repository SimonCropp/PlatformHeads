// swift-tools-version: 5.9
import PackageDescription

// What every AppKit head does underneath its drawing, as the C++ runtime under native/runtime is for
// the raylib heads: a toolkit an app's own Swift package depends on at one tag, and links into the
// library its managed side loads. At the root because SwiftPM looks for a package nowhere else.
let package = Package(
    name: "PlatformHeads",
    platforms: [.macOS(.v12)],
    products: [
        .library(name: "PlatformHeadsMac", targets: ["PlatformHeadsMac"])
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
        .testTarget(
            name: "PlatformHeadsMacTests",
            dependencies: ["PlatformHeadsMac"],
            path: "native/swift/Tests/PlatformHeadsMacTests")
    ])
