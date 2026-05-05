//
//  ImageCache.swift
//  Cloudy — Image caching helper con URLSession
//
//  Nota: Questo file usa il caching nativo di URLSession.
//  Per caching avanzato con Nuke, installare il package e decommentare gli import.
//
//  Usage:
//    CachedImage(url: url, options: .avatar)
//

import Foundation
import SwiftUI

#if canImport(UIKit)
import UIKit
#endif

// import Nuke
// import NukeUI

// MARK: - Image Cache Options

enum ImageCacheOptions {
    case avatar
    case story
    case venueCover
    case chatAttachment
    case generic
    
    var targetSize: CGSize? {
        switch self {
        case .avatar: return CGSize(width: 100, height: 100)
        case .venueCover: return CGSize(width: 800, height: 600)
        case .chatAttachment: return CGSize(width: 600, height: 600)
        default: return nil
        }
    }
}

// MARK: - Cached Image View

struct CachedImage: View {
    let url: URL?
    let options: ImageCacheOptions
    let contentMode: ContentMode
    
    @State private var phase: AsyncImagePhase = .empty
    @State private var isLoading = false
    
    init(
        url: URL?,
        options: ImageCacheOptions = .generic,
        contentMode: ContentMode = .fill
    ) {
        self.url = url
        self.options = options
        self.contentMode = contentMode
    }
    
    var body: some View {
        Group {
            switch phase {
            case .empty:
                placeholder
            case .success(let image):
                image
                    .resizable()
                    .aspectRatio(contentMode: contentMode)
            case .failure:
                placeholder
            @unknown default:
                placeholder
            }
        }
        .task {
            await loadImage()
        }
        .onChange(of: url) {
            Task { await loadImage() }
        }
    }
    
    @ViewBuilder
    private var placeholder: some View {
        ZStack {
            Theme.Palette.blue50
            Image(systemName: options.placeholder)
                .font(.system(size: 24, weight: .medium))
                .foregroundStyle(Theme.Palette.blue200)
        }
    }
    
    private func loadImage() async {
        guard let url else {
            await MainActor.run { phase = .empty }
            return
        }
        
        await MainActor.run { 
            phase = .empty
            isLoading = true
        }
        
        do {
            let data = try await MediaFileCache.shared.data(for: url)
            guard let uiImage = UIImage(data: data) else {
                throw URLError(.cannotDecodeContentData)
            }
            
            await MainActor.run {
                phase = .success(Image(uiImage: uiImage))
                isLoading = false
            }
        } catch {
            await MainActor.run {
                phase = .failure(error)
                isLoading = false
            }
        }
    }
}

// MARK: - Image Prefetcher

@MainActor
final class ImagePrefetcher {
    static let shared = ImagePrefetcher()
    
    private var prefetchTasks: [URL: Task<Void, Never>] = [:]
    
    private init() {}
    
    func prefetch(urls: [URL], options: ImageCacheOptions = .generic) {
        for url in urls {
            guard prefetchTasks[url] == nil else { continue }
            
            prefetchTasks[url] = Task {
                await MediaFileCache.shared.cacheRemoteFile(from: url)
                Task { @MainActor in
                    prefetchTasks.removeValue(forKey: url)
                }
            }
        }
    }
    
    func stopPrefetching(urls: [URL]) {
        for url in urls {
            prefetchTasks[url]?.cancel()
            prefetchTasks.removeValue(forKey: url)
        }
    }
    
    func stopAll() {
        for task in prefetchTasks.values {
            task.cancel()
        }
        prefetchTasks.removeAll()
    }
}

// MARK: - Image Cache Manager

@MainActor
final class ImageCacheManager {
    static let shared = ImageCacheManager()
    
    private init() {}
    
    func clearMemoryCache() {
        URLCache.shared.removeAllCachedResponses()
    }
    
    func clearDiskCache() {
        // URLSession cache management
        URLSession.shared.configuration.urlCache?.removeAllCachedResponses()
        Task {
            await MediaFileCache.shared.cleanup(maxAge: 0)
        }
    }
    
    func clearAll() {
        clearMemoryCache()
        clearDiskCache()
    }
}

// MARK: - View Extension

extension View {
    func cachedImage(
        url: URL?,
        options: ImageCacheOptions = .generic,
        contentMode: ContentMode = .fill
    ) -> some View {
        CachedImage(url: url, options: options, contentMode: contentMode)
    }
}

// MARK: - UIImage Extension

#if canImport(UIKit)
extension UIImage {
    func resized(to size: CGSize) -> UIImage {
        let renderer = UIGraphicsImageRenderer(size: size)
        return renderer.image { _ in
            draw(in: CGRect(origin: .zero, size: size))
        }
    }
    
    func compressed(quality: CGFloat = 0.8) -> Data? {
        jpegData(compressionQuality: quality)
    }
}
#endif

// MARK: - Placeholder Helpers

extension ImageCacheOptions {
    var placeholder: String {
        switch self {
        case .avatar: return "person.crop.circle.fill"
        case .story: return "photo.fill"
        case .venueCover: return "building.2.fill"
        case .chatAttachment: return "doc.fill"
        case .generic: return "photo.fill"
        }
    }
}
