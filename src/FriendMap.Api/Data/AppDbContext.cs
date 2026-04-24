using FriendMap.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FriendMap.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<UserInterest> UserInterests => Set<UserInterest>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<FriendRelation> FriendRelations => Set<FriendRelation>();
    public DbSet<VenueIntention> VenueIntentions => Set<VenueIntention>();
    public DbSet<VenueCheckIn> VenueCheckIns => Set<VenueCheckIn>();
    public DbSet<SocialTable> SocialTables => Set<SocialTable>();
    public DbSet<SocialTableParticipant> SocialTableParticipants => Set<SocialTableParticipant>();
    public DbSet<SocialTableMessage> SocialTableMessages => Set<SocialTableMessage>();
    public DbSet<DirectMessageThread> DirectMessageThreads => Set<DirectMessageThread>();
    public DbSet<DirectMessage> DirectMessages => Set<DirectMessage>();
    public DbSet<UserBlock> UserBlocks => Set<UserBlock>();
    public DbSet<ModerationReport> ModerationReports => Set<ModerationReport>();
    public DbSet<VenueAffluenceSnapshot> VenueAffluenceSnapshots => Set<VenueAffluenceSnapshot>();
    public DbSet<NotificationDeviceToken> NotificationDeviceTokens => Set<NotificationDeviceToken>();
    public DbSet<NotificationOutboxItem> NotificationOutboxItems => Set<NotificationOutboxItem>();
    public DbSet<UserStory> UserStories => Set<UserStory>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>().ToTable("app_users");
        modelBuilder.Entity<UserInterest>().ToTable("user_interests");
        modelBuilder.Entity<Venue>().ToTable("venues");
        modelBuilder.Entity<FriendRelation>().ToTable("friend_relations");
        modelBuilder.Entity<VenueIntention>().ToTable("venue_intentions");
        modelBuilder.Entity<VenueCheckIn>().ToTable("venue_checkins");
        modelBuilder.Entity<SocialTable>().ToTable("social_tables");
        modelBuilder.Entity<SocialTableParticipant>().ToTable("social_table_participants");
        modelBuilder.Entity<SocialTableMessage>().ToTable("social_table_messages");
        modelBuilder.Entity<DirectMessageThread>().ToTable("direct_message_threads");
        modelBuilder.Entity<DirectMessage>().ToTable("direct_messages");
        modelBuilder.Entity<UserBlock>().ToTable("user_blocks");
        modelBuilder.Entity<ModerationReport>().ToTable("moderation_reports");
        modelBuilder.Entity<VenueAffluenceSnapshot>().ToTable("venue_affluence_snapshots");
        modelBuilder.Entity<NotificationDeviceToken>().ToTable("notification_device_tokens");
        modelBuilder.Entity<NotificationOutboxItem>().ToTable("notification_outbox");
        modelBuilder.Entity<UserStory>().ToTable("user_stories");
        modelBuilder.Entity<UserAchievement>().ToTable("user_achievements");

        modelBuilder.Entity<UserStory>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserStory>()
            .HasIndex(x => new { x.UserId, x.ExpiresAtUtc });

        modelBuilder.Entity<UserAchievement>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserAchievement>()
            .HasIndex(x => new { x.UserId, x.BadgeCode })
            .IsUnique();

        modelBuilder.Entity<Venue>()
            .Property(x => x.Location)
            .HasColumnType("geography (point, 4326)");

        modelBuilder.Entity<Venue>()
            .HasIndex(x => x.Location)
            .HasMethod("GIST");

        modelBuilder.Entity<FriendRelation>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.RequesterId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FriendRelation>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.AddresseeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FriendRelation>()
            .HasIndex(x => new { x.RequesterId, x.AddresseeId })
            .IsUnique();

        modelBuilder.Entity<UserInterest>()
            .HasIndex(x => new { x.UserId, x.Tag })
            .IsUnique();

        modelBuilder.Entity<VenueIntention>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<VenueIntention>()
            .HasOne<Venue>()
            .WithMany()
            .HasForeignKey(x => x.VenueId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<VenueCheckIn>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<VenueCheckIn>()
            .HasOne<Venue>()
            .WithMany()
            .HasForeignKey(x => x.VenueId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SocialTable>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.HostUserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SocialTable>()
            .HasOne<Venue>()
            .WithMany()
            .HasForeignKey(x => x.VenueId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SocialTableParticipant>()
            .HasOne<SocialTable>()
            .WithMany()
            .HasForeignKey(x => x.SocialTableId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SocialTableParticipant>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SocialTableParticipant>()
            .HasIndex(x => new { x.SocialTableId, x.UserId })
            .IsUnique();

        modelBuilder.Entity<SocialTableMessage>()
            .HasOne<SocialTable>()
            .WithMany()
            .HasForeignKey(x => x.SocialTableId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SocialTableMessage>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SocialTableMessage>()
            .HasIndex(x => new { x.SocialTableId, x.CreatedAtUtc });

        modelBuilder.Entity<DirectMessageThread>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UserLowId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DirectMessageThread>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UserHighId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DirectMessageThread>()
            .HasIndex(x => new { x.UserLowId, x.UserHighId })
            .IsUnique();

        modelBuilder.Entity<DirectMessageThread>()
            .HasIndex(x => x.LastMessageAtUtc);

        modelBuilder.Entity<DirectMessage>()
            .HasOne<DirectMessageThread>()
            .WithMany()
            .HasForeignKey(x => x.ThreadId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DirectMessage>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.SenderUserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DirectMessage>()
            .HasIndex(x => new { x.ThreadId, x.CreatedAtUtc });

        modelBuilder.Entity<UserBlock>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.BlockerUserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserBlock>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.BlockedUserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserBlock>()
            .HasIndex(x => new { x.BlockerUserId, x.BlockedUserId })
            .IsUnique();

        modelBuilder.Entity<ModerationReport>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.ReporterUserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ModerationReport>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.ReportedUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ModerationReport>()
            .HasOne<Venue>()
            .WithMany()
            .HasForeignKey(x => x.ReportedVenueId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ModerationReport>()
            .HasOne<SocialTable>()
            .WithMany()
            .HasForeignKey(x => x.ReportedSocialTableId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<VenueAffluenceSnapshot>()
            .HasOne<Venue>()
            .WithMany()
            .HasForeignKey(x => x.VenueId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<NotificationDeviceToken>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<NotificationDeviceToken>()
            .HasIndex(x => new { x.UserId, x.Platform, x.DeviceToken })
            .IsUnique();

        modelBuilder.Entity<NotificationOutboxItem>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<NotificationOutboxItem>()
            .HasIndex(x => new { x.Status, x.NextAttemptAtUtc });

        modelBuilder.Entity<Venue>()
            .HasIndex(x => x.ExternalProviderId);

        modelBuilder.Entity<VenueCheckIn>()
            .HasIndex(x => new { x.VenueId, x.ExpiresAtUtc });

        modelBuilder.Entity<VenueIntention>()
            .HasIndex(x => new { x.VenueId, x.StartsAtUtc, x.EndsAtUtc });

        modelBuilder.Entity<VenueAffluenceSnapshot>()
            .HasIndex(x => new { x.VenueId, x.BucketStartUtc })
            .IsUnique();
    }
}
