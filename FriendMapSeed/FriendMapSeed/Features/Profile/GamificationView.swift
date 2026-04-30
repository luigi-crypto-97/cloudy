//
//  GamificationView.swift
//  Cloudy
//

import SwiftUI

struct GamificationView: View {
    @State private var summary: GamificationSummary?
    @State private var leaderboard: Leaderboard?
    @State private var isLoading = false
    @State private var errorMessage: String?
    @State private var selectedCity: String = ""

    var body: some View {
        ScrollView {
            LazyVStack(spacing: 18) {
                if let summary {
                    scoreHero(summary)
                    missions(summary.weeklyMissions)
                    badges(summary.badges)
                } else if isLoading {
                    RoundedRectangle(cornerRadius: 28)
                        .fill(Theme.Palette.blue50)
                        .frame(height: 210)
                        .shimmerLoading()
                }

                leaderboardSection

                if let errorMessage {
                    Text(errorMessage)
                        .font(Theme.Font.caption(12, weight: .semibold))
                        .foregroundStyle(Theme.Palette.coral500)
                }
            }
            .padding(Theme.Spacing.lg)
            .padding(.bottom, 120)
        }
        .background(Theme.Palette.surfaceAlt.ignoresSafeArea())
        .navigationTitle("Classifica")
        .navigationBarTitleDisplayMode(.large)
        .task { await load() }
        .refreshable { await load() }
    }

    private func scoreHero(_ summary: GamificationSummary) -> some View {
        VStack(alignment: .leading, spacing: 18) {
            HStack(alignment: .top) {
                VStack(alignment: .leading, spacing: 6) {
                    Text("Il tuo giro")
                        .font(Theme.Font.caption(12, weight: .heavy))
                        .foregroundStyle(Theme.Palette.blue600)
                    Text("\(summary.totalPoints)")
                        .font(Theme.Font.display(46, weight: .black).monospacedDigit())
                        .foregroundStyle(Theme.Palette.ink)
                        .contentTransition(.numericText())
                    Text("punti totali · livello \(summary.level)")
                        .font(Theme.Font.body(14, weight: .semibold))
                        .foregroundStyle(Theme.Palette.inkSoft)
                }
                Spacer()
                LevelRing(progress: summary.levelProgress, level: summary.level)
            }

            VStack(alignment: .leading, spacing: 8) {
                HStack {
                    Text("Prossimo livello")
                    Spacer()
                    Text("\(Int(summary.levelProgress * 100))%")
                        .monospacedDigit()
                }
                .font(Theme.Font.caption(12, weight: .heavy))
                .foregroundStyle(Theme.Palette.inkMuted)

                GeometryReader { proxy in
                    Capsule()
                        .fill(Theme.Palette.blue50)
                        .overlay(alignment: .leading) {
                            Capsule()
                                .fill(Theme.Palette.blue500)
                                .frame(width: proxy.size.width * summary.levelProgress)
                        }
                }
                .frame(height: 9)
            }

            Text(summary.antiCheatNote)
                .font(Theme.Font.caption(11, weight: .semibold))
                .foregroundStyle(Theme.Palette.inkMuted)
        }
        .padding(20)
        .background(Theme.Palette.surface, in: RoundedRectangle(cornerRadius: 28, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 28, style: .continuous).stroke(Theme.Palette.blue100.opacity(0.75), lineWidth: 1))
        .cardShadow()
    }

    private func missions(_ missions: [WeeklyMission]) -> some View {
        VStack(alignment: .leading, spacing: 12) {
            sectionTitle("Missioni settimanali")
            ForEach(missions) { mission in
                MissionRow(mission: mission)
            }
        }
    }

    private func badges(_ badges: [UserBadge]) -> some View {
        VStack(alignment: .leading, spacing: 12) {
            sectionTitle("Badge")
            if badges.isEmpty {
                CloudyEmptyState(icon: "rosette", title: "Ancora nessun badge", message: "Completa missioni e azioni sociali per sbloccarli.")
                    .padding(.vertical, 8)
            } else {
                ScrollView(.horizontal, showsIndicators: false) {
                    HStack(spacing: 12) {
                        ForEach(badges) { badge in
                            VStack(spacing: 8) {
                                Image(systemName: "rosette")
                                    .font(Theme.Font.title(24, weight: .heavy))
                                    .foregroundStyle(Theme.Palette.blue500)
                                    .frame(width: 58, height: 58)
                                    .background(Circle().fill(Theme.Palette.blue50))
                                Text(badge.title)
                                    .font(Theme.Font.caption(11, weight: .heavy))
                                    .foregroundStyle(Theme.Palette.ink)
                                    .lineLimit(2)
                                    .multilineTextAlignment(.center)
                                    .frame(width: 84)
                            }
                            .padding(12)
                            .background(Theme.Palette.surface, in: RoundedRectangle(cornerRadius: 20, style: .continuous))
                        }
                    }
                }
            }
        }
    }

    private var leaderboardSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack {
                sectionTitle("Classifica zona")
                Spacer()
                TextField("Citta", text: $selectedCity)
                    .font(Theme.Font.caption(12, weight: .semibold))
                    .padding(.horizontal, 10)
                    .padding(.vertical, 8)
                    .frame(width: 116)
                    .background(Theme.Palette.surface, in: Capsule())
                    .submitLabel(.search)
                    .onSubmit { Task { await loadLeaderboard() } }
            }

            if let leaderboard, !leaderboard.entries.isEmpty {
                VStack(spacing: 0) {
                    ForEach(leaderboard.entries.prefix(20)) { entry in
                        LeaderboardRow(entry: entry)
                        if entry.id != leaderboard.entries.prefix(20).last?.id {
                            Rectangle().fill(Theme.Palette.hairline).frame(height: 0.5).padding(.leading, 66)
                        }
                    }
                }
                .background(Theme.Palette.surface, in: RoundedRectangle(cornerRadius: 22, style: .continuous))
                .cardShadow()
            } else {
                CloudyEmptyState(icon: "list.number", title: "Classifica in arrivo", message: "Appena ci sono abbastanza segnali, qui vedrai la tua zona.")
                    .padding(.vertical, 8)
            }
        }
    }

    private func sectionTitle(_ text: String) -> some View {
        Text(text)
            .font(Theme.Font.title(20, weight: .heavy))
            .foregroundStyle(Theme.Palette.ink)
            .frame(maxWidth: .infinity, alignment: .leading)
    }

    private func load() async {
        isLoading = true
        defer { isLoading = false }
        do {
            async let summaryTask = API.gamificationSummary()
            async let leaderboardTask = API.leaderboard(city: selectedCity.isEmpty ? nil : selectedCity)
            summary = try await summaryTask
            leaderboard = try await leaderboardTask
            errorMessage = nil
            try? await API.checkGamificationAchievements()
        } catch {
            errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    private func loadLeaderboard() async {
        do {
            leaderboard = try await API.leaderboard(city: selectedCity.isEmpty ? nil : selectedCity)
            errorMessage = nil
        } catch {
            errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }
}

private struct MissionRow: View {
    let mission: WeeklyMission

    var body: some View {
        HStack(spacing: 13) {
            ZStack {
                Circle().fill(mission.isCompleted ? Theme.Palette.mint400.opacity(0.18) : Theme.Palette.blue50)
                Image(systemName: mission.isCompleted ? "checkmark" : mission.icon)
                    .font(Theme.Font.body(18, weight: .black))
                    .foregroundStyle(mission.isCompleted ? Theme.Palette.mint500 : Theme.Palette.blue500)
            }
            .frame(width: 48, height: 48)

            VStack(alignment: .leading, spacing: 6) {
                HStack {
                    Text(mission.title)
                        .font(Theme.Font.body(15, weight: .heavy))
                        .foregroundStyle(Theme.Palette.ink)
                    Spacer()
                    Text("+\(mission.rewardPoints)")
                        .font(Theme.Font.caption(12, weight: .black))
                        .foregroundStyle(Theme.Palette.blue600)
                }
                Text(mission.subtitle)
                    .font(Theme.Font.caption(12, weight: .semibold))
                    .foregroundStyle(Theme.Palette.inkSoft)
                    .lineLimit(1)
                ProgressView(value: mission.progressRatio)
                    .tint(mission.isCompleted ? Theme.Palette.mint500 : Theme.Palette.blue500)
                Text("\(mission.progress)/\(mission.target)")
                    .font(Theme.Font.caption(10, weight: .heavy))
                    .foregroundStyle(Theme.Palette.inkMuted)
            }
        }
        .padding(14)
        .background(Theme.Palette.surface, in: RoundedRectangle(cornerRadius: 20, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 20, style: .continuous).stroke(Theme.Palette.blue100.opacity(0.55), lineWidth: 1))
    }
}

private struct LeaderboardRow: View {
    let entry: LeaderboardEntry

    var body: some View {
        HStack(spacing: 12) {
            Text("#\(entry.rank)")
                .font(Theme.Font.heroNumber(15).monospacedDigit())
                .foregroundStyle(entry.isMe ? Theme.Palette.blue600 : Theme.Palette.inkMuted)
                .frame(width: 42, alignment: .leading)

            StoryAvatar(
                url: APIClient.shared.mediaURL(from: entry.avatarUrl),
                size: 38,
                hasStory: entry.isMe,
                initials: String((entry.displayName ?? entry.nickname).prefix(1)).uppercased()
            )

            VStack(alignment: .leading, spacing: 2) {
                Text(entry.displayName ?? entry.nickname)
                    .font(Theme.Font.body(14, weight: .heavy))
                    .foregroundStyle(Theme.Palette.ink)
                Text(entry.primaryCity ?? "Cloudy")
                    .font(Theme.Font.caption(11, weight: .semibold))
                    .foregroundStyle(Theme.Palette.inkMuted)
            }

            Spacer()

            VStack(alignment: .trailing, spacing: 2) {
                Text("\(entry.weeklyPoints)")
                    .font(Theme.Font.heroNumber(16).monospacedDigit())
                    .foregroundStyle(Theme.Palette.ink)
                Text("pt sett.")
                    .font(Theme.Font.caption(10, weight: .heavy))
                    .foregroundStyle(Theme.Palette.inkMuted)
            }
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 12)
        .background(entry.isMe ? Theme.Palette.blue50.opacity(0.75) : .clear)
    }
}

private struct LevelRing: View {
    let progress: Double
    let level: Int

    var body: some View {
        ZStack {
            Circle().stroke(Theme.Palette.blue50, lineWidth: 8)
            Circle()
                .trim(from: 0, to: progress)
                .stroke(Theme.Palette.blue500, style: StrokeStyle(lineWidth: 8, lineCap: .round))
                .rotationEffect(.degrees(-90))
            VStack(spacing: 0) {
                Text("\(level)")
                    .font(Theme.Font.display(25, weight: .black).monospacedDigit())
                    .foregroundStyle(Theme.Palette.ink)
                Text("LVL")
                    .font(Theme.Font.caption(9, weight: .black))
                    .foregroundStyle(Theme.Palette.inkMuted)
            }
        }
        .frame(width: 86, height: 86)
    }
}
