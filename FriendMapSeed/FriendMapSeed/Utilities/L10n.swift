//
//  L10n.swift
//  Cloudy — Type-safe localization helper
//
//  Usage: Text(L10n.App.loading)
//

import Foundation

enum L10n {
    
    // MARK: - App General
    enum App {
        static let name = NSLocalizedString("app.name", comment: "App name")
        static let loading = NSLocalizedString("app.loading", comment: "Loading message")
        static let error = NSLocalizedString("app.error", comment: "Generic error")
        static let retry = NSLocalizedString("app.retry", comment: "Retry action")
        static let cancel = NSLocalizedString("app.cancel", comment: "Cancel action")
        static let confirm = NSLocalizedString("app.confirm", comment: "Confirm action")
        static let save = NSLocalizedString("app.save", comment: "Save action")
        static let delete = NSLocalizedString("app.delete", comment: "Delete action")
        static let close = NSLocalizedString("app.close", comment: "Close action")
        static let done = NSLocalizedString("app.done", comment: "Done action")
        static let search = NSLocalizedString("app.search", comment: "Search action")
        static func searchPlaceholder(_ placeholder: String = NSLocalizedString("app.search_placeholder", comment: "Search placeholder")) -> String { placeholder }
        static let noResults = NSLocalizedString("app.no_results", comment: "No results message")
        static let loadingError = NSLocalizedString("app.loading_error", comment: "Loading error message")
    }
    
    // MARK: - Authentication
    enum Auth {
        static let login = NSLocalizedString("auth.login", comment: "Login action")
        static let logout = NSLocalizedString("auth.logout", comment: "Logout action")
        static let loginTitle = NSLocalizedString("auth.login_title", comment: "Login screen title")
        static let loginSubtitle = NSLocalizedString("auth.login_subtitle", comment: "Login screen subtitle")
        static let nickname = NSLocalizedString("auth.nickname", comment: "Nickname label")
        static func nicknamePlaceholder(_ placeholder: String = NSLocalizedString("auth.nickname_placeholder", comment: "Nickname placeholder")) -> String { placeholder }
        static let displayName = NSLocalizedString("auth.display_name", comment: "Display name label")
        static let backendUrl = NSLocalizedString("auth.backend_url", comment: "Backend URL label")
        static let loginButton = NSLocalizedString("auth.login_button", comment: "Login button")
        static let loggingIn = NSLocalizedString("auth.logging_in", comment: "Logging in status")
        static let logoutConfirm = NSLocalizedString("auth.logout_confirm", comment: "Logout confirmation")
        static let sessionExpired = NSLocalizedString("auth.session_expired", comment: "Session expired message")
        static let loginFailed = NSLocalizedString("auth.login_failed", comment: "Login failed message")
    }
    
    // MARK: - Navigation Tabs
    enum Tab {
        static let map = NSLocalizedString("tab.map", comment: "Map tab")
        static let feed = NSLocalizedString("tab.feed", comment: "Feed tab")
        static let tables = NSLocalizedString("tab.tables", comment: "Tables tab")
        static let leaderboard = NSLocalizedString("tab.leaderboard", comment: "Leaderboard tab")
        static let notifications = NSLocalizedString("tab.notifications", comment: "Notifications tab")
        static let profile = NSLocalizedString("tab.profile", comment: "Profile tab")
        static let chat = NSLocalizedString("tab.chat", comment: "Chat tab")
    }
    
    // MARK: - Map
    enum Map {
        static let loading = NSLocalizedString("map.loading", comment: "Map loading message")
        static let loadingError = NSLocalizedString("map.loading_error", comment: "Map error")
        static let centerLocation = NSLocalizedString("map.center_location", comment: "Center location action")
        static let liveLocation = NSLocalizedString("map.live_location", comment: "Live location")
        static let liveLocationActive = NSLocalizedString("map.live_location_active", comment: "Live location active")
        static let liveLocationInactive = NSLocalizedString("map.live_location_inactive", comment: "Live location inactive")
        static let liveLocationDenied = NSLocalizedString("map.live_location_denied", comment: "Live location denied")
        static let filters = NSLocalizedString("map.filters", comment: "Filters")
        static func venuesCount(_ count: Int) -> String {
            String(format: NSLocalizedString("map.venues_count", comment: "Venues count"), count)
        }
        static func peopleCount(_ count: Int) -> String {
            String(format: NSLocalizedString("map.people_count", comment: "People count"), count)
        }
        static func liveAt(_ venueName: String) -> String {
            String(format: NSLocalizedString("map.live_at", comment: "Live at venue"), venueName)
        }
        static let peopleActive = NSLocalizedString("map.people_active", comment: "People active now")
        static let venueDetail = NSLocalizedString("map.venue_detail", comment: "Venue detail")
        static let checkIn = NSLocalizedString("map.check_in", comment: "Check-in action")
        static let planNight = NSLocalizedString("map.plan_night", comment: "Plan night out")
        static let launchFlare = NSLocalizedString("map.launch_flare", comment: "Launch flare")
        static let flareLaunched = NSLocalizedString("map.flare_launched", comment: "Flare launched")
        static let areaDensity = NSLocalizedString("map.area_density", comment: "Area density")
        static let hotVenueSuggestion = NSLocalizedString("map.hot_venue_suggestion", comment: "Hot venue suggestion")
        static let hotVenueGoing = NSLocalizedString("map.hot_venue_going", comment: "Hot venue going")
        static let hotVenueMaybe = NSLocalizedString("map.hot_venue_maybe", comment: "Hot venue maybe")
        static let hotVenueNotInterested = NSLocalizedString("map.hot_venue_not_interested", comment: "Hot venue not interested")
        static let hotVenueMarkedGoing = NSLocalizedString("map.hot_venue_marked_going", comment: "Hot venue marked going")
        static let hotVenueMarkedMaybe = NSLocalizedString("map.hot_venue_marked_maybe", comment: "Hot venue marked maybe")
    }
    
    // MARK: - Feed
    enum Feed {
        static let title = NSLocalizedString("feed.title", comment: "Feed title")
        static let loading = NSLocalizedString("feed.loading", comment: "Feed loading")
        static let empty = NSLocalizedString("feed.empty", comment: "Feed empty")
        static let emptySubtitle = NSLocalizedString("feed.empty_subtitle", comment: "Feed empty subtitle")
        static let storyNew = NSLocalizedString("feed.story_new", comment: "New story")
        static let storyCreate = NSLocalizedString("feed.story_create", comment: "Create story")
        static let cardVenue = NSLocalizedString("feed.card_venue", comment: "Card venue")
        static let cardFlare = NSLocalizedString("feed.card_flare", comment: "Card flare")
        static let cardTable = NSLocalizedString("feed.card_table", comment: "Card table")
        static let cardFriend = NSLocalizedString("feed.card_friend", comment: "Card friend")
        static let cardDismiss = NSLocalizedString("feed.card_dismiss", comment: "Card dismiss")
        static let fatigueDismissed = NSLocalizedString("feed.fatigue_dismissed", comment: "Fatigue dismissed")
        static func rankDistance(_ km: Double) -> String {
            String(format: NSLocalizedString("feed.rank_distance", comment: "Rank distance"), km)
        }
        static func rankPeople(_ count: Int) -> String {
            String(format: NSLocalizedString("feed.rank_people", comment: "Rank people"), count)
        }
        static func rankEnergy(_ energy: Int) -> String {
            String(format: NSLocalizedString("feed.rank_energy", comment: "Rank energy"), energy)
        }
    }
    
    // MARK: - Chat
    enum Chat {
        static let title = NSLocalizedString("chat.title", comment: "Chat title")
        static let loading = NSLocalizedString("chat.loading", comment: "Chat loading")
        static let empty = NSLocalizedString("chat.empty", comment: "Chat empty")
        static let emptySubtitle = NSLocalizedString("chat.empty_subtitle", comment: "Chat empty subtitle")
        static func typePlaceholder(_ placeholder: String = NSLocalizedString("chat.type_placeholder", comment: "Type placeholder")) -> String { placeholder }
        static let send = NSLocalizedString("chat.send", comment: "Send")
        static let sent = NSLocalizedString("chat.sent", comment: "Sent")
        static let delivered = NSLocalizedString("chat.delivered", comment: "Delivered")
        static let read = NSLocalizedString("chat.read", comment: "Read")
        static func typing(_ name: String) -> String {
            String(format: NSLocalizedString("chat.typing", comment: "Typing"), name)
        }
        static let attachPhoto = NSLocalizedString("chat.attach_photo", comment: "Attach photo")
        static let attachFile = NSLocalizedString("chat.attach_file", comment: "Attach file")
        static let attachCamera = NSLocalizedString("chat.attach_camera", comment: "Attach camera")
        static let photoSent = NSLocalizedString("chat.photo_sent", comment: "Photo sent")
        static let fileSent = NSLocalizedString("chat.file_sent", comment: "File sent")
        static let deleteThread = NSLocalizedString("chat.delete_thread", comment: "Delete thread")
        static let deleteConfirm = NSLocalizedString("chat.delete_confirm", comment: "Delete confirm")
        static let groupCreate = NSLocalizedString("chat.group_create", comment: "Group create")
        static let groupTitle = NSLocalizedString("chat.group_title", comment: "Group title")
        static let groupMembers = NSLocalizedString("chat.group_members", comment: "Group members")
        static let groupCreated = NSLocalizedString("chat.group_created", comment: "Group created")
        static let venueChat = NSLocalizedString("chat.venue_chat", comment: "Venue chat")
    }
    
    // MARK: - Profile
    enum Profile {
        static let title = NSLocalizedString("profile.title", comment: "Profile title")
        static let edit = NSLocalizedString("profile.edit", comment: "Edit profile")
        static let editing = NSLocalizedString("profile.editing", comment: "Editing")
        static let saved = NSLocalizedString("profile.saved", comment: "Profile saved")
        static let avatarChange = NSLocalizedString("profile.avatar_change", comment: "Avatar change")
        static let avatarTakePhoto = NSLocalizedString("profile.avatar_take_photo", comment: "Avatar take photo")
        static let avatarChoose = NSLocalizedString("profile.avatar_choose", comment: "Avatar choose")
        static let avatarRemove = NSLocalizedString("profile.avatar_remove", comment: "Avatar remove")
        static let nickname = NSLocalizedString("profile.nickname", comment: "Nickname")
        static let displayName = NSLocalizedString("profile.display_name", comment: "Display name")
        static let bio = NSLocalizedString("profile.bio", comment: "Bio")
        static let bioPlaceholder = NSLocalizedString("profile.bio_placeholder", comment: "Bio placeholder")
        static let birthYear = NSLocalizedString("profile.birth_year", comment: "Birth year")
        static let gender = NSLocalizedString("profile.gender", comment: "Gender")
        static let interests = NSLocalizedString("profile.interests", comment: "Interests")
        static let interestsAdd = NSLocalizedString("profile.interests_add", comment: "Interests add")
        static let interestsSuggest = NSLocalizedString("profile.interests_suggest", comment: "Interests suggest")
        static let interestsSwipeHint = NSLocalizedString("profile.interests_swipe_hint", comment: "Interests swipe hint")
        static let stats = NSLocalizedString("profile.stats", comment: "Stats")
        static let friends = NSLocalizedString("profile.friends", comment: "Friends")
        static let stories = NSLocalizedString("profile.stories", comment: "Stories")
        static let checkIns = NSLocalizedString("profile.check_ins", comment: "Check-ins")
        static func level(_ level: Int) -> String {
            String(format: NSLocalizedString("profile.level", comment: "Level"), level)
        }
        static func points(_ points: Int) -> String {
            String(format: NSLocalizedString("profile.points", comment: "Points"), points)
        }
        static let privacy = NSLocalizedString("profile.privacy", comment: "Privacy")
        static let ghostMode = NSLocalizedString("profile.ghost_mode", comment: "Ghost mode")
        static let ghostModeDesc = NSLocalizedString("profile.ghost_mode_desc", comment: "Ghost mode description")
        static let sharePresence = NSLocalizedString("profile.share_presence", comment: "Share presence")
        static let shareIntentions = NSLocalizedString("profile.share_intentions", comment: "Share intentions")
        static let notifications = NSLocalizedString("profile.notifications", comment: "Notifications")
        static let logout = NSLocalizedString("profile.logout", comment: "Logout")
        static let archive = NSLocalizedString("profile.archive", comment: "Archive")
        static let badges = NSLocalizedString("profile.badges", comment: "Badges")
        static let missions = NSLocalizedString("profile.missions", comment: "Missions")
    }
    
    // MARK: - Errors
    enum Error {
        static let generic = NSLocalizedString("error.generic", comment: "Generic error")
        static let network = NSLocalizedString("error.network", comment: "Network error")
        static let unauthorized = NSLocalizedString("error.unauthorized", comment: "Unauthorized")
        static let notFound = NSLocalizedString("error.not_found", comment: "Not found")
        static let server = NSLocalizedString("error.server", comment: "Server error")
        static let timeout = NSLocalizedString("error.timeout", comment: "Timeout")
        static let noLocation = NSLocalizedString("error.no_location", comment: "No location")
        static let cameraDenied = NSLocalizedString("error.camera_denied", comment: "Camera denied")
        static let photoLibraryDenied = NSLocalizedString("error.photo_library_denied", comment: "Photo library denied")
        static let uploadFailed = NSLocalizedString("error.upload_failed", comment: "Upload failed")
        static let fileTooLarge = NSLocalizedString("error.file_too_large", comment: "File too large")
    }
    
    // MARK: - Accessibility
    enum A11y {
        static let map = NSLocalizedString("a11y.map", comment: "Accessibility map")
        static let feed = NSLocalizedString("a11y.feed", comment: "Accessibility feed")
        static let profile = NSLocalizedString("a11y.profile", comment: "Accessibility profile")
        static let notifications = NSLocalizedString("a11y.notifications", comment: "Accessibility notifications")
        static let settings = NSLocalizedString("a11y.settings", comment: "Accessibility settings")
        static let close = NSLocalizedString("a11y.close", comment: "Accessibility close")
        static let back = NSLocalizedString("a11y.back", comment: "Accessibility back")
        static let next = NSLocalizedString("a11y.next", comment: "Accessibility next")
        static let previous = NSLocalizedString("a11y.previous", comment: "Accessibility previous")
        static let refresh = NSLocalizedString("a11y.refresh", comment: "Accessibility refresh")
        static let loading = NSLocalizedString("a11y.loading", comment: "Accessibility loading")
        static let image = NSLocalizedString("a11y.image", comment: "Accessibility image")
        static func avatar(_ name: String) -> String {
            String(format: NSLocalizedString("a11y.avatar", comment: "Accessibility avatar"), name)
        }
        static func venue(_ name: String) -> String {
            String(format: NSLocalizedString("a11y.venue", comment: "Accessibility venue"), name)
        }
        static func story(_ name: String) -> String {
            String(format: NSLocalizedString("a11y.story", comment: "Accessibility story"), name)
        }
        static let sendMessage = NSLocalizedString("a11y.send_message", comment: "Accessibility send message")
        static let centerLocation = NSLocalizedString("a11y.center_location", comment: "Accessibility center location")
    }
    
    // MARK: - Time
    enum Time {
        static let now = NSLocalizedString("time.now", comment: "Now")
        static let today = NSLocalizedString("time.today", comment: "Today")
        static let yesterday = NSLocalizedString("time.yesterday", comment: "Yesterday")
        static let tomorrow = NSLocalizedString("time.tomorrow", comment: "Tomorrow")
        static func minutesAgo(_ minutes: Int) -> String {
            String(format: NSLocalizedString("time.minutes_ago", comment: "Minutes ago"), minutes)
        }
        static func hoursAgo(_ hours: Int) -> String {
            String(format: NSLocalizedString("time.hours_ago", comment: "Hours ago"), hours)
        }
        static func daysAgo(_ days: Int) -> String {
            String(format: NSLocalizedString("time.days_ago", comment: "Days ago"), days)
        }
        static let justNow = NSLocalizedString("time.just_now", comment: "Just now")
        static func expiresIn(_ time: String) -> String {
            String(format: NSLocalizedString("time.expires_in", comment: "Expires in"), time)
        }
    }
}

// MARK: - String extension for localized strings

extension String {
    var localized: String {
        NSLocalizedString(self, comment: "")
    }
    
    func localized(with arguments: CVarArg...) -> String {
        String(format: NSLocalizedString(self, comment: ""), arguments: arguments)
    }
}
