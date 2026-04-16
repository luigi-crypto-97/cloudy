using FriendMap.Api.Models;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace FriendMap.Api.Data;

public static class DevelopmentDataSeeder
{
    public static readonly Guid GiuliaUserId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid MarcoUserId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid SofiaUserId = Guid.Parse("10000000-0000-0000-0000-000000000003");

    public static readonly Guid BreraVenueId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid NavigliVenueId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    public static readonly Guid PortaRomanaVenueId = Guid.Parse("20000000-0000-0000-0000-000000000003");

    public static readonly Guid BreraTableId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    public static readonly Guid DemoReportId = Guid.Parse("40000000-0000-0000-0000-000000000001");

    private static readonly GeometryFactory GeometryFactory = new(new PrecisionModel(), 4326);

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(ct))
        {
            var usersToBackfill = await db.Users
                .Where(x => string.IsNullOrWhiteSpace(x.AvatarUrl) || string.IsNullOrWhiteSpace(x.Bio))
                .ToListAsync(ct);

            if (usersToBackfill.Count > 0)
            {
                foreach (var user in usersToBackfill)
                {
                    if (string.IsNullOrWhiteSpace(user.AvatarUrl))
                    {
                        user.AvatarUrl = BuildDevAvatarUrl(user.Nickname);
                    }

                    if (string.IsNullOrWhiteSpace(user.Bio))
                    {
                        user.Bio = BuildDevBio(user.Nickname);
                    }

                    user.UpdatedAtUtc = DateTimeOffset.UtcNow;
                }

                await db.SaveChangesAsync(ct);
            }

            if (!await db.UserInterests.AnyAsync(ct))
            {
                db.UserInterests.AddRange(
                    new UserInterest { UserId = GiuliaUserId, Tag = "aperitivi" },
                    new UserInterest { UserId = GiuliaUserId, Tag = "cinema" },
                    new UserInterest { UserId = MarcoUserId, Tag = "musica" },
                    new UserInterest { UserId = MarcoUserId, Tag = "sport" },
                    new UserInterest { UserId = SofiaUserId, Tag = "lettura" },
                    new UserInterest { UserId = SofiaUserId, Tag = "brunch" });

                await db.SaveChangesAsync(ct);
            }

            return;
        }

        var now = DateTimeOffset.UtcNow;
        var bucketStart = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute / 15 * 15, 0, TimeSpan.Zero);

        db.Users.AddRange(
            new AppUser { Id = GiuliaUserId, Nickname = "giulia", DisplayName = "Giulia Negri", AvatarUrl = BuildDevAvatarUrl("giulia"), Bio = "Aperitivi, cinema e tavoli improvvisati.", BirthYear = 1997, Gender = "female" },
            new AppUser { Id = MarcoUserId, Nickname = "marco", DisplayName = "Marco Lodi", AvatarUrl = BuildDevAvatarUrl("marco"), Bio = "Musica live e locali dove si parla davvero.", BirthYear = 1994, Gender = "male" },
            new AppUser { Id = SofiaUserId, Nickname = "sofia", DisplayName = "Sofia Riva", AvatarUrl = BuildDevAvatarUrl("sofia"), Bio = "Brunch, lettura e serate tranquille.", BirthYear = 1999, Gender = "female" });

        db.UserInterests.AddRange(
            new UserInterest { UserId = GiuliaUserId, Tag = "aperitivi" },
            new UserInterest { UserId = GiuliaUserId, Tag = "cinema" },
            new UserInterest { UserId = MarcoUserId, Tag = "musica" },
            new UserInterest { UserId = MarcoUserId, Tag = "sport" },
            new UserInterest { UserId = SofiaUserId, Tag = "lettura" },
            new UserInterest { UserId = SofiaUserId, Tag = "brunch" });

        db.FriendRelations.AddRange(
            new FriendRelation { RequesterId = GiuliaUserId, AddresseeId = MarcoUserId, Status = "accepted" },
            new FriendRelation { RequesterId = GiuliaUserId, AddresseeId = SofiaUserId, Status = "accepted" },
            new FriendRelation { RequesterId = MarcoUserId, AddresseeId = SofiaUserId, Status = "accepted" });

        db.Venues.AddRange(
            new Venue
            {
                Id = BreraVenueId,
                ExternalProviderId = "dev-milano-001",
                Name = "Bar Brera Demo",
                Category = "bar",
                AddressLine = "Via Brera 12",
                City = "Milano",
                Location = CreatePoint(9.1876, 45.4720)
            },
            new Venue
            {
                Id = NavigliVenueId,
                ExternalProviderId = "dev-milano-002",
                Name = "Navigli Social Club",
                Category = "club",
                AddressLine = "Ripa di Porta Ticinese 21",
                City = "Milano",
                Location = CreatePoint(9.1744, 45.4522)
            },
            new Venue
            {
                Id = PortaRomanaVenueId,
                ExternalProviderId = "dev-milano-003",
                Name = "Porta Romana Cafe",
                Category = "cafe",
                AddressLine = "Corso di Porta Romana 88",
                City = "Milano",
                Location = CreatePoint(9.2024, 45.4527)
            });

        db.VenueCheckIns.AddRange(
            new VenueCheckIn { UserId = GiuliaUserId, VenueId = BreraVenueId, ExpiresAtUtc = now.AddHours(2) },
            new VenueCheckIn { UserId = MarcoUserId, VenueId = BreraVenueId, ExpiresAtUtc = now.AddMinutes(90) },
            new VenueCheckIn { UserId = SofiaUserId, VenueId = NavigliVenueId, ExpiresAtUtc = now.AddHours(3) });

        db.VenueIntentions.AddRange(
            new VenueIntention { UserId = GiuliaUserId, VenueId = NavigliVenueId, StartsAtUtc = now.AddHours(1), EndsAtUtc = now.AddHours(4), Note = "Aperitivo" },
            new VenueIntention { UserId = MarcoUserId, VenueId = PortaRomanaVenueId, StartsAtUtc = now.AddHours(2), EndsAtUtc = now.AddHours(5), Note = "Dopo cena" });

        db.SocialTables.Add(new SocialTable
        {
            Id = BreraTableId,
            VenueId = BreraVenueId,
            HostUserId = GiuliaUserId,
            Title = "Tavolo demo Brera",
            Description = "Seed locale per sviluppo",
            StartsAtUtc = now.AddHours(2),
            Capacity = 6,
            JoinPolicy = "auto"
        });

        db.ModerationReports.Add(new ModerationReport
        {
            Id = DemoReportId,
            ReporterUserId = MarcoUserId,
            ReportedVenueId = NavigliVenueId,
            ReasonCode = "inappropriate_content",
            Details = "Report demo per sviluppo admin"
        });

        db.VenueAffluenceSnapshots.AddRange(
            CreateSnapshot(BreraVenueId, bucketStart, 2),
            CreateSnapshot(NavigliVenueId, bucketStart, 2),
            CreateSnapshot(PortaRomanaVenueId, bucketStart, 1));

        await db.SaveChangesAsync(ct);
    }

    private static Point CreatePoint(double longitude, double latitude)
    {
        return GeometryFactory.CreatePoint(new Coordinate(longitude, latitude));
    }

    private static string BuildDevAvatarUrl(string nickname)
    {
        return $"https://i.pravatar.cc/160?u={Uri.EscapeDataString(nickname)}";
    }

    private static string BuildDevBio(string nickname)
    {
        return nickname.Trim().ToLowerInvariant() switch
        {
            "giulia" => "Aperitivi, cinema e tavoli improvvisati.",
            "marco" => "Musica live e locali dove si parla davvero.",
            "sofia" => "Brunch, lettura e serate tranquille.",
            _ => "Profilo development FriendMap."
        };
    }

    private static VenueAffluenceSnapshot CreateSnapshot(Guid venueId, DateTimeOffset bucketStart, int activeUsers)
    {
        return new VenueAffluenceSnapshot
        {
            VenueId = venueId,
            BucketStartUtc = bucketStart,
            BucketEndUtc = bucketStart.AddMinutes(15),
            ActiveUsersEstimated = activeUsers,
            DensityLevel = activeUsers switch
            {
                < 5 => "very_low",
                < 15 => "low",
                < 30 => "medium",
                < 60 => "high",
                _ => "very_high"
            },
            IsSuppressedForPrivacy = true
        };
    }
}
