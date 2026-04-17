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
    public static readonly Guid FolkPubVenueId = Guid.Parse("20000000-0000-0000-0000-000000000004");
    public static readonly Guid ConvivioVenueId = Guid.Parse("20000000-0000-0000-0000-000000000005");
    public static readonly Guid FanariVenueId = Guid.Parse("20000000-0000-0000-0000-000000000006");
    public static readonly Guid MoodCafeVenueId = Guid.Parse("20000000-0000-0000-0000-000000000007");
    public static readonly Guid NonnaAdelmaVenueId = Guid.Parse("20000000-0000-0000-0000-000000000008");
    public static readonly Guid InOutVenueId = Guid.Parse("20000000-0000-0000-0000-000000000009");
    public static readonly Guid StreetTasteVenueId = Guid.Parse("20000000-0000-0000-0000-000000000010");
    public static readonly Guid RaricaVenueId = Guid.Parse("20000000-0000-0000-0000-000000000011");
    public static readonly Guid LaCucina42VenueId = Guid.Parse("20000000-0000-0000-0000-000000000012");
    public static readonly Guid LoveItVenueId = Guid.Parse("20000000-0000-0000-0000-000000000013");
    public static readonly Guid MurneeVenueId = Guid.Parse("20000000-0000-0000-0000-000000000014");
    public static readonly Guid LArmonicaVenueId = Guid.Parse("20000000-0000-0000-0000-000000000015");
    public static readonly Guid EmmaCaffeVenueId = Guid.Parse("20000000-0000-0000-0000-000000000016");
    public static readonly Guid BerBeneVenueId = Guid.Parse("20000000-0000-0000-0000-000000000017");
    public static readonly Guid OroRossoVenueId = Guid.Parse("20000000-0000-0000-0000-000000000018");
    public static readonly Guid RoadhouseVenueId = Guid.Parse("20000000-0000-0000-0000-000000000019");
    public static readonly Guid IlCastagnoVenueId = Guid.Parse("20000000-0000-0000-0000-000000000020");
    public static readonly Guid LaBottegaVinoVenueId = Guid.Parse("20000000-0000-0000-0000-000000000021");

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

            await EnsureTradateVenueSeedAsync(db, ct);

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

        foreach (var seedVenue in GetTradateSeedVenues())
        {
            db.Venues.Add(CreateVenue(seedVenue));
        }

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
            CreateSnapshot(PortaRomanaVenueId, bucketStart, 1),
            CreateSnapshot(FolkPubVenueId, bucketStart, 7),
            CreateSnapshot(ConvivioVenueId, bucketStart, 9),
            CreateSnapshot(FanariVenueId, bucketStart, 5),
            CreateSnapshot(MoodCafeVenueId, bucketStart, 4),
            CreateSnapshot(NonnaAdelmaVenueId, bucketStart, 6),
            CreateSnapshot(InOutVenueId, bucketStart, 12),
            CreateSnapshot(StreetTasteVenueId, bucketStart, 10),
            CreateSnapshot(RaricaVenueId, bucketStart, 6),
            CreateSnapshot(LaCucina42VenueId, bucketStart, 7),
            CreateSnapshot(LoveItVenueId, bucketStart, 8),
            CreateSnapshot(MurneeVenueId, bucketStart, 6),
            CreateSnapshot(LArmonicaVenueId, bucketStart, 9),
            CreateSnapshot(EmmaCaffeVenueId, bucketStart, 5),
            CreateSnapshot(BerBeneVenueId, bucketStart, 4),
            CreateSnapshot(OroRossoVenueId, bucketStart, 11),
            CreateSnapshot(RoadhouseVenueId, bucketStart, 13),
            CreateSnapshot(IlCastagnoVenueId, bucketStart, 7),
            CreateSnapshot(LaBottegaVinoVenueId, bucketStart, 3));

        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureTradateVenueSeedAsync(AppDbContext db, CancellationToken ct)
    {
        var changed = false;
        var seedVenues = GetTradateSeedVenues();

        foreach (var seedVenue in seedVenues)
        {
            var venue = await db.Venues.FirstOrDefaultAsync(x => x.Id == seedVenue.Id, ct);
            if (venue is null)
            {
                db.Venues.Add(CreateVenue(seedVenue));
                changed = true;
                continue;
            }

            changed |= ApplySeedVenue(venue, seedVenue);
        }

        var now = DateTimeOffset.UtcNow;
        var bucketStart = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute / 15 * 15, 0, TimeSpan.Zero);

        foreach (var seedVenue in seedVenues)
        {
            var snapshot = await db.VenueAffluenceSnapshots
                .FirstOrDefaultAsync(x => x.VenueId == seedVenue.Id && x.BucketStartUtc == bucketStart, ct);

            if (snapshot is null)
            {
                db.VenueAffluenceSnapshots.Add(CreateSnapshot(seedVenue.Id, bucketStart, seedVenue.SeedPeopleCount));
                changed = true;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync(ct);
        }
    }

    private static IReadOnlyList<SeedVenueDefinition> GetTradateSeedVenues()
    {
        return new[]
        {
            new SeedVenueDefinition(
                FolkPubVenueId,
                "dev-tradate-001",
                "Folk Pub",
                "pub",
                "Corso Paolo Bernacchi 130",
                "Tradate",
                "IT",
                "+39 339 328 8827",
                "https://www.folkpub.it/",
                "Lun-Sab 18:00-01:00 • Dom chiuso",
                BuildSeedCoverUrl("folk-pub-tradate"),
                "Pub storico di Tradate per birre, aperitivi serali e tavoli informali tra amici.",
                "pub,birra,aperitivo,prenotazione",
                45.7123131,
                8.9062790,
                7),
            new SeedVenueDefinition(
                ConvivioVenueId,
                "dev-tradate-002",
                "Convivio Cafe Bistrot",
                "cafe",
                "Corso Paolo Bernacchi 92",
                "Tradate",
                "IT",
                "+39 346 374 2240",
                "http://www.conviviotradate.it",
                "Lun-Gio 07:30-22:00 • Ven 07:30-24:00 • Sab 08:30-24:00",
                BuildSeedCoverUrl("convivio-tradate"),
                "Cafe bistrot centrale per colazioni, pranzi veloci, aperitivi e wine bar.",
                "colazione,bistrot,wine bar,aperitivo",
                45.7129500,
                8.9057500,
                9),
            new SeedVenueDefinition(
                FanariVenueId,
                "dev-tradate-003",
                "Fanari Pub",
                "pub",
                "Via Crocefisso 33",
                "Tradate",
                "IT",
                "+39 340 729 7256",
                null,
                "Lun-Gio 09:00-24:00 • Ven 09:00-01:00 • Sab 10:00-01:00",
                BuildSeedCoverUrl("fanari-tradate"),
                "Pub con servizio pranzo e cena, tavoli all'aperto e orario lungo fino a notte.",
                "pub,cena,pranzo,tavoli all'aperto",
                45.7126775,
                8.9043286,
                5),
            new SeedVenueDefinition(
                MoodCafeVenueId,
                "dev-tradate-004",
                "Mood Cafe",
                "cafe",
                "Via Bruno Passerini 16",
                "Tradate",
                "IT",
                "+39 375 631 5767",
                "https://www.facebook.com/moodcafetradate/",
                "Lun-Sab 06:30-20:30 • Dom chiuso",
                BuildSeedCoverUrl("mood-cafe-tradate"),
                "Cafe contemporaneo per colazioni, pranzi leggeri e pausa pomeridiana in centro.",
                "cafe,brunch,colazione,lunch",
                45.7099140,
                8.9005528,
                4),
            new SeedVenueDefinition(
                NonnaAdelmaVenueId,
                "dev-tradate-005",
                "Trattoria Nonna Adelma",
                "restaurant",
                "Via Alessandro Manzoni 32",
                "Tradate",
                "IT",
                "+39 0331 841800",
                "https://trattorianonnaadelma.eatbu.com/?lang=en",
                "Lun-Mar 11:30-15:00 • Mer-Sab 11:30-15:00, 18:30-23:00",
                BuildSeedCoverUrl("nonna-adelma-tradate"),
                "Trattoria classica per pranzo e cena con formula più rilassata da tavolo.",
                "cena,trattoria,prenotazione,italiano",
                45.7162781,
                8.9038220,
                6),
            new SeedVenueDefinition(
                InOutVenueId,
                "dev-tradate-006",
                "In & Out",
                "restaurant",
                "Via Gradisca 14",
                "Tradate",
                "IT",
                "+39 0331 852209",
                "https://www.inouttradate.it/",
                "Tutti i giorni 07:00-23:30",
                BuildSeedCoverUrl("inout-tradate"),
                "Food and drink con colazioni, aperitivi, pizza serale e spazio eventi.",
                "aperitivo,pizza,cena,eventi,prenotazione",
                45.7089973,
                8.9110745,
                12),
            new SeedVenueDefinition(
                StreetTasteVenueId,
                "dev-tradate-007",
                "Street Taste",
                "cocktail-bar",
                "Via Monte Grappa 75A",
                "Tradate",
                "IT",
                "+39 0331 848247",
                "https://www.streettradate.it/",
                "Lun 18:00-23:00 • Mar chiuso • Mer 18:00-23:00 • Gio 18:00-24:00 • Ven 18:00-01:00 • Sab 12:00-15:00, 18:00-01:30 • Dom 18:00-24:00",
                BuildSeedCoverUrl("street-taste-tradate"),
                "Cocktail bar con grill, aperitivi e serate più dinamiche rispetto ai locali del centro.",
                "cocktail,aperitivo,grill,musica,prenotazione",
                45.7235108,
                8.8922930,
                10),
            new SeedVenueDefinition(
                RaricaVenueId,
                "dev-tradate-008",
                "Rarica",
                "bakery",
                "Via Monte Grappa 69",
                "Tradate",
                "IT",
                "+39 0331 841771",
                "https://www.rarica.it/",
                "Mar-Dom 08:00-14:00, 16:00-20:00 • Lun chiuso",
                BuildSeedCoverUrl("rarica-tradate"),
                "Pasticceria e rosticceria palermitana, ideale per merenda, take-away e pranzi veloci.",
                "dolci,rosticceria,asporto,colazione",
                45.7171810,
                8.8984499,
                6),
            new SeedVenueDefinition(
                LaCucina42VenueId,
                "dev-tradate-009",
                "La Cucina del Quarantadue",
                "restaurant",
                "Corso Giacomo Matteotti 42",
                "Tradate",
                "IT",
                "+39 0331 852530",
                null,
                "Mar-Sab 12:00-15:00, 19:30-24:00 • Dom-Lun chiuso",
                BuildSeedCoverUrl("cucina-quarantadue-tradate"),
                "Ristorante per pranzi e cene con taglio più classico, adatto anche a prenotazioni tranquille.",
                "cena,ristorante,pranzo,prenotazione",
                45.7150224,
                8.9033736,
                7),
            new SeedVenueDefinition(
                LoveItVenueId,
                "dev-tradate-010",
                "Love It",
                "pizza-bar",
                "Corso Paolo Bernacchi 146",
                "Tradate",
                "IT",
                "+39 0331 1838775",
                null,
                "Mar-Mer 09:00-15:00, 17:00-23:00 • Gio 09:00-15:00, 17:00-23:30 • Ven 09:00-15:00, 17:00-24:00 • Sab 09:00-24:00 • Dom 17:00-23:30",
                BuildSeedCoverUrl("love-it-tradate"),
                "Locale più pop con pizza, poke, brunch e aperitivo in fascia stazione/centro.",
                "pizza,brunch,aperitivo,poke",
                45.7131500,
                8.9067800,
                8),
            new SeedVenueDefinition(
                MurneeVenueId,
                "dev-tradate-011",
                "Murnee Bistrot",
                "bistrot",
                "Via Sopranzi 18",
                "Tradate",
                "IT",
                "+39 0331 849358",
                "https://www.facebook.com/100083594131328",
                "Sun-Mon 18:00-24:00 • Tue-Thu 07:00-15:00, 18:00-24:00 • Fri-Sab 07:00-15:00, 18:00-01:30",
                BuildSeedCoverUrl("murnee-tradate"),
                "Bistrot con doppia anima: caffetteria di giorno e cena/drink la sera.",
                "bistrot,cena,drink,colazione",
                45.7154953,
                8.9070174,
                6),
            new SeedVenueDefinition(
                LArmonicaVenueId,
                "dev-tradate-012",
                "L'Armonica",
                "restaurant",
                "Via Vincenzo Monti 6",
                "Tradate",
                "IT",
                "+39 331 161 1851",
                "https://www.ristorantelarmonica.com/",
                "Lun-Mar, Gio-Sab 19:30-22:30 • Dom 12:30-15:30",
                BuildSeedCoverUrl("larmonica-tradate"),
                "Ristorante mediterraneo più curato, adatto a cena con prenotazione e occasioni più calme.",
                "cena,mediterraneo,vegetariano,prenotazione",
                45.7167202,
                8.9058848,
                9),
            new SeedVenueDefinition(
                EmmaCaffeVenueId,
                "dev-tradate-013",
                "Emma Caffe",
                "cafe",
                "Via Monte Grappa 41",
                "Tradate",
                "IT",
                "+39 0331 117 5469",
                "https://www.emmacaffe.it/",
                "Mar-Ven 05:30-21:30 • Sab 06:30-21:30 • Dom 07:30-13:30, 15:30-21:30",
                BuildSeedCoverUrl("emma-caffe-tradate"),
                "Caffetteria recente con colazioni, aperitivi e apericena in una fascia più everyday.",
                "caffetteria,colazione,aperitivo,apericena",
                45.7161000,
                8.8996000,
                5),
            new SeedVenueDefinition(
                BerBeneVenueId,
                "dev-tradate-014",
                "Enoteca Bar BerBene",
                "wine-bar",
                "Corso Paolo Bernacchi 97",
                "Tradate",
                "IT",
                "+39 0331 844584",
                "https://www.enotecaberbene.it/",
                "Mar-Sab 07:30-21:30 • Dom-Lun chiuso",
                BuildSeedCoverUrl("berbene-tradate"),
                "Enoteca urbana per aperitivi, vini e piccoli piatti nel cuore di Tradate.",
                "enoteca,aperitivo,vino,prenotazione",
                45.7132500,
                8.9059000,
                4),
            new SeedVenueDefinition(
                OroRossoVenueId,
                "dev-tradate-015",
                "Oro Rosso",
                "restaurant",
                "Via Guglielmo Marconi 26",
                "Tradate",
                "IT",
                "+39 0331 852703",
                "http://www.ororossoristorante.it",
                "Mar-Ven 12:00-14:30, 19:00-23:30 • Sab 19:00-00:30 • Dom 12:00-14:30, 19:00-23:30",
                BuildSeedCoverUrl("oro-rosso-tradate"),
                "Ristorante più classico con pizza e cucina italiana, vicino alla stazione.",
                "ristorante,pizza,cena,prenotazione",
                45.7115000,
                8.9075000,
                11),
            new SeedVenueDefinition(
                RoadhouseVenueId,
                "dev-tradate-016",
                "Roadhouse Restaurant Tradate",
                "restaurant",
                "Via Monte S. Michele 52",
                "Tradate",
                "IT",
                "+39 0331 841680",
                "https://www.roadhouse.it/",
                "Tutti i giorni 12:00-15:00, 18:30-22:30 • Ven-Sab fino a 23:30",
                BuildSeedCoverUrl("roadhouse-tradate"),
                "Catena casual dining per burger e carne, utile per gruppi e serate più semplici.",
                "burger,gruppi,cena,prenotazione",
                45.7198000,
                8.9182000,
                13),
            new SeedVenueDefinition(
                IlCastagnoVenueId,
                "dev-tradate-017",
                "Il Castagno",
                "restaurant",
                "Via Dei Cappuccini 47",
                "Tradate",
                "IT",
                "+39 349 354 3139",
                "https://www.agriilcastagno.com/",
                "Prenotazione consigliata • agriturismo/ristorante nel verde",
                BuildSeedCoverUrl("il-castagno-tradate"),
                "Agriturismo poco fuori centro, più adatto a pranzi, cene e tavolate con prenotazione.",
                "agriturismo,pranzo,cena,prenotazione,eventi",
                45.7179000,
                8.8974000,
                7),
            new SeedVenueDefinition(
                LaBottegaVinoVenueId,
                "dev-tradate-018",
                "La Bottega Del Vino",
                "wine-bar",
                "Via Sopranzi 3",
                "Tradate",
                "IT",
                null,
                null,
                "Lun-Sab 09:00-12:30, 15:30-19:00",
                BuildSeedCoverUrl("la-bottega-del-vino-tradate"),
                "Enoteca compatta da centro storico, più da degustazione e bottiglia che da lunga permanenza.",
                "enoteca,vino,degustazione",
                45.7157000,
                8.9067000,
                3)
        };
    }

    private static Venue CreateVenue(SeedVenueDefinition seedVenue)
    {
        return new Venue
        {
            Id = seedVenue.Id,
            ExternalProviderId = seedVenue.ExternalProviderId,
            Name = seedVenue.Name,
            Category = seedVenue.Category,
            AddressLine = seedVenue.AddressLine,
            City = seedVenue.City,
            CountryCode = seedVenue.CountryCode,
            PhoneNumber = seedVenue.PhoneNumber,
            WebsiteUrl = seedVenue.WebsiteUrl,
            HoursSummary = seedVenue.HoursSummary,
            CoverImageUrl = seedVenue.CoverImageUrl,
            Description = seedVenue.Description,
            TagsCsv = seedVenue.TagsCsv,
            Location = CreatePoint(seedVenue.Longitude, seedVenue.Latitude)
        };
    }

    private static bool ApplySeedVenue(Venue venue, SeedVenueDefinition seedVenue)
    {
        var changed = false;

        if (!string.Equals(venue.ExternalProviderId, seedVenue.ExternalProviderId, StringComparison.Ordinal))
        {
            venue.ExternalProviderId = seedVenue.ExternalProviderId;
            changed = true;
        }

        if (!string.Equals(venue.Name, seedVenue.Name, StringComparison.Ordinal))
        {
            venue.Name = seedVenue.Name;
            changed = true;
        }

        if (!string.Equals(venue.Category, seedVenue.Category, StringComparison.Ordinal))
        {
            venue.Category = seedVenue.Category;
            changed = true;
        }

        if (!string.Equals(venue.AddressLine, seedVenue.AddressLine, StringComparison.Ordinal))
        {
            venue.AddressLine = seedVenue.AddressLine;
            changed = true;
        }

        if (!string.Equals(venue.City, seedVenue.City, StringComparison.Ordinal))
        {
            venue.City = seedVenue.City;
            changed = true;
        }

        if (!string.Equals(venue.CountryCode, seedVenue.CountryCode, StringComparison.Ordinal))
        {
            venue.CountryCode = seedVenue.CountryCode;
            changed = true;
        }

        if (!string.Equals(venue.PhoneNumber, seedVenue.PhoneNumber, StringComparison.Ordinal))
        {
            venue.PhoneNumber = seedVenue.PhoneNumber;
            changed = true;
        }

        if (!string.Equals(venue.WebsiteUrl, seedVenue.WebsiteUrl, StringComparison.Ordinal))
        {
            venue.WebsiteUrl = seedVenue.WebsiteUrl;
            changed = true;
        }

        if (!string.Equals(venue.HoursSummary, seedVenue.HoursSummary, StringComparison.Ordinal))
        {
            venue.HoursSummary = seedVenue.HoursSummary;
            changed = true;
        }

        if (!string.Equals(venue.CoverImageUrl, seedVenue.CoverImageUrl, StringComparison.Ordinal))
        {
            venue.CoverImageUrl = seedVenue.CoverImageUrl;
            changed = true;
        }

        if (!string.Equals(venue.Description, seedVenue.Description, StringComparison.Ordinal))
        {
            venue.Description = seedVenue.Description;
            changed = true;
        }

        if (!string.Equals(venue.TagsCsv, seedVenue.TagsCsv, StringComparison.Ordinal))
        {
            venue.TagsCsv = seedVenue.TagsCsv;
            changed = true;
        }

        var currentLatitude = venue.Location?.Y ?? 0d;
        var currentLongitude = venue.Location?.X ?? 0d;
        if (Math.Abs(currentLatitude - seedVenue.Latitude) > 0.000001d ||
            Math.Abs(currentLongitude - seedVenue.Longitude) > 0.000001d)
        {
            venue.Location = CreatePoint(seedVenue.Longitude, seedVenue.Latitude);
            changed = true;
        }

        if (changed)
        {
            venue.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        return changed;
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

    private static string BuildSeedCoverUrl(string slug)
    {
        var sourceUrl = slug switch
        {
            "folk-pub-tradate" => "https://www.folkpub.it/",
            "convivio-tradate" => "https://www.conviviotradate.it/",
            "fanari-tradate" => "https://www.paginegialle.it/tradate-va/ristoranti/fanari-pub_20328890",
            "mood-cafe-tradate" => "https://www.paginegialle.it/tradate-va/ristoranti/mood-cafe_20318699",
            "nonna-adelma-tradate" => "https://trattorianonnaadelma.eatbu.com/?lang=en",
            "inout-tradate" => "https://www.inouttradate.it/",
            "street-taste-tradate" => "https://www.streettradate.it/",
            "rarica-tradate" => "https://www.rarica.it/",
            "cucina-quarantadue-tradate" => "https://www.cylex-italia.it/tradate/la-cucina-del-quarantadue-12932190.html",
            "love-it-tradate" => "https://www.tripadvisor.com/Restaurant_Review-g1574957-d32882733-Reviews-Love_It-Tradate_Province_of_Varese_Lombardy.html",
            "murnee-tradate" => "https://maps.apple.com/place?place-id=I50E8CF1D90BE589",
            "larmonica-tradate" => "https://www.ristorantelarmonica.com/",
            "emma-caffe-tradate" => "https://www.emmacaffe.it/",
            "berbene-tradate" => "https://www.enotecaberbene.it/",
            "oro-rosso-tradate" => "http://www.ororossoristorante.it",
            "roadhouse-tradate" => "https://www.roadhouse.it/",
            "il-castagno-tradate" => "https://www.agriilcastagno.com/",
            "la-bottega-del-vino-tradate" => "https://restaurantguru.it/La-Bottega-Del-Vino-Tradate",
            _ => null
        };

        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            return $"https://picsum.photos/seed/{Uri.EscapeDataString($"friendmap-{slug}")}/960/640";
        }

        return $"https://s.wordpress.com/mshots/v1/{Uri.EscapeDataString(sourceUrl)}?w=1200";
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

    private sealed record SeedVenueDefinition(
        Guid Id,
        string ExternalProviderId,
        string Name,
        string Category,
        string AddressLine,
        string City,
        string CountryCode,
        string? PhoneNumber,
        string? WebsiteUrl,
        string? HoursSummary,
        string? CoverImageUrl,
        string? Description,
        string? TagsCsv,
        double Latitude,
        double Longitude,
        int SeedPeopleCount);
}
