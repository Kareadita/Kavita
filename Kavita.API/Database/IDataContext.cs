using System;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.Entities;
using Kavita.Models.Entities.History;
using Kavita.Models.Entities.Metadata;
using Kavita.Models.Entities.MetadataMatching;
using Kavita.Models.Entities.Person;
using Kavita.Models.Entities.Progress;
using Kavita.Models.Entities.ReadingLists;
using Kavita.Models.Entities.Scrobble;
using Kavita.Models.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Kavita.API.Database;


public interface IDataContext : IDisposable
{

    DatabaseFacade Database { get; }

    DbSet<AppUser> Users { get; }
    DbSet<Library> Library { get; }
    DbSet<Series> Series { get; }
    DbSet<Chapter> Chapter { get; }
    DbSet<Volume> Volume { get; }
    DbSet<AppUser> AppUser { get; }
    DbSet<MangaFile> MangaFile { get; }
    DbSet<AppUserProgress> AppUserProgresses { get; }
    DbSet<AppUserRating> AppUserRating { get; }
    DbSet<ServerSetting> ServerSetting { get; }
    DbSet<AppUserPreferences> AppUserPreferences { get; }
    DbSet<SeriesMetadata> SeriesMetadata { get; }
    DbSet<SeriesMetadataTag> SeriesMetadataTag { get; }
    DbSet<GenreSeriesMetadata> GenreSeriesMetadata { get; }

    [Obsolete("Use AppUserCollection")]
    DbSet<CollectionTag> CollectionTag { get; }

    DbSet<AppUserBookmark> AppUserBookmark { get; }
    DbSet<ReadingList> ReadingList { get; }
    DbSet<ReadingListItem> ReadingListItem { get; }
    DbSet<Person> Person { get; }
    DbSet<PersonAlias> PersonAlias { get; }
    DbSet<Genre> Genre { get; }
    DbSet<Tag> Tag { get; }
    DbSet<SiteTheme> SiteTheme { get; }
    DbSet<SeriesRelation> SeriesRelation { get; }
    DbSet<FolderPath> FolderPath { get; }
    DbSet<Device> Device { get; }
    DbSet<ServerStatistics> ServerStatistics { get; }
    DbSet<MediaError> MediaError { get; }
    DbSet<ScrobbleEvent> ScrobbleEvent { get; }
    DbSet<ScrobbleError> ScrobbleError { get; }
    DbSet<ScrobbleHold> ScrobbleHold { get; }
    DbSet<AppUserOnDeckRemoval> AppUserOnDeckRemoval { get; }
    DbSet<AppUserTableOfContent> AppUserTableOfContent { get; }
    DbSet<AppUserSmartFilter> AppUserSmartFilter { get; }
    DbSet<AppUserDashboardStream> AppUserDashboardStream { get; }
    DbSet<AppUserSideNavStream> AppUserSideNavStream { get; }
    DbSet<AppUserExternalSource> AppUserExternalSource { get; }
    DbSet<ExternalReview> ExternalReview { get; }
    DbSet<ExternalRating> ExternalRating { get; }
    DbSet<ExternalSeriesMetadata> ExternalSeriesMetadata { get; }
    DbSet<ExternalRecommendation> ExternalRecommendation { get; }
    DbSet<ManualMigrationHistory> ManualMigrationHistory { get; }

    [Obsolete("Use IsBlacklisted field on Series")]
    DbSet<SeriesBlacklist> SeriesBlacklist { get; }

    DbSet<AppUserCollection> AppUserCollection { get; }
    DbSet<ChapterPeople> ChapterPeople { get; }
    DbSet<SeriesMetadataPeople> SeriesMetadataPeople { get; }
    DbSet<EmailHistory> EmailHistory { get; }
    DbSet<MetadataSettings> MetadataSettings { get; }
    DbSet<MetadataFieldMapping> MetadataFieldMapping { get; }
    DbSet<AppUserChapterRating> AppUserChapterRating { get; }
    DbSet<AppUserReadingProfile> AppUserReadingProfiles { get; }
    DbSet<AppUserAnnotation> AppUserAnnotation { get; }
    DbSet<EpubFont> EpubFont { get; }
    DbSet<AppUserReadingSession> AppUserReadingSession { get; }
    DbSet<AppUserReadingSessionActivityData> AppUserReadingSessionActivityData { get; }
    DbSet<AppUserReadingHistory> AppUserReadingHistory { get; }
    DbSet<ClientDevice> ClientDevice { get; }
    DbSet<ClientDeviceHistory> ClientDeviceHistory { get; }
    DbSet<AppUserAuthKey> AppUserAuthKey { get; }
    DbSet<ReadingListTag> ReadingListTag { get; }

    // Change Tracking and Saving
    ChangeTracker ChangeTracker { get; }
    int SaveChanges();
    int SaveChanges(bool acceptAllChangesOnSuccess);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default);

    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
}
