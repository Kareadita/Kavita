---
applyTo: "Kavita.Server/Controllers/**/*.cs,Kavita.Services/**/*.cs,Kavita.Database/Repositories/**/*.cs,Kavita.Server.Tests/Controllers/EndpointAccessTests.cs"
excludeAgent: "cloud-agent"
---
# Authorization review

Kavita is multi-user. A user may only see libraries they are granted, and content within their age restriction. Flag any of the following as a security issue. Name the endpoint and the unchecked id.

## Access attributes
`[SeriesAccess]`, `[VolumeAccess]`, `[ChapterAccess]`, `[LibraryAccess]`, `[PersonAccess]`, `[ReadingListAccess]` (Kavita.Server/Attributes/EntityAccessAttribute.cs) check ONE int read from the route or query string. They do not read the request body, and they do not handle id lists. Admin-only endpoints (`[Authorize(PolicyGroups.AdminPolicy)]`) are exempt.

## Flag these
1. **Missing attribute.** A non-admin action takes `seriesId`/`volumeId`/`chapterId`/`libraryId`/`personId`/`readingListId` from route or query, has no matching attribute, and no access check in the body.
2. **Ids in the body or in a list.** A DTO or query carries `SeriesId(s)`, `ChapterIds`, `VolumeIds`, `LibraryIds` and so on. Each id must be checked with `HasAccessToSeries/Volume/Chapter/Library(userId, id)`, or the query must be user-scoped, e.g. `ReadingListRepository.GetFilesizesAsync(ids, userId)`. Example of the bug: `POST /api/download/bulk-series-size` returned sizes for any `SeriesIds`, including libraries the user cannot see.
3. **Ownership of user-owned rows.** An update or delete loads a row by id (dashboard/side-nav streams, filters, annotations, bookmarks, reviews, ratings, reading profiles, devices, holds) and changes it without checking `row.AppUserId == userId`. Example of the bug: `UpdateSideNavStream` toggled another user's stream from a guessed `dto.Id`.
4. **Child id resolves to a restricted parent.** A lookup by `annotationId`, `bookmarkId`, `reviewId` and similar returns data without checking the parent series' library access and age rating.
5. **Unscoped repository query.** A repository method returns series, chapter, person or annotation data for a non-admin caller without library scoping and age-restriction filtering. Social or sharing preferences (e.g. `RestrictBySocialPreferences`) are NOT access control. Compare with `CreatedFilteredAnnotationQueryable`.
6. **Weak `userId` source.** `userId` comes from the request instead of `UserContext` / `User.GetUserId()`, or it is accepted and never used.
7. **Allowlist edits.** `EndpointAccessTests.cs` has `RouteQueryAllowlist` and `DtoIdAllowlist`. A new entry must give a specific reason that points at the check (e.g. "HasAccessToSeries in body", "apiKey user resolved, then HasAccessToLibrary"). Flag entries with a vague reason ("safe", "not needed", "TODO"), and flag an entry added in the same PR as a new endpoint when the attribute could simply be used instead.

## Do not flag
- Admin-only endpoints.
- Ids that are only used to scope a query already filtered by the current user's id.
- Missing checks that a user-scoped repository method already performs. Read the repository method before reporting.
