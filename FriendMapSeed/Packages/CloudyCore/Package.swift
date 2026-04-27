// swift-tools-version: 5.9
//
//  CloudyCore
//  Logica di dominio cross-platform e testabile.
//  Non importa SwiftUI / UIKit / MapKit, solo Foundation e CoreLocation
//  (CoreLocation è disponibile solo su Apple, ma le funzioni che richiedono
//  geometria sono parametriche su tipi POD).
//
import PackageDescription

let package = Package(
    name: "CloudyCore",
    platforms: [
        .iOS(.v17), .macOS(.v14)
    ],
    products: [
        .library(name: "CloudyCore", targets: ["CloudyCore"])
    ],
    targets: [
        .target(name: "CloudyCore", path: "Sources/CloudyCore"),
        .testTarget(
            name: "CloudyCoreTests",
            dependencies: ["CloudyCore"],
            path: "Tests/CloudyCoreTests"
        )
    ]
)
