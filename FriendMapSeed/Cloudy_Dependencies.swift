//
//  Cloudy_Dependencies.swift
//  Cloudy — Swift Package Manager dependencies manifest
//
//  Per aggiungere dipendenze al progetto:
//  1. Aggiungi il package qui
//  2. Esegui: xcodebuild -resolvePackageDependencies
//

import PackageDescription

let package = Package(
    name: "Cloudy",
    platforms: [
        .iOS(.v17)
    ],
    products: [],
    dependencies: [
        // SignalR Client per chat real-time
        .package(url: "https://github.com/SignalRClient/SignalR-Client-Swift.git", from: "0.12.0"),
        
        // Nuke per image caching avanzato
        .package(url: "https://github.com/kean/Nuke.git", from: "12.8.0"),
        
        // Firebase (Analytics, Crashlytics, Messaging)
        .package(url: "https://github.com/firebase/firebase-ios-sdk.git", from: "11.0.0"),
        
        // Sentry per crash reporting e performance monitoring
        .package(url: "https://github.com/getsentry/sentry-cocoa.git", from: "8.30.0"),
        
        // SwiftLint per linting
        .package(url: "https://github.com/realm/SwiftLint.git", from: "0.57.0"),
    ],
    targets: [
        // Target principale FriendMapSeed
        .target(
            name: "FriendMapSeed",
            dependencies: [
                .product(name: "SignalRClient", package: "SignalR-Client-Swift"),
                .product(name: "Nuke", package: "Nuke"),
                .product(name: "FirebaseAnalytics", package: "firebase-ios-sdk"),
                .product(name: "FirebaseCrashlytics", package: "firebase-ios-sdk"),
                .product(name: "Sentry", package: "sentry-cocoa"),
                "CloudyCore"
            ],
            path: "FriendMapSeed"
        ),
        
        // CloudyCore package locale
        .target(
            name: "CloudyCore",
            dependencies: [],
            path: "Packages/CloudyCore/Sources/CloudyCore"
        ),
        
        // Test per CloudyCore
        .testTarget(
            name: "CloudyCoreTests",
            dependencies: ["CloudyCore"],
            path: "Packages/CloudyCore/Tests/CloudyCoreTests"
        )
    ]
)
