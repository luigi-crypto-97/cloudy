//
//  Motion.swift
//  Cloudy — Motion ridotta, chirurgica, accessibile.
//

import SwiftUI

extension Animation {
    static let cloudySnap = Animation.spring(response: 0.32, dampingFraction: 0.78)
    static let cloudySoft = Animation.spring(response: 0.55, dampingFraction: 0.88)
    static let cloudySmooth = Animation.smooth(duration: 0.35)

    // Legacy names while older screens migrate.
    static let cloudySnappy = Animation.cloudySnap
    static let cloudyBounce = Animation.cloudySnap
    static let cloudyFlow = Animation.cloudySmooth
}

enum CloudyMotion {
    static func snap(reduceMotion: Bool) -> Animation {
        reduceMotion ? .linear(duration: 0.12) : .cloudySnap
    }

    static func snappy(reduceMotion: Bool) -> Animation {
        snap(reduceMotion: reduceMotion)
    }

    static func soft(reduceMotion: Bool) -> Animation {
        reduceMotion ? .linear(duration: 0.16) : .cloudySoft
    }

    static func smooth(reduceMotion: Bool) -> Animation {
        reduceMotion ? .linear(duration: 0.12) : .cloudySmooth
    }

    static func bounce(reduceMotion: Bool) -> Animation {
        snap(reduceMotion: reduceMotion)
    }
}

struct PressableScaleModifier: ViewModifier {
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @GestureState private var isPressed = false
    var scale: CGFloat = 0.97

    func body(content: Content) -> some View {
        content
            .scaleEffect(isPressed && !reduceMotion ? scale : 1)
            .animation(CloudyMotion.snap(reduceMotion: reduceMotion), value: isPressed)
            .simultaneousGesture(
                DragGesture(minimumDistance: 0)
                    .updating($isPressed) { _, state, _ in state = true }
            )
    }
}

struct ShimmerLoadingModifier: ViewModifier {
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @State private var phase: CGFloat = -1.0

    func body(content: Content) -> some View {
        content
            .overlay {
                GeometryReader { proxy in
                    LinearGradient(
                        colors: [.clear, Theme.Palette.blue50.opacity(0.78), .clear],
                        startPoint: .top,
                        endPoint: .bottom
                    )
                    .frame(width: proxy.size.width * 0.46)
                    .rotationEffect(.degrees(16))
                    .offset(x: proxy.size.width * phase)
                }
                .allowsHitTesting(false)
            }
            .clipped()
            .onAppear {
                guard !reduceMotion else { return }
                withAnimation(.linear(duration: 1.2).repeatForever(autoreverses: false)) {
                    phase = 1.8
                }
            }
    }
}

struct SkeletonShimmer: View {
    var cornerRadius: CGFloat = Theme.Radius.md

    var body: some View {
        RoundedRectangle(cornerRadius: cornerRadius, style: .continuous)
            .fill(Theme.Palette.surfaceAlt)
            .shimmerLoading()
    }
}

extension AnyTransition {
    static var cloudyList: AnyTransition {
        .asymmetric(
            insertion: .opacity.combined(with: .scale(scale: 0.96)),
            removal: .opacity
        )
    }
}

extension View {
    func pressableScale(_ scale: CGFloat = 0.97) -> some View {
        modifier(PressableScaleModifier(scale: scale))
    }

    func shimmerLoading() -> some View {
        modifier(ShimmerLoadingModifier())
    }

    func cloudyListTransition() -> some View {
        transition(.cloudyList)
    }

    // Kept as a no-op-compatible API for screens not yet migrated.
    func breathingScale(amount: CGFloat = 1.0, duration: Double = 0) -> some View {
        self
    }
}
