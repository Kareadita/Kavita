using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using API.DTOs.KavitaPlus.Metadata;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Enums.UserPreferences;
using API.Entities.History;
using API.Entities.Interfaces;
using API.Entities.Metadata;
using API.Entities.Scrobble;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace API.Data;

public sealed class DataContext : IdentityDbContext<AppUser, AppRole, int,
    IdentityUserClaim<int>, AppUserRole, IdentityUserLogin<int>,
    IdentityRoleClaim<int>, IdentityUserToken<int>>
{
    public DataContext(DbContextOptions options) : base(options)
    {
        ChangeTracker.Tracked += OnEntityTracked;
        ChangeTracker.StateChanged += OnEntityStateChanged;
    }

    public DbSet<Library> Library { get; set; } = null!;
    public DbSet<Series> Series { get; set; } = null!;
    public DbSet<Chapter> Chapter { get; set; } = null!;
    public DbSet<Volume> Volume { get; set; } = null!;
    public DbSet<AppUser> AppUser { get; set; } = null!;
    public DbSet<MangaFile> MangaFile { get; set; } = null!;
    public DbSet<AppUserProgress> AppUserProgresses { get; set; } = null!;
    public DbSet<AppUserRating> AppUserRating { get; set; } = null!;
    public DbSet<ServerSetting> ServerSetting { get; set; } = null!;
    public DbSet<AppUserPreferences> AppUserPreferences { get; set; } = null!;
    public DbSet<SeriesMetadata> SeriesMetadata { get; set; } = null!;
    [Obsolete]
    public DbSet<CollectionTag> CollectionTag { get; set; } = null!;
    public DbSet<AppUserBookmark> AppUserBookmark { get; set; } = null!;
    public DbSet<ReadingList> ReadingList { get; set; } = null!;
    public DbSet<ReadingListItem> ReadingListItem { get; set; } = null!;
    public DbSet<Person> Person { get; set; } = null!;
    public DbSet<Genre> Genre { get; set; } = null!;
    public DbSet<Tag> Tag { get; set; } = null!;
    public DbSet<SiteTheme> SiteTheme { get; set; } = null!;
    public DbSet<SeriesRelation> SeriesRelation { get; set; } = null!;
    public DbSet<FolderPath> FolderPath { get; set; } = null!;
    public DbSet<Device> Device { get; set; } = null!;
    public DbSet<ServerStatistics> ServerStatistics { get; set; } = null!;
    public DbSet<MediaError> MediaError { get; set; } = null!;
    public DbSet<ScrobbleEvent> ScrobbleEvent { get; set; } = null!;
    public DbSet<ScrobbleError> ScrobbleError { get; set; } = null!;
    public DbSet<ScrobbleHold> ScrobbleHold { get; set; } = null!;
    public DbSet<AppUserOnDeckRemoval> AppUserOnDeckRemoval { get; set; } = null!;
    public DbSet<AppUserTableOfContent> AppUserTableOfContent { get; set; } = null!;
    public DbSet<AppUserSmartFilter> AppUserSmartFilter { get; set; } = null!;
    public DbSet<AppUserDashboardStream> AppUserDashboardStream { get; set; } = null!;
    public DbSet<AppUserSideNavStream> AppUserSideNavStream { get; set; } = null!;
    public DbSet<AppUserExternalSource> AppUserExternalSource { get; set; } = null!;
    public DbSet<ExternalReview> ExternalReview { get; set; } = null!;
    public DbSet<ExternalRating> ExternalRating { get; set; } = null!;
    public DbSet<ExternalSeriesMetadata> ExternalSeriesMetadata { get; set; } = null!;
    public DbSet<ExternalRecommendation> ExternalRecommendation { get; set; } = null!;
    public DbSet<ManualMigrationHistory> ManualMigrationHistory { get; set; } = null!;
    public DbSet<SeriesBlacklist> SeriesBlacklist { get; set; } = null!;
    public DbSet<AppUserCollection> AppUserCollection { get; set; } = null!;
    public DbSet<ChapterPeople> ChapterPeople { get; set; } = null!;
    public DbSet<SeriesMetadataPeople> SeriesMetadataPeople { get; set; } = null!;
    public DbSet<EmailHistory> EmailHistory { get; set; } = null!;
    public DbSet<MetadataSettings> MetadataSettings { get; set; } = null!;
    public DbSet<MetadataFieldMapping> MetadataFieldMapping { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);


        builder.Entity<AppUser>()
            .HasMany(ur => ur.UserRoles)
            .WithOne(u => u.User)
            .HasForeignKey(ur => ur.UserId)
            .IsRequired();

        builder.Entity<AppRole>()
            .HasMany(ur => ur.UserRoles)
            .WithOne(u => u.Role)
            .HasForeignKey(ur => ur.RoleId)
            .IsRequired();

        builder.Entity<SeriesRelation>()
            .HasOne(pt => pt.Series)
            .WithMany(p => p.Relations)
            .HasForeignKey(pt => pt.SeriesId)
            .OnDelete(DeleteBehavior.Cascade);


        builder.Entity<SeriesRelation>()
            .HasOne(pt => pt.TargetSeries)
            .WithMany(t => t.RelationOf)
            .HasForeignKey(pt => pt.TargetSeriesId)
            .OnDelete(DeleteBehavior.Cascade);



        builder.Entity<AppUserPreferences>()
            .Property(b => b.BookThemeName)
            .HasDefaultValue("Dark");
        builder.Entity<AppUserPreferences>()
            .Property(b => b.BackgroundColor)
            .HasDefaultValue("#000000");
        builder.Entity<AppUserPreferences>()
            .Property(b => b.GlobalPageLayoutMode)
            .HasDefaultValue(PageLayoutMode.Cards);
        builder.Entity<AppUserPreferences>()
            .Property(b => b.BookReaderWritingStyle)
            .HasDefaultValue(WritingStyle.Horizontal);
        builder.Entity<AppUserPreferences>()
            .Property(b => b.Locale)
            .IsRequired(true)
            .HasDefaultValue("en");
        builder.Entity<AppUserPreferences>()
            .Property(b => b.AniListScrobblingEnabled)
            .HasDefaultValue(true);
        builder.Entity<AppUserPreferences>()
            .Property(b => b.WantToReadSync)
            .HasDefaultValue(true);

        builder.Entity<Library>()
            .Property(b => b.AllowScrobbling)
            .HasDefaultValue(true);
        builder.Entity<Library>()
            .Property(b => b.AllowMetadataMatching)
            .HasDefaultValue(true);

        builder.Entity<Chapter>()
            .Property(b => b.WebLinks)
            .HasDefaultValue(string.Empty);
        builder.Entity<SeriesMetadata>()
            .Property(b => b.WebLinks)
            .HasDefaultValue(string.Empty);

        builder.Entity<Chapter>()
            .Property(b => b.ISBN)
            .HasDefaultValue(string.Empty);

        builder.Entity<AppUserDashboardStream>()
            .Property(b => b.StreamType)
            .HasDefaultValue(DashboardStreamType.SmartFilter);
        builder.Entity<AppUserDashboardStream>()
            .HasIndex(e => e.Visible)
            .IsUnique(false);

        builder.Entity<AppUserSideNavStream>()
            .Property(b => b.StreamType)
            .HasDefaultValue(SideNavStreamType.SmartFilter);
        builder.Entity<AppUserSideNavStream>()
            .HasIndex(e => e.Visible)
            .IsUnique(false);

        builder.Entity<ExternalSeriesMetadata>()
            .HasOne(em => em.Series)
            .WithOne(s => s.ExternalSeriesMetadata)
            .HasForeignKey<ExternalSeriesMetadata>(em => em.SeriesId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<AppUserCollection>()
            .Property(b => b.AgeRating)
            .HasDefaultValue(AgeRating.Unknown);

        // Configure the many-to-many relationship for Movie and Person
        builder.Entity<ChapterPeople>()
            .HasKey(cp => new { cp.ChapterId, cp.PersonId, cp.Role });

        builder.Entity<ChapterPeople>()
            .HasOne(cp => cp.Chapter)
            .WithMany(c => c.People)
            .HasForeignKey(cp => cp.ChapterId);

        builder.Entity<ChapterPeople>()
            .HasOne(cp => cp.Person)
            .WithMany(p => p.ChapterPeople)
            .HasForeignKey(cp => cp.PersonId)
            .OnDelete(DeleteBehavior.Cascade);


        builder.Entity<SeriesMetadataPeople>()
            .HasKey(smp => new { smp.SeriesMetadataId, smp.PersonId, smp.Role });

        builder.Entity<SeriesMetadataPeople>()
            .HasOne(smp => smp.SeriesMetadata)
            .WithMany(sm => sm.People)
            .HasForeignKey(smp => smp.SeriesMetadataId);

        builder.Entity<SeriesMetadataPeople>()
            .HasOne(smp => smp.Person)
            .WithMany(p => p.SeriesMetadataPeople)
            .HasForeignKey(smp => smp.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SeriesMetadataPeople>()
            .Property(b => b.OrderWeight)
            .HasDefaultValue(0);

        builder.Entity<MetadataSettings>()
            .Property(x => x.AgeRatingMappings)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<Dictionary<string, AgeRating>>(v, JsonSerializerOptions.Default) ?? new Dictionary<string, AgeRating>()
            );

        // Ensure blacklist is stored as a JSON array
        builder.Entity<MetadataSettings>()
            .Property(x => x.Blacklist)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>()
            );
        builder.Entity<MetadataSettings>()
            .Property(x => x.Whitelist)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>()
            );
        builder.Entity<MetadataSettings>()
            .Property(x => x.Overrides)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<MetadataSettingField>>(v, JsonSerializerOptions.Default) ?? new List<MetadataSettingField>()
            );

        // Configure one-to-many relationship
        builder.Entity<MetadataSettings>()
            .HasMany(x => x.FieldMappings)
            .WithOne(x => x.MetadataSettings)
            .HasForeignKey(x => x.MetadataSettingsId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<MetadataSettings>()
            .Property(b => b.Enabled)
            .HasDefaultValue(true);
        builder.Entity<MetadataSettings>()
            .Property(b => b.EnableCoverImage)
            .HasDefaultValue(true);
    }

    #nullable enable
    private static void OnEntityTracked(object? sender, EntityTrackedEventArgs e)
    {
        if (e.FromQuery || e.Entry.State != EntityState.Added || e.Entry.Entity is not IEntityDate entity) return;

        entity.LastModified = DateTime.Now;
        entity.LastModifiedUtc = DateTime.UtcNow;

        // This allows for mocking
        if (entity.Created == DateTime.MinValue)
        {
            entity.Created = DateTime.Now;
            entity.CreatedUtc = DateTime.UtcNow;
        }
    }

    private static void OnEntityStateChanged(object? sender, EntityStateChangedEventArgs e)
    {
        if (e.NewState != EntityState.Modified || e.Entry.Entity is not IEntityDate entity) return;
        entity.LastModified = DateTime.Now;
        entity.LastModifiedUtc = DateTime.UtcNow;
    }
    #nullable disable

    private void OnSaveChanges()
    {
        foreach (var saveEntity in ChangeTracker.Entries()
                     .Where(e => e.State == EntityState.Modified)
                     .Select(entry => entry.Entity)
                     .OfType<IHasConcurrencyToken>())
        {
            saveEntity.OnSavingChanges();
        }
    }

    #region SaveChanges overrides

    public override int SaveChanges()
    {
        OnSaveChanges();

        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        OnSaveChanges();

        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default(CancellationToken))
    {
        OnSaveChanges();

        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken))
    {
        OnSaveChanges();

        return base.SaveChangesAsync(cancellationToken);
    }

    #endregion
}
