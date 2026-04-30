using FriendMap.Api.Data;
using FriendMap.Api.Models;
using FriendMap.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FriendMap.Api.Endpoints;

public static class GamificationEndpoints
{
    public static RouteGroupBuilder MapGamificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/gamification").WithTags("Gamification").RequireAuthorization();
        group.MapGet("/me", GetMyGamificationAsync);
        group.MapGet("/leaderboard", GetLeaderboardAsync);
        group.MapGet("/missions", GetWeeklyMissionsAsync);
        group.MapPost("/check", CheckAchievementsAsync);
        return group;
    }

    private static async Task<IResult> GetMyGamificationAsync(
        ClaimsPrincipal user,
        AppDbContext db,
        AdminOpsStateService adminOps,
        CancellationToken ct)
    {
        var currentUserId = CurrentUser.GetUserId(user);
        if (currentUserId == Guid.Empty)
        {
            return Results.Forbid();
        }

        var now = DateTimeOffset.UtcNow;
        var score = await BuildUserScoreAsync(db, currentUserId, null, now, ct);
        var badges = await db.UserAchievements
            .AsNoTracking()
            .Where(x => x.UserId == currentUserId)
            .OrderByDescending(x => x.EarnedAtUtc)
            .Select(x => new UserBadgeDto(x.BadgeCode, BadgeTitle(x.BadgeCode), x.EarnedAtUtc))
            .ToListAsync(ct);
        var missions = await BuildWeeklyMissionsAsync(db, currentUserId, now, adminOps, ct);

        return Results.Ok(new GamificationSummaryDto(
            score.TotalPoints,
            score.WeeklyPoints,
            score.Level,
            score.LevelProgress,
            score.PrimaryCity,
            badges,
            missions,
            "Punti calcolati da azioni sociali reali con cap anti-spam giornalieri: check-in, intenzioni, stories, tavoli, flare, commenti, like ricevuti, valutazioni verificate e nuove connessioni. Le valutazioni segnalate come false tolgono punti."));
    }

    private static async Task<IResult> GetLeaderboardAsync(
        string? city,
        string? zone,
        int limit,
        ClaimsPrincipal user,
        AppDbContext db,
        CancellationToken ct)
    {
        var currentUserId = CurrentUser.GetUserId(user);
        if (currentUserId == Guid.Empty)
        {
            return Results.Forbid();
        }

        var now = DateTimeOffset.UtcNow;
        var normalizedLimit = Math.Clamp(limit <= 0 ? 30 : limit, 5, 100);
        var userIds = await db.Users.AsNoTracking().Select(x => x.Id).ToListAsync(ct);
        var scope = new GamificationScope(city, zone);

        var scores = new List<UserScoreSnapshot>();
        foreach (var userId in userIds)
        {
            var score = await BuildUserScoreAsync(db, userId, scope, now, ct);
            if (score.TotalPoints > 0)
            {
                scores.Add(score);
            }
        }

        var ranked = scores
            .OrderByDescending(x => x.WeeklyPoints)
            .ThenByDescending(x => x.TotalPoints)
            .Take(normalizedLimit)
            .ToList();

        var users = await db.Users
            .AsNoTracking()
            .Where(x => ranked.Select(r => r.UserId).Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);

        var entries = ranked.Select((score, index) =>
        {
            var u = users[score.UserId];
            return new LeaderboardEntryDto(
                index + 1,
                score.UserId,
                u.Nickname,
                u.DisplayName,
                u.AvatarUrl,
                score.TotalPoints,
                score.WeeklyPoints,
                score.Level,
                score.PrimaryCity,
                score.UserId == currentUserId);
        }).ToList();

        return Results.Ok(new LeaderboardDto(
            string.IsNullOrWhiteSpace(city) ? "Italia" : city.Trim(),
            string.IsNullOrWhiteSpace(zone) ? null : zone.Trim(),
            now,
            entries));
    }

    private static async Task<IResult> GetWeeklyMissionsAsync(
        ClaimsPrincipal user,
        AppDbContext db,
        AdminOpsStateService adminOps,
        CancellationToken ct)
    {
        var currentUserId = CurrentUser.GetUserId(user);
        if (currentUserId == Guid.Empty)
        {
            return Results.Forbid();
        }

        return Results.Ok(await BuildWeeklyMissionsAsync(db, currentUserId, DateTimeOffset.UtcNow, adminOps, ct));
    }

    private static async Task<IResult> CheckAchievementsAsync(
        ClaimsPrincipal user,
        AppDbContext db,
        CancellationToken ct)
    {
        var currentUserId = CurrentUser.GetUserId(user);
        if (currentUserId == Guid.Empty)
        {
            return Results.Forbid();
        }

        var score = await BuildUserScoreAsync(db, currentUserId, null, DateTimeOffset.UtcNow, ct);
        var existingBadges = (await db.UserAchievements
            .AsNoTracking()
            .Where(x => x.UserId == currentUserId)
            .Select(x => x.BadgeCode)
            .ToListAsync(ct))
            .ToHashSet();

        var newAchievements = new List<UserAchievement>();
        Award("first_checkin", score.CheckIns >= 1);
        Award("explorer_10", score.DistinctVenues >= 10);
        Award("story_maker", score.Stories >= 3);
        Award("table_spark", score.TablesHosted + score.TablesJoined >= 1);
        Award("intent_planner", score.Intentions >= 3);
        Award("social_spark", score.Comments >= 5 || score.LikesReceived >= 10);
        Award("flare_starter", score.FlaresLaunched >= 3);
        Award("connector_5", score.AcceptedFriends >= 5);
        Award("trusted_reviewer", score.VerifiedRatings >= 3);
        Award("weekly_heat", score.WeeklyPoints >= 250);

        if (newAchievements.Count > 0)
        {
            db.UserAchievements.AddRange(newAchievements);
            await db.SaveChangesAsync(ct);
        }

        return Results.Ok(new
        {
            awarded = newAchievements.Select(x => x.BadgeCode),
            points = score.TotalPoints,
            level = score.Level
        });

        void Award(string badgeCode, bool condition)
        {
            if (condition && !existingBadges.Contains(badgeCode))
            {
                newAchievements.Add(new UserAchievement { UserId = currentUserId, BadgeCode = badgeCode });
            }
        }
    }

    private static async Task<List<WeeklyMissionDto>> BuildWeeklyMissionsAsync(
        AppDbContext db,
        Guid userId,
        DateTimeOffset now,
        AdminOpsStateService adminOps,
        CancellationToken ct)
    {
        var weekStart = WeekStart(now);

        var distinctVenues = await db.VenueCheckIns
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.CreatedAtUtc >= weekStart)
            .Select(x => x.VenueId)
            .Distinct()
            .CountAsync(ct);

        var storyCount = await db.UserStories
            .AsNoTracking()
            .CountAsync(x => x.UserId == userId && x.VenueId != null && x.CreatedAtUtc >= weekStart, ct);

        var intentionCount = await db.VenueIntentions
            .AsNoTracking()
            .CountAsync(x => x.UserId == userId && x.CreatedAtUtc >= weekStart, ct);

        var hostedTables = await db.SocialTables
            .AsNoTracking()
            .CountAsync(x => x.HostUserId == userId && x.CreatedAtUtc >= weekStart, ct);

        var joinedTables = await db.SocialTableParticipants
            .AsNoTracking()
            .CountAsync(x => x.UserId == userId && x.Status == "accepted" && x.CreatedAtUtc >= weekStart, ct);

        var verifiedRatings = await db.VenueRatings
            .AsNoTracking()
            .CountAsync(x => x.UserId == userId && x.IsVerifiedVisit && !x.IsFlagged && x.CreatedAtUtc >= weekStart, ct);

        var flareRelays = await db.FlareRelayAudits
            .AsNoTracking()
            .CountAsync(x => x.SenderUserId == userId && x.CreatedAtUtc >= weekStart, ct);

        var invitedFriends = await db.FriendRelations
            .AsNoTracking()
            .CountAsync(x => x.RequesterId == userId && x.CreatedAtUtc >= weekStart, ct);

        var metricProgress = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["check_in"] = distinctVenues,
            ["venue_story"] = storyCount,
            ["verified_rating"] = verifiedRatings,
            ["table_join"] = hostedTables + joinedTables,
            ["flare_relay"] = flareRelays,
            ["invite_friend"] = invitedFriends
        };

        var missions = new List<WeeklyMissionDto>
        {
            Mission("weekly_explorer", "Giro nuovo", "Visita 3 locali diversi questa settimana", "mappin.and.ellipse", distinctVenues, 3, 120),
            Mission("weekly_story", "Racconta la serata", "Posta 2 stories taggate a un locale", "camera.fill", storyCount, 2, 100),
            Mission("weekly_group", "Muovi il gruppo", "Crea un tavolo o dichiara 2 intenzioni", "person.3.fill", hostedTables + joinedTables + intentionCount, 2, 150),
            Mission("weekly_reviewer", "Occhio locale", "Valuta 3 locali che hai davvero vissuto", "star.fill", verifiedRatings, 3, 90)
        };

        foreach (var adventure in adminOps.GetAdventures().Where(x => x.IsActive))
        {
            foreach (var objective in adventure.Objectives.Where(x => x.IsActive))
            {
                var progress = metricProgress.GetValueOrDefault(objective.MetricKey);
                missions.Add(Mission(
                    $"admin_{adventure.Id:N}_{objective.Id:N}",
                    objective.Title,
                    $"{adventure.Title} · {MetricLabel(objective.MetricKey)}",
                    MetricIcon(objective.MetricKey),
                    progress,
                    objective.Target,
                    objective.RewardPoints));
            }
        }

        return missions
            .OrderBy(x => x.IsCompleted)
            .ThenByDescending(x => x.RewardPoints)
            .Take(12)
            .ToList();
    }

    private static WeeklyMissionDto Mission(string code, string title, string subtitle, string icon, int progress, int target, int points)
    {
        var clamped = Math.Clamp(progress, 0, target);
        return new WeeklyMissionDto(code, title, subtitle, icon, clamped, target, points, clamped >= target);
    }

    private static string MetricIcon(string metricKey) => metricKey switch
    {
        "venue_story" => "camera.fill",
        "verified_rating" => "star.fill",
        "table_join" => "person.3.fill",
        "flare_relay" => "flame.fill",
        "invite_friend" => "person.badge.plus",
        _ => "mappin.and.ellipse"
    };

    private static string MetricLabel(string metricKey) => metricKey switch
    {
        "venue_story" => "stories nei locali",
        "verified_rating" => "recensioni verificate",
        "table_join" => "tavoli sociali",
        "flare_relay" => "flare rilanciati",
        "invite_friend" => "inviti amici",
        _ => "visite locali"
    };

    private static async Task<UserScoreSnapshot> BuildUserScoreAsync(
        AppDbContext db,
        Guid userId,
        GamificationScope? scope,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var weekStart = WeekStart(now);
        var checkIns = await db.VenueCheckIns
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Join(db.Venues.AsNoTracking(), c => c.VenueId, v => v.Id, (c, v) => new ScoredVenueEvent(c.CreatedAtUtc, v.Id, v.City))
            .ToListAsync(ct);
        checkIns = ApplyScope(checkIns, scope);

        var stories = await db.UserStories
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .GroupJoin(db.Venues.AsNoTracking(), s => s.VenueId, v => v.Id, (s, venues) => new { Story = s, Venue = venues.FirstOrDefault() })
            .Select(x => new ScoredVenueEvent(x.Story.CreatedAtUtc, x.Story.VenueId, x.Venue == null ? null : x.Venue.City))
            .ToListAsync(ct);
        stories = ApplyScope(stories, scope);

        var intentions = await db.VenueIntentions
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Join(db.Venues.AsNoTracking(), i => i.VenueId, v => v.Id, (i, v) => new ScoredVenueEvent(i.CreatedAtUtc, v.Id, v.City))
            .ToListAsync(ct);
        intentions = ApplyScope(intentions, scope);

        var hostedTables = await db.SocialTables
            .AsNoTracking()
            .Where(x => x.HostUserId == userId)
            .Join(db.Venues.AsNoTracking(), t => t.VenueId, v => v.Id, (t, v) => new ScoredVenueEvent(t.CreatedAtUtc, v.Id, v.City))
            .ToListAsync(ct);
        hostedTables = ApplyScope(hostedTables, scope);

        var joinedTables = await db.SocialTableParticipants
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Status == "accepted")
            .Join(db.SocialTables.AsNoTracking(), p => p.SocialTableId, t => t.Id, (p, t) => new { Participant = p, Table = t })
            .Join(db.Venues.AsNoTracking(), x => x.Table.VenueId, v => v.Id, (x, v) => new ScoredVenueEvent(x.Participant.CreatedAtUtc, v.Id, v.City))
            .ToListAsync(ct);
        joinedTables = ApplyScope(joinedTables, scope);

        var flareResponses = await db.FlareResponses
            .AsNoTracking()
            .CountAsync(x => x.UserId == userId, ct);
        var weeklyFlareResponses = await db.FlareResponses
            .AsNoTracking()
            .CountAsync(x => x.UserId == userId && x.CreatedAtUtc >= weekStart, ct);

        var flaresLaunched = await db.FlareSignals
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new ScoredVenueEvent(x.CreatedAtUtc, null, null))
            .ToListAsync(ct);
        flaresLaunched = ApplyScope(flaresLaunched, scope);

        var comments = await db.UserStoryComments
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Join(db.UserStories.AsNoTracking(), c => c.UserStoryId, s => s.Id, (c, s) => new { Comment = c, Story = s })
            .GroupJoin(db.Venues.AsNoTracking(), x => x.Story.VenueId, v => v.Id, (x, venues) => new ScoredVenueEvent(x.Comment.CreatedAtUtc, x.Story.VenueId, venues.Select(v => v.City).FirstOrDefault()))
            .ToListAsync(ct);
        comments = ApplyScope(comments, scope);

        var likesReceived = await db.UserStoryReactions
            .AsNoTracking()
            .Where(x => x.UserId != userId && x.ReactionType == "like")
            .Join(db.UserStories.AsNoTracking().Where(x => x.UserId == userId), r => r.UserStoryId, s => s.Id, (r, s) => new { Reaction = r, Story = s })
            .GroupJoin(db.Venues.AsNoTracking(), x => x.Story.VenueId, v => v.Id, (x, venues) => new ScoredVenueEvent(x.Reaction.CreatedAtUtc, x.Story.VenueId, venues.Select(v => v.City).FirstOrDefault()))
            .ToListAsync(ct);
        likesReceived = ApplyScope(likesReceived, scope);

        var acceptedFriendEvents = await db.FriendRelations
            .AsNoTracking()
            .Where(x => x.Status == "accepted" && (x.RequesterId == userId || x.AddresseeId == userId))
            .Select(x => new ScoredVenueEvent(x.UpdatedAtUtc ?? x.CreatedAtUtc, null, null))
            .ToListAsync(ct);
        acceptedFriendEvents = ApplyScope(acceptedFriendEvents, scope);

        var verifiedRatings = await db.VenueRatings
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.IsVerifiedVisit && !x.IsFlagged)
            .Join(db.Venues.AsNoTracking(), r => r.VenueId, v => v.Id, (r, v) => new ScoredVenueEvent(r.CreatedAtUtc, v.Id, v.City))
            .ToListAsync(ct);
        verifiedRatings = ApplyScope(verifiedRatings, scope);

        var flaggedRatings = await db.VenueRatings
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.IsFlagged)
            .Select(x => new ScoredVenueEvent(x.FlaggedAtUtc ?? x.UpdatedAtUtc ?? x.CreatedAtUtc, x.VenueId, null))
            .ToListAsync(ct);
        flaggedRatings = ApplyScope(flaggedRatings, scope);

        var totalPoints =
            CappedDailyPoints(checkIns, 10, 5) +
            CappedDailyPoints(intentions, 6, 5) +
            CappedDailyPoints(stories, 15, 6) +
            CappedDailyPoints(hostedTables, 35, 2) +
            CappedDailyPoints(joinedTables, 20, 3) +
            CappedDailyPoints(flaresLaunched, 8, 4) +
            CappedDailyPoints(comments, 4, 12) +
            CappedDailyPoints(likesReceived, 2, 30) +
            CappedDailyPoints(verifiedRatings, 7, 5) +
            CappedDailyPoints(acceptedFriendEvents, 25, 5) +
            Math.Min(flareResponses, 20) * 5 -
            Math.Min(flaggedRatings.Count, 10) * 30;

        var weeklyPoints =
            CappedDailyPoints(checkIns.Where(x => x.CreatedAtUtc >= weekStart), 10, 5) +
            CappedDailyPoints(intentions.Where(x => x.CreatedAtUtc >= weekStart), 6, 5) +
            CappedDailyPoints(stories.Where(x => x.CreatedAtUtc >= weekStart), 15, 6) +
            CappedDailyPoints(hostedTables.Where(x => x.CreatedAtUtc >= weekStart), 35, 2) +
            CappedDailyPoints(joinedTables.Where(x => x.CreatedAtUtc >= weekStart), 20, 3) +
            CappedDailyPoints(flaresLaunched.Where(x => x.CreatedAtUtc >= weekStart), 8, 4) +
            CappedDailyPoints(comments.Where(x => x.CreatedAtUtc >= weekStart), 4, 12) +
            CappedDailyPoints(likesReceived.Where(x => x.CreatedAtUtc >= weekStart), 2, 30) +
            CappedDailyPoints(verifiedRatings.Where(x => x.CreatedAtUtc >= weekStart), 7, 5) +
            CappedDailyPoints(acceptedFriendEvents.Where(x => x.CreatedAtUtc >= weekStart), 25, 5) +
            Math.Min(weeklyFlareResponses, 10) * 5 -
            Math.Min(flaggedRatings.Count(x => x.CreatedAtUtc >= weekStart), 10) * 30;

        totalPoints = Math.Max(0, totalPoints);
        weeklyPoints = Math.Max(0, weeklyPoints);

        var level = Math.Max(1, (totalPoints / 250) + 1);
        var progress = (totalPoints % 250) / 250.0;
        var primaryCity = checkIns
            .Concat(stories)
            .Concat(intentions)
            .Concat(hostedTables)
            .Concat(joinedTables)
            .Where(x => !string.IsNullOrWhiteSpace(x.City))
            .GroupBy(x => x.City!)
            .OrderByDescending(x => x.Count())
            .Select(x => x.Key)
            .FirstOrDefault();

        return new UserScoreSnapshot(
            userId,
            totalPoints,
            weeklyPoints,
            level,
            progress,
            primaryCity,
            checkIns.Count,
            checkIns.Select(x => x.VenueId).Where(x => x != null).Distinct().Count(),
            stories.Count,
            hostedTables.Count,
            joinedTables.Count,
            intentions.Count,
            comments.Count,
            likesReceived.Count,
            flaresLaunched.Count,
            acceptedFriendEvents.Count,
            verifiedRatings.Count,
            flaggedRatings.Count);
    }

    private static List<ScoredVenueEvent> ApplyScope(List<ScoredVenueEvent> events, GamificationScope? scope)
    {
        if (scope is null)
        {
            return events;
        }

        return events.Where(x =>
            (string.IsNullOrWhiteSpace(scope.City) || string.Equals(x.City, scope.City.Trim(), StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(scope.Zone) || string.Equals(x.City, scope.Zone.Trim(), StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private static int CappedDailyPoints(IEnumerable<ScoredVenueEvent> events, int pointsPerEvent, int dailyCap)
    {
        return events
            .GroupBy(x => x.CreatedAtUtc.UtcDateTime.Date)
            .Sum(day => Math.Min(day.Count(), dailyCap) * pointsPerEvent);
    }

    private static DateTimeOffset WeekStart(DateTimeOffset now)
    {
        var date = now.UtcDateTime.Date;
        var diff = ((int)date.DayOfWeek + 6) % 7;
        return new DateTimeOffset(date.AddDays(-diff), TimeSpan.Zero);
    }

    private static string BadgeTitle(string badgeCode) => badgeCode switch
    {
        "first_checkin" => "Primo check-in",
        "explorer_10" => "Esploratore urbano",
        "story_maker" => "Story maker",
        "table_spark" => "Scintilla sociale",
        "intent_planner" => "Radar acceso",
        "social_spark" => "Conversazione viva",
        "flare_starter" => "Flare starter",
        "connector_5" => "Connettore",
        "trusted_reviewer" => "Occhio locale",
        "weekly_heat" => "Settimana calda",
        "checkin_10" => "10 check-in",
        "first_host" => "Primo tavolo",
        "host_5" => "Host seriale",
        "streak_3" => "Tre giorni in giro",
        _ => badgeCode.Replace('_', ' ')
    };

    private sealed record GamificationScope(string? City, string? Zone);
    private sealed record ScoredVenueEvent(DateTimeOffset CreatedAtUtc, Guid? VenueId, string? City);
    private sealed record UserScoreSnapshot(
        Guid UserId,
        int TotalPoints,
        int WeeklyPoints,
        int Level,
        double LevelProgress,
        string? PrimaryCity,
        int CheckIns,
        int DistinctVenues,
        int Stories,
        int TablesHosted,
        int TablesJoined,
        int Intentions,
        int Comments,
        int LikesReceived,
        int FlaresLaunched,
        int AcceptedFriends,
        int VerifiedRatings,
        int FlaggedRatings);
}

public record GamificationSummaryDto(
    int TotalPoints,
    int WeeklyPoints,
    int Level,
    double LevelProgress,
    string? PrimaryCity,
    IReadOnlyList<UserBadgeDto> Badges,
    IReadOnlyList<WeeklyMissionDto> WeeklyMissions,
    string AntiCheatNote);

public record UserBadgeDto(string Code, string Title, DateTimeOffset EarnedAtUtc);
public record WeeklyMissionDto(string Code, string Title, string Subtitle, string Icon, int Progress, int Target, int RewardPoints, bool IsCompleted);
public record LeaderboardDto(string ScopeName, string? Zone, DateTimeOffset GeneratedAtUtc, IReadOnlyList<LeaderboardEntryDto> Entries);
public record LeaderboardEntryDto(int Rank, Guid UserId, string Nickname, string? DisplayName, string? AvatarUrl, int TotalPoints, int WeeklyPoints, int Level, string? PrimaryCity, bool IsMe);
