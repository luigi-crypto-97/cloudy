//
//  ChatAttachmentView.swift
//  Cloudy
//

import SwiftUI

struct ChatMessageBubbleContent: View {
    let messageBody: String
    let isMine: Bool

    private var attachment: ChatAttachment? {
        ChatAttachment.parse(messageBody)
    }

    var body: some View {
        if let attachment {
            attachmentView(attachment)
        } else {
            textBubble(messageBody)
        }
    }

    @ViewBuilder
    private func attachmentView(_ attachment: ChatAttachment) -> some View {
        switch attachment.kind {
        case .image:
            VStack(alignment: .leading, spacing: 7) {
                AsyncImage(url: attachment.url) { phase in
                    switch phase {
                    case .success(let image):
                        image
                            .resizable()
                            .scaledToFill()
                    case .failure:
                        fileFallback(icon: "photo", title: "Foto non disponibile")
                    default:
                        ZStack {
                            RoundedRectangle(cornerRadius: 18, style: .continuous)
                                .fill(Theme.Palette.surfaceAlt)
                            ProgressView()
                                .tint(Theme.Palette.blue500)
                        }
                    }
                }
                .frame(width: 230, height: 290)
                .clipShape(RoundedRectangle(cornerRadius: 18, style: .continuous))

                if let title = attachment.title, !title.isEmpty {
                    Text(title)
                        .font(Theme.Font.caption(11, weight: .semibold))
                        .foregroundStyle(isMine ? .white.opacity(0.82) : Theme.Palette.inkMuted)
                        .lineLimit(1)
                }
            }
            .padding(6)
            .background(
                RoundedRectangle(cornerRadius: 22, style: .continuous)
                    .fill(isMine ? AnyShapeStyle(Theme.Palette.blue500) : AnyShapeStyle(Theme.Palette.surface))
            )

        case .file:
            Link(destination: attachment.url) {
                HStack(spacing: 10) {
                    Image(systemName: "doc.fill")
                        .font(.system(size: 18, weight: .heavy))
                        .foregroundStyle(isMine ? .white : Theme.Palette.blue500)
                        .frame(width: 34, height: 34)
                        .background(
                            Circle().fill(isMine ? .white.opacity(0.14) : Theme.Palette.blue50)
                        )

                    VStack(alignment: .leading, spacing: 2) {
                        Text(attachment.title ?? "Allegato")
                            .font(Theme.Font.body(14, weight: .bold))
                            .foregroundStyle(isMine ? .white : Theme.Palette.ink)
                            .lineLimit(1)
                        Text("Tocca per aprire")
                            .font(Theme.Font.caption(11, weight: .semibold))
                            .foregroundStyle(isMine ? .white.opacity(0.72) : Theme.Palette.inkMuted)
                    }
                }
                .padding(.horizontal, 14)
                .padding(.vertical, 12)
                .background(
                    RoundedRectangle(cornerRadius: 18, style: .continuous)
                        .fill(isMine ? AnyShapeStyle(Theme.Palette.blue500) : AnyShapeStyle(Theme.Palette.surface))
                )
            }
            .buttonStyle(.plain)
        }
    }

    private func textBubble(_ text: String) -> some View {
        Text(text)
            .font(Theme.Font.body(15))
            .foregroundStyle(isMine ? .white : Theme.Palette.ink)
            .padding(.horizontal, 14)
            .padding(.vertical, 10)
            .background(
                RoundedRectangle(cornerRadius: 18, style: .continuous)
                    .fill(isMine ? AnyShapeStyle(Theme.Palette.blue500) : AnyShapeStyle(Theme.Palette.surface))
            )
    }

    private func fileFallback(icon: String, title: String) -> some View {
        ZStack {
            RoundedRectangle(cornerRadius: 18, style: .continuous)
                .fill(Theme.Palette.surfaceAlt)
            VStack(spacing: 8) {
                Image(systemName: icon)
                    .font(.system(size: 28, weight: .semibold))
                Text(title)
                    .font(Theme.Font.caption(12, weight: .bold))
            }
            .foregroundStyle(Theme.Palette.inkMuted)
        }
    }
}

private struct ChatAttachment {
    enum Kind { case image, file }

    let kind: Kind
    let url: URL
    let title: String?

    static func parse(_ body: String) -> ChatAttachment? {
        let lines = body
            .split(separator: "\n", omittingEmptySubsequences: false)
            .map { String($0).trimmingCharacters(in: .whitespacesAndNewlines) }
        guard let rawUrl = lines.compactMap(extractURL).last,
              let url = APIClient.shared.mediaURL(from: rawUrl)
        else { return nil }

        let markerLine = lines.first ?? ""
        let title = attachmentTitle(from: markerLine)
        let lowerPath = url.path.lowercased()
        let imageExtensions = [".jpg", ".jpeg", ".png", ".webp", ".heic", ".gif"]
        let isStory = markerLine.localizedCaseInsensitiveContains("storia")
        let isImage = markerLine.hasPrefix("[image]") || isStory || imageExtensions.contains(where: { lowerPath.hasSuffix($0) })
        return ChatAttachment(kind: isImage ? .image : .file, url: url, title: title)
    }

    nonisolated private static func extractURL(from line: String) -> String? {
        for rawToken in line.split(separator: " ") {
            let token = String(rawToken)
                .trimmingCharacters(in: CharacterSet(charactersIn: " \n\t.,;:)("))
            if let url = URL(string: token), url.scheme?.hasPrefix("http") == true {
                return token
            }
        }
        if let url = URL(string: line), url.scheme?.hasPrefix("http") == true {
            return line
        }
        return nil
    }

    nonisolated private static func attachmentTitle(from markerLine: String) -> String? {
        let clean = markerLine
            .replacingOccurrences(of: "[image]", with: "")
            .replacingOccurrences(of: "[file]", with: "")
            .replacingOccurrences(of: "📎", with: "")
            .trimmingCharacters(in: .whitespacesAndNewlines)
        return clean.isEmpty ? nil : clean
    }
}
