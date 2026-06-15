// swift-tools-version:6.1
import PackageDescription

let package = Package(
    name: "ReWriteMac",
    platforms: [.macOS(.v13)],
    targets: [
        .executableTarget(
            name: "ReWriteMac",
            path: "Sources/ReWriteMac",
            swiftSettings: [
                // Build in Swift 5 language mode (no strict concurrency).
                .unsafeFlags(["-swift-version", "5"])
            ],
            linkerSettings: [
                .linkedFramework("Cocoa"),
                .linkedFramework("WebKit"),
                .linkedFramework("Carbon"),
                .linkedFramework("ServiceManagement"),
            ]
        )
    ]
)
