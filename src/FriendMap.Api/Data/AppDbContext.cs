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
    public DbSet<GroupChat> GroupChats => Set<GroupChat>();
    public DbSet<GroupChatMember> GroupChatMembers => Set<GroupChatMember>();
    public DbSet<GroupChatMessage> GroupChatMessages => Set<GroupChatMessage>();
    public DbSet<UserBlock> UserBlocks => Set<UserBlock>();
    public DbSet<ModerationReport> ModerationReports => Set<ModerationReport>();
    public DbSet<VenueAffluenceSnapshot> VenueAffluenceSnapshots => Set<VenueAffluenceSnapshot>();
    public DbSet<NotificationDeviceToken> NotificationDeviceTokens => Set<NotificationDeviceToken>();
    public DbSet<NotificationOutboxItem> NotificationOutboxItems => Set<NotificationOutboxItem>();
    public DbSet<UserStory> UserStories => Set<UserStory>();
    public DbSet<UserStoryReaction> UserStoryReactions => Set<UserStoryReaction>();
    public DbSet<UserStoryComment> UserStoryComments => Set<UserStoryComment>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();
    public DbSet<FlareSignal> FlareSignals => Set<FlareSignal>();
    public DbSet<FlareResponse> FlareResponses => Set<FlareResponse>();
    public DbSet<DeepLinkToken> DeepLinkTokens => Set<DeepLinkToken>();
    public DbSet<FeedCardFatigue> FeedCardFatigues => Set<FeedCardFatigue>();
    public DbSet<FlareRelayAudit> FlareRelayAudits => Set<FlareRelayAudit>();
    public DbSet<FeedReentryNotificationState> FeedReentryNotificationStates => Set<FeedReentryNotificationState>();

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
        modelBuilder.Entity<GroupChat>().ToTable("group_chats");
        modelBuilder.Entity<GroupChatMember>().ToTable("group_chat_members");
        modelBuilder.Entity<GroupChatMessage>().ToTable("group_chat_messages");
        modelBuilder.Entity<UserBlock>().ToTable("user_blocks");
        modelBuilder.Entity<ModerationReport>().ToTable("moderation_reports");
        modelBuilder.Entity<VenueAffluenceSnapshot>().ToTable("venue_affluence_snapshots");
        modelBuilder.Entity<NotificationDeviceToken>().ToTable("notification_device_tokens");
        modelBuilder.Entity<NotificationOutboxItem>().ToTable("notification_outbox");
        modelBuilder.Entity<UserStory>().ToTable("user_stories");
        modelBuilder.Entity<UserStoryReaction>().ToTable("user_story_reactions");
        modelBuilder.Entity<UserStoryComment>().ToTable("user_story_comments");
        modelBuilder.Entity<UserAchievement>().ToTable("user_achievements");
        modelBuilder.Entity<FlareSignal>().ToTable("flare_signals");
        modelBuilder.Entity<FlareResponse>().ToTable("flare_responses");
        modelBuilder.Entity<DeepLinkToken>().ToTable("deep_link_tokens");
        modelBuilder.Entity<FeedCardFatigue>().ToTable("feed_card_fatigues");
        modelBuilder.Entity<FlareRelayAudit>().ToTable("flare_relay_audits");
        modelBuilder.Entity<FeedReentryNotificationState>().ToTable("feed_reentry_notification_states");

        modelBuilder.Entity<UserStory>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserStory>()
            .HasOne<Venue>()
            .WithMany()
            .HasForeignKey(x => x.VenueId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<UserStory>()
            .HasIndex(x => new { x.UserId, x.ExpiresAtUtc });

        modelBuilder.Entity<UserStory>()
            .HasIndex(x => new { x.VenueId, x.ExpiresAtUtc });

        modelBuilder.Entity<UserStoryReaction>()
            .HasOne<UserStory>()
            .WithMany()
            .HasForeignKey(x => x.UserStoryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserStoryReaction>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserStoryReaction>()
            .HasIndex(x => new { x.UserStoryId, x.UserId, x.ReactionType })
            .IsUnique();

        modelBuilder.Entity<UserStoryComment>()
            .HasOne<UserStory>()
            .WithMany()
            .HasForeignKey(x => x.UserStoryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserStoryComment>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserStoryComment>()
            .HasIndex(x => new { x.UserStoryId, x.CreatedAtUtc });

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

        modelBuilder.Entity<GroupChat>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GroupChat>()
            .HasOne<Venue>()
            .WithMany()
            .HasForeignKey(x => x.VenueId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GroupChat>()
            .HasIndex(x => new { x.Kind, x.VenueId });

        modelBuilder.Entity<GroupChat>()
            .HasIndex(x => x.LastMessageAtUtc);

        modelBuilder.Entity<GroupChatMember>()
            .HasOne<GroupChat>()
            .WithMany()
            .HasForeignKey(x => x.GroupChatId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GroupChatMember>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GroupChatMember>()
            .HasIndex(x => new { x.GroupChatId, x.UserId })
            .IsUnique();

        modelBuilder.Entity<GroupChatMessage>()
            .HasOne<GroupChat>()
            .WithMany()
            .HasForeignKey(x => x.GroupChatId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GroupChatMessage>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GroupChatMessage>()
            .HasIndex(x => new { x.GroupChatId, x.CreatedAtUtc });

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

        modelBuilder.Entity<FlareSignal>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FlareSignal>()
            .HasIndex(x => x.ExpiresAtUtc);

        modelBuilder.Entity<FlareResponse>()
            .HasOne<FlareSignal>()
            .WithMany()
            .HasForeignKey(x => x.FlareSignalId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FlareResponse>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FlareResponse>()
            .HasIndex(x => new { x.FlareSignalId, x.UserId });

        modelBuilder.Entity<DeepLinkToken>()
            .HasIndex(x => x.Token)
            .IsUnique();

        modelBuilder.Entity<DeepLinkToken>()
            .HasIndex(x => new { x.LinkType, x.TargetId, x.ExpiresAtUtc });

        modelBuilder.Entity<FeedCardFatigue>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FeedCardFatigue>()
            .HasIndex(x => new { x.UserId, x.CardKey })
            .IsUnique();

        modelBuilder.Entity<FlareRelayAudit>()
            .HasOne<FlareSignal>()
            .WithMany()
            .HasForeignKey(x => x.FlareSignalId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FlareRelayAudit>()
            .HasIndex(x => new { x.SenderUserId, x.CreatedAtUtc });

        modelBuilder.Entity<FlareRelayAudit>()
            .HasIndex(x => new { x.FlareSignalId, x.SenderUserId, x.TargetUserId });

        modelBuilder.Entity<FeedReentryNotificationState>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FeedReentryNotificationState>()
            .HasIndex(x => new { x.UserId, x.TriggerType, x.TriggerKey })
            .IsUnique();

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
