BEGIN TRANSACTION;
CREATE TABLE IF NOT EXISTS "AspNetUsers" (
	"Id"	SERIAL NOT NULL,
	"AccessFailedCount"	INTEGER NOT NULL,
	"AgeRestriction"	INTEGER NOT NULL,
	"AgeRestrictionIncludeUnknowns"	BOOLEAN NOT NULL,
	"AniListAccessToken"	TEXT,
	"ApiKey"	TEXT,
	"ConcurrencyStamp"	TEXT,
	"ConfirmationToken"	TEXT,
	"Created"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"CreatedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"Email"	TEXT,
	"EmailConfirmed"	BOOLEAN NOT NULL,
	"LastActive"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LastActiveUtc"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LockoutEnabled"	BOOLEAN NOT NULL,
	"LockoutEnd"	TEXT,
	"NormalizedEmail"	TEXT,
	"NormalizedUserName"	TEXT,
	"PasswordHash"	TEXT,
	"PhoneNumber"	TEXT,
	"PhoneNumberConfirmed"	BOOLEAN NOT NULL,
	"RowVersion"	INTEGER NOT NULL,
	"SecurityStamp"	TEXT,
	"TwoFactorEnabled"	BOOLEAN NOT NULL,
	"UserName"	TEXT,
	"MalAccessToken"	TEXT,
	"MalUserName"	TEXT,
	PRIMARY KEY("Id")
);
CREATE TABLE IF NOT EXISTS "AppUserSmartFilter" (
	"Id"	SERIAL NOT NULL,
	"Name"	TEXT,
	"Filter"	TEXT,
	"AppUserId"	INTEGER NOT NULL,
	PRIMARY KEY("Id"),
	FOREIGN KEY("AppUserId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "Person" (
	"Id"	SERIAL NOT NULL,
	"Name"	TEXT,
	"NormalizedName"	TEXT,
	"Role"	INTEGER NOT NULL,
	PRIMARY KEY("Id")
);
CREATE TABLE IF NOT EXISTS "Genre" (
	"Id"	SERIAL NOT NULL,
	"NormalizedTitle"	TEXT,
	"Title"	TEXT,
	PRIMARY KEY("Id")
);
CREATE TABLE IF NOT EXISTS "Library" (
	"Id"	SERIAL NOT NULL,
	"AllowScrobbling"	BOOLEAN NOT NULL DEFAULT TRUE,
	"CoverImage"	TEXT,
	"Created"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"CreatedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"FolderWatching"	BOOLEAN NOT NULL,
	"IncludeInDashboard"	BOOLEAN NOT NULL,
	"IncludeInRecommended"	BOOLEAN NOT NULL,
	"IncludeInSearch"	BOOLEAN NOT NULL,
	"LastModified"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LastModifiedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LastScanned"	TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT '0001-01-01 00:00:00+00',
	"ManageCollections"	BOOLEAN NOT NULL,
	"ManageReadingLists"	BOOLEAN NOT NULL,
	"Name"	TEXT,
	"Type"	INTEGER NOT NULL,
	"PrimaryColor"	TEXT,
	"SecondaryColor"	TEXT,
	PRIMARY KEY("Id")
);
CREATE TABLE IF NOT EXISTS "Series" (
	"Id"	SERIAL NOT NULL,
	"AvgHoursToRead"	REAL NOT NULL,
	"CoverImage"	TEXT,
	"CoverImageLocked"	BOOLEAN NOT NULL,
	"Created"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"CreatedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"FolderPath"	TEXT,
	"Format"	INTEGER NOT NULL,
	"LastChapterAdded"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LastChapterAddedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LastFolderScanned"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LastFolderScannedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LastModified"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LastModifiedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LibraryId"	INTEGER NOT NULL,
	"LocalizedName"	TEXT,
	"LocalizedNameLocked"   BOOLEAN NOT NULL,
	"LowestFolderPath"	TEXT,
	"MaxHoursToRead"	INTEGER NOT NULL,
	"MinHoursToRead"	INTEGER NOT NULL,
	"Name"	TEXT,
	"NormalizedLocalizedName"	TEXT,
	"NormalizedName"	TEXT,
	"OriginalName"	TEXT,
	"Pages"	INTEGER NOT NULL,
	"PrimaryColor"	TEXT,
	"SecondaryColor"	TEXT,
	"SortName"	TEXT,
	"SortNameLocked"   BOOLEAN NOT NULL,
	"WordCount"	INTEGER NOT NULL,
	PRIMARY KEY("Id"),
	FOREIGN KEY("LibraryId") REFERENCES "Library"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "SeriesBlacklist" (
	"Id"	SERIAL NOT NULL,
	"SeriesId"	INTEGER NOT NULL,
	"LastChecked"	TIMESTAMP WITH TIME ZONE NOT NULL,
	PRIMARY KEY("Id"),
	FOREIGN KEY("SeriesId") REFERENCES "Series"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "SeriesMetadata" (
	"Id"	SERIAL NOT NULL,
	"SeriesId"	INTEGER NOT NULL,
	"RowVersion"	INTEGER NOT NULL,
	"Summary"	TEXT,
	"AgeRating"	INTEGER NOT NULL DEFAULT 0,
	"ReleaseYear"	INTEGER NOT NULL DEFAULT 0,
	"Language"	TEXT,
	"TotalCount"	INTEGER NOT NULL DEFAULT 0,
	"PublicationStatus"	INTEGER NOT NULL DEFAULT 0,
	"AgeRatingLocked"	BOOLEAN NOT NULL DEFAULT FALSE,
	"CharacterLocked"	BOOLEAN NOT NULL DEFAULT FALSE,
	"ColoristLocked"	BOOLEAN NOT NULL DEFAULT FALSE,
	"CoverArtistLocked"	BOOLEAN NOT NULL DEFAULT FALSE,
	"EditorLocked"	BOOLEAN NOT NULL DEFAULT FALSE,
	"GenresLocked"	BOOLEAN NOT NULL DEFAULT FALSE,
	"InkerLocked"	BOOLEAN NOT NULL DEFAULT FALSE,
	"LanguageLocked"	BOOLEAN NOT NULL DEFAULT FALSE,
	"LettererLocked"	BOOLEAN NOT NULL DEFAULT FALSE,
	"PencillerLocked"	BOOLEAN NOT NULL DEFAULT FALSE,
	"PublicationStatusLocked"	BOOLEAN NOT NULL DEFAULT FALSE,
	"PublisherLocked"	BOOLEAN NOT NULL DEFAULT FALSE,
	"SummaryLocked"	BOOLEAN NOT NULL DEFAULT FALSE,
	"TagsLocked"	BOOLEAN NOT NULL DEFAULT FALSE,
	"TranslatorLocked"	BOOLEAN NOT NULL DEFAULT FALSE,
	"WriterLocked"	BOOLEAN NOT NULL DEFAULT FALSE,
	"MaxCount"	INTEGER NOT NULL DEFAULT 0,
	"ReleaseYearLocked"	BOOLEAN NOT NULL DEFAULT FALSE,
	"WebLinks"	TEXT DEFAULT '',
	"ImprintLocked"	BOOLEAN NOT NULL DEFAULT FALSE,
	"LocationLocked"	BOOLEAN NOT NULL DEFAULT FALSE,
	"TeamLocked"	BOOLEAN NOT NULL DEFAULT FALSE,
	PRIMARY KEY("Id"),
	FOREIGN KEY("SeriesId") REFERENCES "Series"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "Tag" (
	"Id"	SERIAL NOT NULL,
	"NormalizedTitle"	TEXT,
	"Title"	TEXT,
	PRIMARY KEY("Id")
);
CREATE TABLE IF NOT EXISTS "SeriesMetadataTag" (
	"SeriesMetadatasId"	INTEGER NOT NULL,
	"TagsId"	INTEGER NOT NULL,
	PRIMARY KEY("SeriesMetadatasId","TagsId"),
	FOREIGN KEY("SeriesMetadatasId") REFERENCES "SeriesMetadata"("Id") ON DELETE CASCADE,
	FOREIGN KEY("TagsId") REFERENCES "Tag"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "SeriesRelation" (
	"Id"	SERIAL NOT NULL,
	"RelationKind"	INTEGER NOT NULL,
	"SeriesId"	INTEGER NOT NULL,
	"TargetSeriesId"	INTEGER NOT NULL,
	PRIMARY KEY("Id"),
	FOREIGN KEY("SeriesId") REFERENCES "Series"("Id") ON DELETE CASCADE,
	FOREIGN KEY("TargetSeriesId") REFERENCES "Series"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "ServerSetting" (
	"Key"	INTEGER NOT NULL,
	"RowVersion"	INTEGER NOT NULL,
	"Value"	TEXT,
	PRIMARY KEY("Key")
);
CREATE TABLE IF NOT EXISTS "ServerStatistics" (
	"Id"	SERIAL NOT NULL,
	"Year"	INTEGER NOT NULL,
	"SeriesCount"	INTEGER NOT NULL,
	"VolumeCount"	INTEGER NOT NULL,
	"ChapterCount"	INTEGER NOT NULL,
	"FileCount"	INTEGER NOT NULL,
	"UserCount"	INTEGER NOT NULL,
	"GenreCount"	INTEGER NOT NULL,
	"PersonCount"	INTEGER NOT NULL,
	"TagCount"	INTEGER NOT NULL,
	PRIMARY KEY("Id")
);
CREATE TABLE IF NOT EXISTS "SiteTheme" (
	"Id"	SERIAL NOT NULL,
	"Name"	TEXT,
	"NormalizedName"	TEXT,
	"FileName"	TEXT,
	"IsDefault"	BOOLEAN NOT NULL,
	"Provider"	INTEGER NOT NULL,
	"Created"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LastModified"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"CreatedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT '0001-01-01 00:00:00+00',
	"LastModifiedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT '0001-01-01 00:00:00+00',
	"Author"	TEXT,
	"CompatibleVersion"	TEXT,
	"Description"	TEXT,
	"GitHubPath"	TEXT,
	"PreviewUrls"	TEXT,
	"ShaHash"	TEXT,
	PRIMARY KEY("Id")
);
CREATE TABLE IF NOT EXISTS "Volume" (
	"Id"	SERIAL NOT NULL,
	"AvgHoursToRead"	REAL NOT NULL,
	"CoverImage"	TEXT,
	"CoverImageLocked"	BOOLEAN NOT NULL,
	"Created"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"CreatedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LastModified"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LastModifiedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LookupName"	TEXT,
	"MaxHoursToRead"	INTEGER NOT NULL,
	"MaxNumber"	REAL NOT NULL,
	"MinHoursToRead"	INTEGER NOT NULL,
	"MinNumber"	REAL NOT NULL,
	"Name"	TEXT,
	"Number"	INTEGER NOT NULL,
	"Pages"	INTEGER NOT NULL,
	"PrimaryColor"	TEXT,
	"SecondaryColor"	TEXT,
	"SeriesId"	INTEGER NOT NULL,
	"WordCount"	INTEGER NOT NULL,
	PRIMARY KEY("Id"),
	FOREIGN KEY("SeriesId") REFERENCES "Series"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "Chapter" (
	"Id"	SERIAL NOT NULL,
	"AgeRating"	INTEGER NOT NULL,
	"AgeRatingLocked"   BOOLEAN NOT NULL,
	"AlternateCount"	INTEGER NOT NULL,
	"AlternateNumber"	TEXT,
	"AlternateSeries"	TEXT,
	"AvgHoursToRead"	REAL NOT NULL,
	"CharacterLocked"   BOOLEAN NOT NULL,
	"ColoristLocked"   BOOLEAN NOT NULL,
	"Count"	INTEGER NOT NULL,
	"CoverArtistLocked"   BOOLEAN NOT NULL,
	"CoverImage"	TEXT,
	"CoverImageLocked"	BOOLEAN NOT NULL,
	"Created"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"CreatedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"EditorLocked"   BOOLEAN NOT NULL,
	"GenresLocked"   BOOLEAN NOT NULL,
	"ISBN"	TEXT DEFAULT '',
	"ISBNLocked"   BOOLEAN NOT NULL,
	"ImprintLocked"   BOOLEAN NOT NULL,
	"InkerLocked"   BOOLEAN NOT NULL,
	"IsSpecial"	BOOLEAN NOT NULL,
	"Language"	TEXT,
	"LanguageLocked"   BOOLEAN NOT NULL,
	"LastModified"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LastModifiedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LettererLocked"   BOOLEAN NOT NULL,
	"LocationLocked"   BOOLEAN NOT NULL,
	"MaxHoursToRead"	INTEGER NOT NULL,
	"MaxNumber"	REAL NOT NULL,
	"MinHoursToRead"	INTEGER NOT NULL,
	"MinNumber"	REAL NOT NULL,
	"Number"	TEXT,
	"Pages"	INTEGER NOT NULL,
	"PencillerLocked"   BOOLEAN NOT NULL,
	"PrimaryColor"	TEXT,
	"PublisherLocked"   BOOLEAN NOT NULL,
	"Range"	TEXT,
	"ReleaseDate"	TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT '0001-01-01 00:00:00+00',
	"ReleaseDateLocked"   BOOLEAN NOT NULL,
	"SecondaryColor"	TEXT,
	"SeriesGroup"	TEXT,
	"SortOrder"	REAL NOT NULL,
	"SortOrderLocked"   BOOLEAN NOT NULL,
	"StoryArc"	TEXT,
	"StoryArcNumber"	TEXT,
	"Summary"	TEXT,
	"SummaryLocked"   BOOLEAN NOT NULL,
	"TagsLocked"   BOOLEAN NOT NULL,
	"TeamLocked"   BOOLEAN NOT NULL,
	"Title"	TEXT,
	"TitleName"	TEXT,
	"TitleNameLocked"   BOOLEAN NOT NULL,
	"TotalCount"	INTEGER NOT NULL,
	"TranslatorLocked"   BOOLEAN NOT NULL,
	"VolumeId"	INTEGER NOT NULL,
	"WebLinks"	TEXT DEFAULT '',
	"WordCount"	INTEGER NOT NULL,
	"WriterLocked"   BOOLEAN NOT NULL,
	PRIMARY KEY("Id"),
	FOREIGN KEY("VolumeId") REFERENCES "Volume"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "ChapterGenre" (
	"ChaptersId"	INTEGER NOT NULL,
	"GenresId"	INTEGER NOT NULL,
	PRIMARY KEY("ChaptersId","GenresId"),
	FOREIGN KEY("ChaptersId") REFERENCES "Chapter"("Id") ON DELETE CASCADE,
	FOREIGN KEY("GenresId") REFERENCES "Genre"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "ChapterPerson" (
	"ChapterMetadatasId"	INTEGER NOT NULL,
	"PeopleId"	INTEGER NOT NULL,
	PRIMARY KEY("ChapterMetadatasId","PeopleId"),
	FOREIGN KEY("ChapterMetadatasId") REFERENCES "Chapter"("Id") ON DELETE CASCADE,
	FOREIGN KEY("PeopleId") REFERENCES "Person"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "ChapterTag" (
	"ChaptersId"	INTEGER NOT NULL,
	"TagsId"	INTEGER NOT NULL,
	PRIMARY KEY("ChaptersId","TagsId"),
	FOREIGN KEY("ChaptersId") REFERENCES "Chapter"("Id") ON DELETE CASCADE,
	FOREIGN KEY("TagsId") REFERENCES "Tag"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "CollectionTag" (
	"Id"	SERIAL NOT NULL,
	"CoverImage"	TEXT,
	"CoverImageLocked"	BOOLEAN NOT NULL,
	"NormalizedTitle"	TEXT,
	"Promoted"	BOOLEAN NOT NULL,
	"RowVersion"	INTEGER NOT NULL,
	"Summary"	TEXT,
	"Title"	TEXT,
	PRIMARY KEY("Id")
);
CREATE TABLE IF NOT EXISTS "CollectionTagSeriesMetadata" (
	"CollectionTagsId"	INTEGER NOT NULL,
	"SeriesMetadatasId"	INTEGER NOT NULL,
	PRIMARY KEY("CollectionTagsId","SeriesMetadatasId"),
	FOREIGN KEY("CollectionTagsId") REFERENCES "CollectionTag"("Id") ON DELETE CASCADE,
	FOREIGN KEY("SeriesMetadatasId") REFERENCES "SeriesMetadata"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "AppUserBookmark" (
	"Id"	SERIAL NOT NULL,
	"Page"	INTEGER NOT NULL,
	"VolumeId"	INTEGER NOT NULL,
	"SeriesId"	INTEGER NOT NULL,
	"ChapterId"	INTEGER NOT NULL,
	"AppUserId"	INTEGER NOT NULL,
	"FileName"	TEXT,
	"Created"	TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT '0001-01-01 00:00:00+00',
	"LastModified"	TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT '0001-01-01 00:00:00+00',
	"CreatedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT '0001-01-01 00:00:00+00',
	"LastModifiedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT '0001-01-01 00:00:00+00',
	PRIMARY KEY("Id"),
	FOREIGN KEY("AppUserId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "AppUserCollection" (
	"Id"	SERIAL NOT NULL,
	"Title"	TEXT,
	"NormalizedTitle"	TEXT,
	"Summary"	TEXT,
	"Promoted"	BOOLEAN NOT NULL,
	"CoverImage"	TEXT,
	"CoverImageLocked"	BOOLEAN NOT NULL,
	"AgeRating"	INTEGER NOT NULL DEFAULT 0,
	"Created"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LastModified"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"CreatedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LastModifiedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LastSyncUtc"	TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT '0001-01-01 00:00:00+00',
	"Source"	INTEGER NOT NULL,
	"SourceUrl"	TEXT,
	"AppUserId"	INTEGER NOT NULL,
	"MissingSeriesFromSource"	TEXT,
	"TotalSourceCount"	INTEGER NOT NULL DEFAULT 0,
	"PrimaryColor"	TEXT,
	"SecondaryColor"	TEXT,
	PRIMARY KEY("Id"),
	FOREIGN KEY("AppUserId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "AppUserCollectionSeries" (
	"CollectionsId"	INTEGER NOT NULL,
	"ItemsId"	INTEGER NOT NULL,
	PRIMARY KEY("CollectionsId","ItemsId"),
	FOREIGN KEY("CollectionsId") REFERENCES "AppUserCollection"("Id") ON DELETE CASCADE,
	FOREIGN KEY("ItemsId") REFERENCES "Series"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "AppUserDashboardStream" (
	"Id"	SERIAL NOT NULL,
	"Name"	TEXT,
	"IsProvided"	BOOLEAN NOT NULL,
	"Order"	INTEGER NOT NULL,
	"StreamType"	INTEGER NOT NULL DEFAULT 4,
	"Visible"	BOOLEAN NOT NULL,
	"SmartFilterId"	INTEGER,
	"AppUserId"	INTEGER NOT NULL,
	PRIMARY KEY("Id"),
	FOREIGN KEY("AppUserId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
	FOREIGN KEY("SmartFilterId") REFERENCES "AppUserSmartFilter"("Id")
);
CREATE TABLE IF NOT EXISTS "AppUserExternalSource" (
	"Id"	SERIAL NOT NULL,
	"Name"	TEXT,
	"Host"	TEXT,
	"ApiKey"	TEXT,
	"AppUserId"	INTEGER NOT NULL,
	PRIMARY KEY("Id"),
	FOREIGN KEY("AppUserId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "AppUserLibrary" (
	"AppUsersId"	INTEGER NOT NULL,
	"LibrariesId"	INTEGER NOT NULL,
	PRIMARY KEY("AppUsersId","LibrariesId"),
	FOREIGN KEY("AppUsersId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
	FOREIGN KEY("LibrariesId") REFERENCES "Library"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "AppUserOnDeckRemoval" (
	"Id"	SERIAL NOT NULL,
	"SeriesId"	INTEGER NOT NULL,
	"AppUserId"	INTEGER NOT NULL,
	PRIMARY KEY("Id"),
	FOREIGN KEY("AppUserId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
	FOREIGN KEY("SeriesId") REFERENCES "Series"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "AppUserPreferences" (
	"Id"	SERIAL NOT NULL,
	"AppUserId"	INTEGER NOT NULL,
	"AutoCloseMenu"	BOOLEAN NOT NULL,
	"BackgroundColor"	TEXT DEFAULT '#000000',
	"BlurUnreadSummaries"	BOOLEAN NOT NULL,
	"BookReaderFontFamily"	TEXT,
	"BookReaderFontSize"	INTEGER NOT NULL,
	"BookReaderImmersiveMode"	BOOLEAN NOT NULL,
	"BookReaderLayoutMode"	INTEGER NOT NULL,
	"BookReaderLineSpacing"	INTEGER NOT NULL,
	"BookReaderMargin"	INTEGER NOT NULL,
	"BookReaderReadingDirection"	INTEGER NOT NULL,
	"BookReaderTapToPaginate"	BOOLEAN NOT NULL,
	"BookReaderWritingStyle"	INTEGER NOT NULL DEFAULT 0,
	"BookThemeName"	TEXT DEFAULT 'Dark',
	"CollapseSeriesRelationships"	BOOLEAN NOT NULL,
	"EmulateBook"	BOOLEAN NOT NULL,
	"GlobalPageLayoutMode"	INTEGER NOT NULL DEFAULT 0,
	"LayoutMode"	INTEGER NOT NULL,
	"Locale"	TEXT NOT NULL DEFAULT 'en',
	"NoTransitions"	BOOLEAN NOT NULL,
	"PageSplitOption"	INTEGER NOT NULL,
	"PdfScrollMode"	INTEGER NOT NULL,
	"PdfSpreadMode"	INTEGER NOT NULL,
	"PdfTheme"	INTEGER NOT NULL,
	"PromptForDownloadSize"	BOOLEAN NOT NULL,
	"ReaderMode"	INTEGER NOT NULL,
	"ReadingDirection"	INTEGER NOT NULL,
	"ScalingOption"	INTEGER NOT NULL,
	"ShareReviews"	BOOLEAN NOT NULL,
	"ShowScreenHints"	BOOLEAN NOT NULL,
	"SwipeToPaginate"	BOOLEAN NOT NULL,
	"ThemeId"	INTEGER,
	PRIMARY KEY("Id"),
	FOREIGN KEY("AppUserId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
	FOREIGN KEY("ThemeId") REFERENCES "SiteTheme"("Id")
);
CREATE TABLE IF NOT EXISTS "AppUserProgresses" (
	"Id"	SERIAL NOT NULL,
	"AppUserId"	INTEGER NOT NULL,
	"BookScrollId"	TEXT,
	"ChapterId"	INTEGER NOT NULL,
	"Created"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"CreatedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LastModified"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LastModifiedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LibraryId"	INTEGER NOT NULL,
	"PagesRead"	INTEGER NOT NULL,
	"SeriesId"	INTEGER NOT NULL,
	"VolumeId"	INTEGER NOT NULL,
	PRIMARY KEY("Id"),
	FOREIGN KEY("AppUserId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
	FOREIGN KEY("ChapterId") REFERENCES "Chapter"("Id") ON DELETE CASCADE,
	FOREIGN KEY("SeriesId") REFERENCES "Series"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "AppUserRating" (
	"Id"	SERIAL NOT NULL,
	"AppUserId"	INTEGER NOT NULL,
	"HasBeenRated"	BOOLEAN NOT NULL,
	"Rating"	REAL NOT NULL,
	"Review"	TEXT,
	"SeriesId"	INTEGER NOT NULL,
	"Tagline"	TEXT,
	PRIMARY KEY("Id"),
	FOREIGN KEY("AppUserId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
	FOREIGN KEY("SeriesId") REFERENCES "Series"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "AppUserSideNavStream" (
	"Id"	SERIAL NOT NULL,
	"Name"	TEXT,
	"IsProvided"	BOOLEAN NOT NULL,
	"Order"	INTEGER NOT NULL,
	"LibraryId"	INTEGER,
	"ExternalSourceId"	INTEGER,
	"StreamType"	INTEGER NOT NULL DEFAULT 5,
	"Visible"	BOOLEAN NOT NULL,
	"SmartFilterId"	INTEGER,
	"AppUserId"	INTEGER NOT NULL,
	PRIMARY KEY("Id"),
	FOREIGN KEY("AppUserId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
	FOREIGN KEY("SmartFilterId") REFERENCES "AppUserSmartFilter"("Id")
);
CREATE TABLE IF NOT EXISTS "AppUserTableOfContent" (
	"Id"	SERIAL NOT NULL,
	"PageNumber"	INTEGER NOT NULL,
	"Title"	TEXT,
	"SeriesId"	INTEGER NOT NULL,
	"ChapterId"	INTEGER NOT NULL,
	"VolumeId"	INTEGER NOT NULL,
	"LibraryId"	INTEGER NOT NULL,
	"BookScrollId"	TEXT,
	"Created"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"CreatedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LastModified"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LastModifiedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"AppUserId"	INTEGER NOT NULL,
	PRIMARY KEY("Id"),
	FOREIGN KEY("AppUserId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
	FOREIGN KEY("ChapterId") REFERENCES "Chapter"("Id") ON DELETE CASCADE,
	FOREIGN KEY("SeriesId") REFERENCES "Series"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "AppUserWantToRead" (
	"Id"	SERIAL NOT NULL,
	"SeriesId"	INTEGER NOT NULL,
	"AppUserId"	INTEGER NOT NULL,
	PRIMARY KEY("Id"),
	FOREIGN KEY("AppUserId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
	FOREIGN KEY("SeriesId") REFERENCES "Series"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "AspNetRoles" (
	"Id"	SERIAL NOT NULL,
	"Name"	TEXT,
	"NormalizedName"	TEXT,
	"ConcurrencyStamp"	TEXT,
	PRIMARY KEY("Id")
);
CREATE TABLE IF NOT EXISTS "AspNetRoleClaims" (
	"Id"	SERIAL NOT NULL,
	"RoleId"	INTEGER NOT NULL,
	"ClaimType"	TEXT,
	"ClaimValue"	TEXT,
	PRIMARY KEY("Id"),
	FOREIGN KEY("RoleId") REFERENCES "AspNetRoles"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "AspNetUserClaims" (
	"Id"	SERIAL NOT NULL,
	"UserId"	INTEGER NOT NULL,
	"ClaimType"	TEXT,
	"ClaimValue"	TEXT,
	PRIMARY KEY("Id"),
	FOREIGN KEY("UserId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "AspNetUserLogins" (
	"LoginProvider"	TEXT NOT NULL,
	"ProviderKey"	TEXT NOT NULL,
	"ProviderDisplayName"	TEXT,
	"UserId"	INTEGER NOT NULL,
	PRIMARY KEY("LoginProvider","ProviderKey"),
	FOREIGN KEY("UserId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "AspNetUserRoles" (
	"UserId"	INTEGER NOT NULL,
	"RoleId"	INTEGER NOT NULL,
	PRIMARY KEY("UserId","RoleId"),
	FOREIGN KEY("RoleId") REFERENCES "AspNetRoles"("Id") ON DELETE CASCADE,
	FOREIGN KEY("UserId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "AspNetUserTokens" (
	"UserId"	INTEGER NOT NULL,
	"LoginProvider"	TEXT NOT NULL,
	"Name"	TEXT NOT NULL,
	"Value"	TEXT,
	PRIMARY KEY("UserId","LoginProvider","Name"),
	FOREIGN KEY("UserId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "Device" (
	"Id"	SERIAL NOT NULL,
	"IpAddress"	TEXT,
	"Name"	TEXT,
	"EmailAddress"	TEXT,
	"Platform"	INTEGER NOT NULL,
	"AppUserId"	INTEGER NOT NULL,
	"LastUsed"	TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT '0001-01-01 00:00:00+00',
	"Created"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LastModified"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"CreatedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT '0001-01-01 00:00:00+00',
	"LastModifiedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT '0001-01-01 00:00:00+00',
	"LastUsedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT '0001-01-01 00:00:00+00',
	PRIMARY KEY("Id"),
	FOREIGN KEY("AppUserId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "ExternalRating" (
	"Id"	SERIAL NOT NULL,
	"AverageScore"	INTEGER NOT NULL,
	"FavoriteCount"	INTEGER NOT NULL,
	"Provider"	INTEGER NOT NULL,
	"ProviderUrl"	TEXT,
	"SeriesId"	INTEGER NOT NULL,
	PRIMARY KEY("Id")
);
CREATE TABLE IF NOT EXISTS "ExternalSeriesMetadata" (
	"Id"	SERIAL NOT NULL,
	"AverageExternalRating"	INTEGER NOT NULL,
	"AniListId"	INTEGER NOT NULL,
	"MalId"	INTEGER NOT NULL,
	"GoogleBooksId"	TEXT,
	"ValidUntilUtc"	TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT '0001-01-01 00:00:00+00',
	"SeriesId"	INTEGER NOT NULL,
	PRIMARY KEY("Id"),
	FOREIGN KEY("SeriesId") REFERENCES "Series"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "ExternalRatingExternalSeriesMetadata" (
	"ExternalRatingsId"	INTEGER NOT NULL,
	"ExternalSeriesMetadatasId"	INTEGER NOT NULL,
	PRIMARY KEY("ExternalRatingsId","ExternalSeriesMetadatasId"),
	FOREIGN KEY("ExternalRatingsId") REFERENCES "ExternalRating"("Id") ON DELETE CASCADE,
	FOREIGN KEY("ExternalSeriesMetadatasId") REFERENCES "ExternalSeriesMetadata"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "ExternalRecommendation" (
	"Id"	SERIAL NOT NULL,
	"AniListId"	INTEGER,
	"CoverUrl"	TEXT,
	"MalId"	INTEGER,
	"Name"	TEXT,
	"Provider"	INTEGER NOT NULL,
	"SeriesId"	INTEGER,
	"Summary"	TEXT,
	"Url"	TEXT,
	PRIMARY KEY("Id")
);
CREATE TABLE IF NOT EXISTS "ExternalRecommendationExternalSeriesMetadata" (
	"ExternalRecommendationsId"	INTEGER NOT NULL,
	"ExternalSeriesMetadatasId"	INTEGER NOT NULL,
	PRIMARY KEY("ExternalRecommendationsId","ExternalSeriesMetadatasId"),
	FOREIGN KEY("ExternalRecommendationsId") REFERENCES "ExternalRecommendation"("Id") ON DELETE CASCADE,
	FOREIGN KEY("ExternalSeriesMetadatasId") REFERENCES "ExternalSeriesMetadata"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "ExternalReview" (
	"Id"	SERIAL NOT NULL,
	"Tagline"	TEXT,
	"Body"	TEXT,
	"BodyJustText"	TEXT,
	"RawBody"	TEXT,
	"Provider"	INTEGER NOT NULL,
	"SiteUrl"	TEXT,
	"Username"	TEXT,
	"Rating"	INTEGER NOT NULL,
	"Score"	INTEGER NOT NULL,
	"TotalVotes"	INTEGER NOT NULL,
	"SeriesId"	INTEGER NOT NULL,
	PRIMARY KEY("Id")
);
CREATE TABLE IF NOT EXISTS "ExternalReviewExternalSeriesMetadata" (
	"ExternalReviewsId"	INTEGER NOT NULL,
	"ExternalSeriesMetadatasId"	INTEGER NOT NULL,
	PRIMARY KEY("ExternalReviewsId","ExternalSeriesMetadatasId"),
	FOREIGN KEY("ExternalReviewsId") REFERENCES "ExternalReview"("Id") ON DELETE CASCADE,
	FOREIGN KEY("ExternalSeriesMetadatasId") REFERENCES "ExternalSeriesMetadata"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "FolderPath" (
	"Id"	SERIAL NOT NULL,
	"LibraryId"	INTEGER NOT NULL,
	"Path"	TEXT,
	"LastScanned"	TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT '0001-01-01 00:00:00+00',
	PRIMARY KEY("Id"),
	FOREIGN KEY("LibraryId") REFERENCES "Library"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "GenreSeriesMetadata" (
	"GenresId"	INTEGER NOT NULL,
	"SeriesMetadatasId"	INTEGER NOT NULL,
	PRIMARY KEY("GenresId","SeriesMetadatasId"),
	FOREIGN KEY("GenresId") REFERENCES "Genre"("Id") ON DELETE CASCADE,
	FOREIGN KEY("SeriesMetadatasId") REFERENCES "SeriesMetadata"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "LibraryExcludePattern" (
	"Id"	SERIAL NOT NULL,
	"Pattern"	TEXT,
	"LibraryId"	INTEGER NOT NULL,
	PRIMARY KEY("Id"),
	FOREIGN KEY("LibraryId") REFERENCES "Library"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "LibraryFileTypeGroup" (
	"Id"	SERIAL NOT NULL,
	"LibraryId"	INTEGER NOT NULL,
	"FileTypeGroup"	INTEGER NOT NULL,
	PRIMARY KEY("Id"),
	FOREIGN KEY("LibraryId") REFERENCES "Library"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "MangaFile" (
	"Id"	SERIAL NOT NULL,
	"ChapterId"	INTEGER NOT NULL,
	"FilePath"	TEXT,
	"Format"	INTEGER NOT NULL,
	"Pages"	INTEGER NOT NULL,
	"LastModified"	TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT '0001-01-01 00:00:00+00',
	"LastFileAnalysis"	TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT '0001-01-01 00:00:00+00',
	"Created"	TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT '0001-01-01 00:00:00+00',
	"Bytes"	INTEGER NOT NULL DEFAULT 0,
	"Extension"	TEXT,
	"CreatedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT '0001-01-01 00:00:00+00',
	"LastFileAnalysisUtc"	TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT '0001-01-01 00:00:00+00',
	"LastModifiedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT '0001-01-01 00:00:00+00',
	"FileName"	TEXT,
	PRIMARY KEY("Id"),
	FOREIGN KEY("ChapterId") REFERENCES "Chapter"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "ManualMigrationHistory" (
	"Id"	SERIAL NOT NULL,
	"ProductVersion"	TEXT,
	"Name"	TEXT,
	"RanAt"	TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT '0001-01-01 00:00:00+00',
	PRIMARY KEY("Id")
);
CREATE TABLE IF NOT EXISTS "MediaError" (
	"Id"	SERIAL NOT NULL,
	"Extension"	TEXT,
	"FilePath"	TEXT,
	"Comment"	TEXT,
	"Details"	TEXT,
	"Created"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LastModified"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"CreatedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LastModifiedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL,
	PRIMARY KEY("Id")
);
CREATE TABLE IF NOT EXISTS "PersonSeriesMetadata" (
	"PeopleId"	INTEGER NOT NULL,
	"SeriesMetadatasId"	INTEGER NOT NULL,
	PRIMARY KEY("PeopleId","SeriesMetadatasId"),
	FOREIGN KEY("PeopleId") REFERENCES "Person"("Id") ON DELETE CASCADE,
	FOREIGN KEY("SeriesMetadatasId") REFERENCES "SeriesMetadata"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "ReadingList" (
	"Id"	SERIAL NOT NULL,
	"AgeRating"	INTEGER NOT NULL,
	"AppUserId"	INTEGER NOT NULL,
	"CoverImage"	TEXT,
	"CoverImageLocked"	BOOLEAN NOT NULL,
	"Created"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"CreatedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"EndingMonth"	INTEGER NOT NULL,
	"EndingYear"	INTEGER NOT NULL,
	"LastModified"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LastModifiedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"NormalizedTitle"	TEXT NOT NULL,
	"Promoted"	BOOLEAN NOT NULL,
	"StartingMonth"	INTEGER NOT NULL,
	"StartingYear"	INTEGER NOT NULL,
	"Summary"	TEXT,
	"Title"	TEXT NOT NULL,
	"PrimaryColor"	TEXT,
	"SecondaryColor"	TEXT,
	PRIMARY KEY("Id"),
	FOREIGN KEY("AppUserId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "ReadingListItem" (
	"Id"	SERIAL NOT NULL,
	"ChapterId"	INTEGER NOT NULL,
	"Order"	INTEGER NOT NULL,
	"ReadingListId"	INTEGER NOT NULL,
	"SeriesId"	INTEGER NOT NULL,
	"VolumeId"	INTEGER NOT NULL,
	PRIMARY KEY("Id"),
	FOREIGN KEY("ChapterId") REFERENCES "Chapter"("Id") ON DELETE CASCADE,
	FOREIGN KEY("ReadingListId") REFERENCES "ReadingList"("Id") ON DELETE CASCADE,
	FOREIGN KEY("SeriesId") REFERENCES "Series"("Id") ON DELETE CASCADE,
	FOREIGN KEY("VolumeId") REFERENCES "Volume"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "ScrobbleEvent" (
	"Id"	SERIAL NOT NULL,
	"AniListId"	INTEGER,
	"AppUserId"	INTEGER NOT NULL,
	"ChapterNumber"	INTEGER,
	"Created"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"CreatedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"ErrorDetails"	TEXT,
	"Format"	INTEGER NOT NULL,
	"IsErrored"	BOOLEAN NOT NULL,
	"IsProcessed"	BOOLEAN NOT NULL,
	"LastModified"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LastModifiedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LibraryId"	INTEGER NOT NULL,
	"MalId"	INTEGER,
	"ProcessDateUtc"	TEXT,
	"Rating"	REAL,
	"ReviewBody"	TEXT,
	"ReviewTitle"	TEXT,
	"ScrobbleEventType"	INTEGER NOT NULL,
	"SeriesId"	INTEGER NOT NULL,
	"VolumeNumber"	REAL,
	PRIMARY KEY("Id"),
	FOREIGN KEY("AppUserId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
	FOREIGN KEY("LibraryId") REFERENCES "Library"("Id") ON DELETE CASCADE,
	FOREIGN KEY("SeriesId") REFERENCES "Series"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "ScrobbleError" (
	"Id"	SERIAL NOT NULL,
	"Comment"	TEXT,
	"Details"	TEXT,
	"SeriesId"	INTEGER NOT NULL,
	"LibraryId"	INTEGER NOT NULL,
	"ScrobbleEventId"	INTEGER NOT NULL,
	"ScrobbleEventId1"	INTEGER,
	"Created"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LastModified"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"CreatedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LastModifiedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL,
	PRIMARY KEY("Id"),
	FOREIGN KEY("ScrobbleEventId1") REFERENCES "ScrobbleEvent"("Id"),
	FOREIGN KEY("SeriesId") REFERENCES "Series"("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "ScrobbleHold" (
	"Id"	SERIAL NOT NULL,
	"SeriesId"	INTEGER NOT NULL,
	"AppUserId"	INTEGER NOT NULL,
	"Created"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"CreatedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LastModified"	TIMESTAMP WITH TIME ZONE NOT NULL,
	"LastModifiedUtc"	TIMESTAMP WITH TIME ZONE NOT NULL,
	PRIMARY KEY("Id"),
	FOREIGN KEY("AppUserId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
	FOREIGN KEY("SeriesId") REFERENCES "Series"("Id") ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
	"MigrationId"	TEXT NOT NULL,
	"ProductVersion"	TEXT NOT NULL,
	PRIMARY KEY("MigrationId")
);
CREATE INDEX IF NOT EXISTS "EmailIndex" ON "AspNetUsers" (
	"NormalizedEmail"
);
CREATE INDEX IF NOT EXISTS "IX_AppUserBookmark_AppUserId" ON "AppUserBookmark" (
	"AppUserId"
);
CREATE INDEX IF NOT EXISTS "IX_AppUserCollectionSeries_ItemsId" ON "AppUserCollectionSeries" (
	"ItemsId"
);
CREATE INDEX IF NOT EXISTS "IX_AppUserCollection_AppUserId" ON "AppUserCollection" (
	"AppUserId"
);
CREATE INDEX IF NOT EXISTS "IX_AppUserDashboardStream_AppUserId" ON "AppUserDashboardStream" (
	"AppUserId"
);
CREATE INDEX IF NOT EXISTS "IX_AppUserDashboardStream_SmartFilterId" ON "AppUserDashboardStream" (
	"SmartFilterId"
);
CREATE INDEX IF NOT EXISTS "IX_AppUserDashboardStream_Visible" ON "AppUserDashboardStream" (
	"Visible"
);
CREATE INDEX IF NOT EXISTS "IX_AppUserExternalSource_AppUserId" ON "AppUserExternalSource" (
	"AppUserId"
);
CREATE INDEX IF NOT EXISTS "IX_AppUserLibrary_LibrariesId" ON "AppUserLibrary" (
	"LibrariesId"
);
CREATE INDEX IF NOT EXISTS "IX_AppUserOnDeckRemoval_AppUserId" ON "AppUserOnDeckRemoval" (
	"AppUserId"
);
CREATE INDEX IF NOT EXISTS "IX_AppUserOnDeckRemoval_SeriesId" ON "AppUserOnDeckRemoval" (
	"SeriesId"
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_AppUserPreferences_AppUserId" ON "AppUserPreferences" (
	"AppUserId"
);
CREATE INDEX IF NOT EXISTS "IX_AppUserPreferences_ThemeId" ON "AppUserPreferences" (
	"ThemeId"
);
CREATE INDEX IF NOT EXISTS "IX_AppUserProgresses_AppUserId" ON "AppUserProgresses" (
	"AppUserId"
);
CREATE INDEX IF NOT EXISTS "IX_AppUserProgresses_ChapterId" ON "AppUserProgresses" (
	"ChapterId"
);
CREATE INDEX IF NOT EXISTS "IX_AppUserProgresses_SeriesId" ON "AppUserProgresses" (
	"SeriesId"
);
CREATE INDEX IF NOT EXISTS "IX_AppUserRating_AppUserId" ON "AppUserRating" (
	"AppUserId"
);
CREATE INDEX IF NOT EXISTS "IX_AppUserRating_SeriesId" ON "AppUserRating" (
	"SeriesId"
);
CREATE INDEX IF NOT EXISTS "IX_AppUserSideNavStream_AppUserId" ON "AppUserSideNavStream" (
	"AppUserId"
);
CREATE INDEX IF NOT EXISTS "IX_AppUserSideNavStream_SmartFilterId" ON "AppUserSideNavStream" (
	"SmartFilterId"
);
CREATE INDEX IF NOT EXISTS "IX_AppUserSideNavStream_Visible" ON "AppUserSideNavStream" (
	"Visible"
);
CREATE INDEX IF NOT EXISTS "IX_AppUserSmartFilter_AppUserId" ON "AppUserSmartFilter" (
	"AppUserId"
);
CREATE INDEX IF NOT EXISTS "IX_AppUserTableOfContent_AppUserId" ON "AppUserTableOfContent" (
	"AppUserId"
);
CREATE INDEX IF NOT EXISTS "IX_AppUserTableOfContent_ChapterId" ON "AppUserTableOfContent" (
	"ChapterId"
);
CREATE INDEX IF NOT EXISTS "IX_AppUserTableOfContent_SeriesId" ON "AppUserTableOfContent" (
	"SeriesId"
);
CREATE INDEX IF NOT EXISTS "IX_AppUserWantToRead_AppUserId" ON "AppUserWantToRead" (
	"AppUserId"
);
CREATE INDEX IF NOT EXISTS "IX_AppUserWantToRead_SeriesId" ON "AppUserWantToRead" (
	"SeriesId"
);
CREATE INDEX IF NOT EXISTS "IX_AspNetRoleClaims_RoleId" ON "AspNetRoleClaims" (
	"RoleId"
);
CREATE INDEX IF NOT EXISTS "IX_AspNetUserClaims_UserId" ON "AspNetUserClaims" (
	"UserId"
);
CREATE INDEX IF NOT EXISTS "IX_AspNetUserLogins_UserId" ON "AspNetUserLogins" (
	"UserId"
);
CREATE INDEX IF NOT EXISTS "IX_AspNetUserRoles_RoleId" ON "AspNetUserRoles" (
	"RoleId"
);
CREATE INDEX IF NOT EXISTS "IX_ChapterGenre_GenresId" ON "ChapterGenre" (
	"GenresId"
);
CREATE INDEX IF NOT EXISTS "IX_ChapterPerson_PeopleId" ON "ChapterPerson" (
	"PeopleId"
);
CREATE INDEX IF NOT EXISTS "IX_ChapterTag_TagsId" ON "ChapterTag" (
	"TagsId"
);
CREATE INDEX IF NOT EXISTS "IX_Chapter_VolumeId" ON "Chapter" (
	"VolumeId"
);
CREATE INDEX IF NOT EXISTS "IX_CollectionTagSeriesMetadata_SeriesMetadatasId" ON "CollectionTagSeriesMetadata" (
	"SeriesMetadatasId"
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_CollectionTag_Id_Promoted" ON "CollectionTag" (
	"Id",
	"Promoted"
);
CREATE INDEX IF NOT EXISTS "IX_Device_AppUserId" ON "Device" (
	"AppUserId"
);
CREATE INDEX IF NOT EXISTS "IX_ExternalRatingExternalSeriesMetadata_ExternalSeriesMetadatasId" ON "ExternalRatingExternalSeriesMetadata" (
	"ExternalSeriesMetadatasId"
);
CREATE INDEX IF NOT EXISTS "IX_ExternalRecommendationExternalSeriesMetadata_ExternalSeriesMetadatasId" ON "ExternalRecommendationExternalSeriesMetadata" (
	"ExternalSeriesMetadatasId"
);
CREATE INDEX IF NOT EXISTS "IX_ExternalRecommendation_SeriesId" ON "ExternalRecommendation" (
	"SeriesId"
);
CREATE INDEX IF NOT EXISTS "IX_ExternalReviewExternalSeriesMetadata_ExternalSeriesMetadatasId" ON "ExternalReviewExternalSeriesMetadata" (
	"ExternalSeriesMetadatasId"
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_ExternalSeriesMetadata_SeriesId" ON "ExternalSeriesMetadata" (
	"SeriesId"
);
CREATE INDEX IF NOT EXISTS "IX_FolderPath_LibraryId" ON "FolderPath" (
	"LibraryId"
);
CREATE INDEX IF NOT EXISTS "IX_GenreSeriesMetadata_SeriesMetadatasId" ON "GenreSeriesMetadata" (
	"SeriesMetadatasId"
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Genre_NormalizedTitle" ON "Genre" (
	"NormalizedTitle"
);
CREATE INDEX IF NOT EXISTS "IX_LibraryExcludePattern_LibraryId" ON "LibraryExcludePattern" (
	"LibraryId"
);
CREATE INDEX IF NOT EXISTS "IX_LibraryFileTypeGroup_LibraryId" ON "LibraryFileTypeGroup" (
	"LibraryId"
);
CREATE INDEX IF NOT EXISTS "IX_MangaFile_ChapterId" ON "MangaFile" (
	"ChapterId"
);
CREATE INDEX IF NOT EXISTS "IX_PersonSeriesMetadata_SeriesMetadatasId" ON "PersonSeriesMetadata" (
	"SeriesMetadatasId"
);
CREATE INDEX IF NOT EXISTS "IX_ReadingListItem_ChapterId" ON "ReadingListItem" (
	"ChapterId"
);
CREATE INDEX IF NOT EXISTS "IX_ReadingListItem_ReadingListId" ON "ReadingListItem" (
	"ReadingListId"
);
CREATE INDEX IF NOT EXISTS "IX_ReadingListItem_SeriesId" ON "ReadingListItem" (
	"SeriesId"
);
CREATE INDEX IF NOT EXISTS "IX_ReadingListItem_VolumeId" ON "ReadingListItem" (
	"VolumeId"
);
CREATE INDEX IF NOT EXISTS "IX_ReadingList_AppUserId" ON "ReadingList" (
	"AppUserId"
);
CREATE INDEX IF NOT EXISTS "IX_ScrobbleError_ScrobbleEventId1" ON "ScrobbleError" (
	"ScrobbleEventId1"
);
CREATE INDEX IF NOT EXISTS "IX_ScrobbleError_SeriesId" ON "ScrobbleError" (
	"SeriesId"
);
CREATE INDEX IF NOT EXISTS "IX_ScrobbleEvent_AppUserId" ON "ScrobbleEvent" (
	"AppUserId"
);
CREATE INDEX IF NOT EXISTS "IX_ScrobbleEvent_LibraryId" ON "ScrobbleEvent" (
	"LibraryId"
);
CREATE INDEX IF NOT EXISTS "IX_ScrobbleEvent_SeriesId" ON "ScrobbleEvent" (
	"SeriesId"
);
CREATE INDEX IF NOT EXISTS "IX_ScrobbleHold_AppUserId" ON "ScrobbleHold" (
	"AppUserId"
);
CREATE INDEX IF NOT EXISTS "IX_ScrobbleHold_SeriesId" ON "ScrobbleHold" (
	"SeriesId"
);
CREATE INDEX IF NOT EXISTS "IX_SeriesBlacklist_SeriesId" ON "SeriesBlacklist" (
	"SeriesId"
);
CREATE INDEX IF NOT EXISTS "IX_SeriesMetadataTag_TagsId" ON "SeriesMetadataTag" (
	"TagsId"
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_SeriesMetadata_Id_SeriesId" ON "SeriesMetadata" (
	"Id",
	"SeriesId"
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_SeriesMetadata_SeriesId" ON "SeriesMetadata" (
	"SeriesId"
);
CREATE INDEX IF NOT EXISTS "IX_SeriesRelation_SeriesId" ON "SeriesRelation" (
	"SeriesId"
);
CREATE INDEX IF NOT EXISTS "IX_SeriesRelation_TargetSeriesId" ON "SeriesRelation" (
	"TargetSeriesId"
);
CREATE INDEX IF NOT EXISTS "IX_Series_LibraryId" ON "Series" (
	"LibraryId"
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Tag_NormalizedTitle" ON "Tag" (
	"NormalizedTitle"
);
CREATE INDEX IF NOT EXISTS "IX_Volume_SeriesId" ON "Volume" (
	"SeriesId"
);
CREATE UNIQUE INDEX IF NOT EXISTS "RoleNameIndex" ON "AspNetRoles" (
	"NormalizedName"
);
CREATE UNIQUE INDEX IF NOT EXISTS "UserNameIndex" ON "AspNetUsers" (
	"NormalizedUserName"
);
COMMIT;
