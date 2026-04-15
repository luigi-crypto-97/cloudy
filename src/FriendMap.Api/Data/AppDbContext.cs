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
    public DbSet<ModerationReport> ModerationReports => Set<ModerationReport>();
    public DbSet<VenueAffluenceSnapshot> VenueAffluenceSnapshots => Set<VenueAffluenceSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FriendRelation>()
            .HasIndex(x => new { x.RequesterId, x.AddresseeId })
            .IsUnique();

        modelBuilder.Entity<UserInterest>()
            .HasIndex(x => new { x.UserId, x.Tag })
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
