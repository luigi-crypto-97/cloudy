//
//  MediaFileCache.swift
//  Cloudy
//
//  Cache su disco per immagini, video e allegati. URLCache aiuta finche il
//  sistema decide di tenere le risposte; questa cache rende esplicito il
//  fallback offline per media gia visti e per allegati in attesa di upload.
//

import Foundation

actor MediaFileCache {
    static let shared = MediaFileCache()

    private let fileManager = FileManager.default
    private let cacheDirectory: URL
    private let pendingDirectory: URL
    private let session: URLSession

    private init() {
        let base = fileManager.urls(for: .cachesDirectory, in: .userDomainMask).first!
            .appendingPathComponent("CloudyMedia", isDirectory: true)
        cacheDirectory = base.appendingPathComponent("Remote", isDirectory: true)
        pendingDirectory = base.appendingPathComponent("PendingUploads", isDirectory: true)
        session = URLSession(configuration: .default)

        try? fileManager.createDirectory(at: cacheDirectory, withIntermediateDirectories: true)
        try? fileManager.createDirectory(at: pendingDirectory, withIntermediateDirectories: true)
    }

    func cachedFileURL(for remoteURL: URL) -> URL? {
        let url = cacheURL(for: remoteURL)
        return fileManager.fileExists(atPath: url.path) ? url : nil
    }

    func localFileURL(for remoteURL: URL) async throws -> URL {
        let destination = cacheURL(for: remoteURL)
        if fileManager.fileExists(atPath: destination.path) {
            return destination
        }
        _ = try await data(for: remoteURL)
        return destination
    }

    func data(for remoteURL: URL) async throws -> Data {
        let destination = cacheURL(for: remoteURL)
        if let data = try? Data(contentsOf: destination) {
            return data
        }

        let (data, response) = try await session.data(from: remoteURL)
        guard let http = response as? HTTPURLResponse, (200...299).contains(http.statusCode) else {
            throw URLError(.badServerResponse)
        }
        try data.write(to: destination, options: [.atomic])
        return data
    }

    func cacheRemoteFile(from remoteURL: URL) async {
        _ = try? await data(for: remoteURL)
    }

    func storePendingUpload(data: Data, fileName: String) throws -> URL {
        let safeName = sanitized(fileName)
        let url = pendingDirectory.appendingPathComponent("\(UUID().uuidString)-\(safeName)")
        try data.write(to: url, options: [.atomic])
        return url
    }

    func dataForPendingUpload(path: String) throws -> Data {
        try Data(contentsOf: URL(fileURLWithPath: path))
    }

    func removePendingUpload(path: String?) {
        guard let path else { return }
        try? fileManager.removeItem(atPath: path)
    }

    func cleanup(maxAge: TimeInterval = 30 * 24 * 60 * 60) {
        let cutoff = Date(timeIntervalSinceNow: -maxAge)
        for directory in [cacheDirectory, pendingDirectory] {
            guard let files = try? fileManager.contentsOfDirectory(
                at: directory,
                includingPropertiesForKeys: [.contentModificationDateKey],
                options: [.skipsHiddenFiles]
            ) else { continue }

            for file in files {
                let values = try? file.resourceValues(forKeys: [.contentModificationDateKey])
                if let modified = values?.contentModificationDate, modified < cutoff {
                    try? fileManager.removeItem(at: file)
                }
            }
        }
    }

    private func cacheURL(for remoteURL: URL) -> URL {
        let extensionPart = remoteURL.pathExtension.isEmpty ? "bin" : remoteURL.pathExtension
        let key = Data(remoteURL.absoluteString.utf8)
            .base64EncodedString()
            .replacingOccurrences(of: "/", with: "_")
            .replacingOccurrences(of: "+", with: "-")
            .replacingOccurrences(of: "=", with: "")
        return cacheDirectory.appendingPathComponent("\(key).\(extensionPart)")
    }

    private func sanitized(_ fileName: String) -> String {
        let allowed = CharacterSet.alphanumerics.union(CharacterSet(charactersIn: ".-_"))
        let scalars = fileName.unicodeScalars.map { allowed.contains($0) ? Character($0) : "-" }
        let result = String(scalars).trimmingCharacters(in: CharacterSet(charactersIn: ".-"))
        return result.isEmpty ? "upload.bin" : result
    }
}
