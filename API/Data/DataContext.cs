using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using API.DTOs.Progress;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Enums.User;
using API.Entities.Enums.UserPreferences;
using API.Entities.History;
using API.Entities.Interfaces;
using API.Entities.Metadata;
using API.Entities.MetadataMatching;
using API.Entities.Person;
using API.Entities.Progress;
using API.Entities.Scrobble;
using API.Entities.User;
using API.Extensions;
using Hangfire.Storage.SQLite.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

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
    public DbSet<SeriesMetadataTag> SeriesMetadataTag { get; set; } = null;
    public DbSet<GenreSeriesMetadata> GenreSeriesMetadata { get; set; } = null;
    [Obsolete("Use AppUserCollection")]
    public DbSet<CollectionTag> CollectionTag { get; set; } = null!;
    public DbSet<AppUserBookmark> AppUserBookmark { get; set; } = null!;
    public DbSet<ReadingList> ReadingList { get; set; } = null!;
    public DbSet<ReadingListItem> ReadingListItem { get; set; } = null!;
    public DbSet<Person> Person { get; set; } = null!;
    public DbSet<PersonAlias> PersonAlias { get; set; } = null!;
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
    [Obsolete("Use IsBlacklisted field on Series")]
    public DbSet<SeriesBlacklist> SeriesBlacklist { get; set; } = null!;
    public DbSet<AppUserCollection> AppUserCollection { get; set; } = null!;
    public DbSet<ChapterPeople> ChapterPeople { get; set; } = null!;
    public DbSet<SeriesMetadataPeople> SeriesMetadataPeople { get; set; } = null!;
    public DbSet<EmailHistory> EmailHistory { get; set; } = null!;
    public DbSet<MetadataSettings> MetadataSettings { get; set; } = null!;
    public DbSet<MetadataFieldMapping> MetadataFieldMapping { get; set; } = null!;
    public DbSet<AppUserChapterRating> AppUserChapterRating { get; set; } = null!;
    public DbSet<AppUserReadingProfile> AppUserReadingProfiles { get; set; } = null!;
    public DbSet<AppUserAnnotation> AppUserAnnotation { get; set; } = null!;
    public DbSet<EpubFont> EpubFont { get; set; } = null!;
    public DbSet<AppUserReadingSession> AppUserReadingSession { get; set; } = null!;
    public DbSet<AppUserReadingSessionActivityData> AppUserReadingSessionActivityData { get; set; } = null!;
    public DbSet<AppUserReadingHistory> AppUserReadingHistory { get; set; } = null!;
    public DbSet<ClientDevice> ClientDevice { get; set; } = null!;
    public DbSet<ClientDeviceHistory> ClientDeviceHistory { get; set; } = null!;
    public DbSet<AppUserAuthKey> AppUserAuthKey { get; set; } = null!;


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
        builder.Entity<AppUserPreferences>()
            .Property(b => b.AllowAutomaticWebtoonReaderDetection)
            .HasDefaultValue(true);
        builder.Entity<AppUserPreferences>()
            .Property(b => b.ColorScapeEnabled)
            .HasDefaultValue(true);
        builder.Entity<AppUserPreferences>()
            .Property(b => b.PromptForRereadsAfter)
            .HasDefaultValue(30);

        builder.Entity<Library>()
            .Property(b => b.AllowScrobbling)
            .HasDefaultValue(true);
        builder.Entity<Library>()
            .Property(b => b.AllowMetadataMatching)
            .HasDefaultValue(true);
        builder.Entity<Library>()
            .Property(b => b.EnableMetadata)
            .HasDefaultValue(true);
        builder.Entity<Library>()
            .Property(l => l.DefaultLanguage)
            .HasDefaultValue(string.Empty);

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
            .HasJsonConversion([]);

        // Ensure blacklist is stored as a JSON array
        builder.Entity<MetadataSettings>()
            .Property(x => x.Blacklist)
            .HasJsonConversion([]);
        builder.Entity<MetadataSettings>()
            .Property(x => x.Whitelist)
            .HasJsonConversion([]);
        builder.Entity<MetadataSettings>()
            .Property(x => x.Overrides)
            .HasJsonConversion([]);

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

        builder.Entity<AppUserReadingProfile>()
            .Property(b => b.BookThemeName)
            .HasDefaultValue("Dark");
        builder.Entity<AppUserReadingProfile>()
            .Property(b => b.BackgroundColor)
            .HasDefaultValue("#000000");
        builder.Entity<AppUserReadingProfile>()
            .Property(b => b.BookReaderWritingStyle)
            .HasDefaultValue(WritingStyle.Horizontal);
        builder.Entity<AppUserReadingProfile>()
            .Property(b => b.AllowAutomaticWebtoonReaderDetection)
            .HasDefaultValue(true);

        builder.Entity<AppUserReadingProfile>()
            .Property(rp => rp.LibraryIds)
            .HasJsonConversion([])
            .HasColumnType("TEXT");
        builder.Entity<AppUserReadingProfile>()
            .Property(rp => rp.SeriesIds)
            .HasJsonConversion([])
            .HasColumnType("TEXT");

        builder.Entity<SeriesMetadata>()
            .Property(sm => sm.KPlusOverrides)
            .HasJsonConversion([])
            .HasColumnType("TEXT")
            .HasDefaultValue(new List<MetadataSettingField>());
        builder.Entity<Chapter>()
            .Property(sm => sm.KPlusOverrides)
            .HasJsonConversion([])
            .HasColumnType("TEXT")
            .HasDefaultValue(new List<MetadataSettingField>());

        builder.Entity<AppUserPreferences>()
            .Property(a => a.BookReaderHighlightSlots)
            .HasJsonConversion([])
            .HasColumnType("TEXT")
            .HasDefaultValue(new List<HighlightSlot>());

        builder.Entity<AppUserPreferences>()
            .Property(p => p.CustomKeyBinds)
            .HasJsonConversion([])
            .HasColumnType("TEXT")
            .HasDefaultValue(new Dictionary<KeyBindTarget, IList<KeyBind>>());

        builder.Entity<AppUser>()
            .Property(user => user.IdentityProvider)
            .HasDefaultValue(IdentityProvider.Kavita);

        builder.Entity<AppUserPreferences>()
            .Property(a => a.SocialPreferences)
            .HasJsonConversion(new AppUserSocialPreferences())
            .HasColumnType("TEXT")
            .HasDefaultValue(new AppUserSocialPreferences());

        builder.Entity<AppUserPreferences>()
            .Property(a => a.OpdsPreferences)
            .HasJsonConversion(new AppUserOpdsPreferences())
            .HasColumnType("TEXT")
            .HasDefaultValue(new AppUserOpdsPreferences());

        builder.Entity<AppUserAnnotation>()
            .PrimitiveCollection(a => a.Likes)
            .HasDefaultValue(new List<int>());

        builder.Entity<AppUserReadingSession>()
            .Property(b => b.IsActive)
            .HasDefaultValue(true);

        builder.Entity<AppUserReadingSession>()
            .HasMany(x => x.ActivityData)
            .WithOne(a => a.ReadingSession)
            .HasForeignKey(a => a.AppUserReadingSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<AppUserReadingSessionActivityData>(e
            => e.ComplexProperty(d=> d.ClientInfo, b => b.ToJson()));

        builder.Entity<AppUserReadingHistory>()
            .Property(sm => sm.Data)
            .HasJsonConversion(new DailyReadingDataDto())
            .HasColumnType("TEXT")
            .HasDefaultValue(new DailyReadingDataDto());
        builder.Entity<AppUserReadingHistory>()
            .Property(sm => sm.ClientInfoUsed)
            .HasJsonConversion([])
            .HasColumnType("TEXT")
            .HasDefaultValue(new List<ClientInfoData>());

        builder.Entity<ClientDevice>()
            .Property(sm => sm.CurrentClientInfo)
            .HasJsonConversion(new ClientInfoData())
            .HasColumnType("TEXT")
            .HasDefaultValue(new ClientInfoData());

        builder.Entity<ClientDeviceHistory>()
            .Property(sm => sm.ClientInfo)
            .HasJsonConversion(new ClientInfoData())
            .HasColumnType("TEXT")
            .HasDefaultValue(new ClientInfoData());

        builder.Entity<SeriesMetadata>()
            .HasMany(sm => sm.Tags)
            .WithMany(t => t.SeriesMetadatas)
            .UsingEntity<SeriesMetadataTag>();

        builder.Entity<SeriesMetadata>()
            .HasMany(sm => sm.Genres)
            .WithMany(t => t.SeriesMetadatas)
            .UsingEntity<GenreSeriesMetadata>();

        builder.Entity<AppUserAuthKey>()
            .Property(a => a.Provider)
            .HasDefaultValue(AuthKeyProvider.User);
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
