using System;

namespace Kavita.Services.Kobo;

/// <summary>
/// Deterministic UUID v5 Tag ids for Kobo shelves (Reading Lists / Collections).
/// Uses the same Kavita namespace as chapter entitlements; names are
/// <c>readinglist:{id}</c> and <c>collection:{id}</c>.
/// </summary>
public static class KoboTagId
{
    public static Guid FromReadingListId(int readingListId) =>
        KoboEntitlementId.CreateVersion5(KoboEntitlementId.Namespace, $"readinglist:{readingListId}");

    public static string FromReadingListIdString(int readingListId) =>
        FromReadingListId(readingListId).ToString();

    public static Guid FromCollectionId(int collectionId) =>
        KoboEntitlementId.CreateVersion5(KoboEntitlementId.Namespace, $"collection:{collectionId}");

    public static string FromCollectionIdString(int collectionId) =>
        FromCollectionId(collectionId).ToString();
}
