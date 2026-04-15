using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.OPDS;
using Kavita.Models.DTOs.OPDS.Requests;

namespace Kavita.API.Services;

public interface IOpdsService
{
    Task<Feed> GetCatalogue(OpdsCatalogueRequest request, CancellationToken ct = default);
    Task<Feed> GetSmartFilters(OpdsPaginatedCatalogueRequest request, CancellationToken ct = default);
    Task<Feed> GetLibraries(OpdsPaginatedCatalogueRequest request, CancellationToken ct = default);
    Task<Feed> GetWantToRead(OpdsPaginatedCatalogueRequest request, CancellationToken ct = default);
    Task<Feed> GetCollections(OpdsPaginatedCatalogueRequest request, CancellationToken ct = default);
    Task<Feed> GetReadingLists(OpdsPaginatedCatalogueRequest request, CancellationToken ct = default);
    Task<Feed> GetRecentlyAdded(OpdsPaginatedCatalogueRequest request, CancellationToken ct = default);
    Task<Feed> GetRecentlyUpdated(OpdsPaginatedCatalogueRequest request, CancellationToken ct = default);
    Task<Feed> GetOnDeck(OpdsPaginatedCatalogueRequest request, CancellationToken ct = default);

    Task<Feed> GetSeriesFromSmartFilter(OpdsItemsFromEntityIdRequest request, CancellationToken ct = default);
    Task<Feed> GetSeriesFromCollection(OpdsItemsFromEntityIdRequest request, CancellationToken ct = default);
    Task<Feed> GetSeriesFromLibrary(OpdsItemsFromEntityIdRequest request, CancellationToken ct = default);
    Task<Feed> GetReadingListItems(OpdsItemsFromEntityIdRequest request, CancellationToken ct = default);
    Task<Feed> GetSeriesDetail(OpdsItemsFromEntityIdRequest request, CancellationToken ct = default);
    Task<Feed> GetItemsFromVolume(OpdsItemsFromCompoundEntityIdsRequest request, CancellationToken ct = default);
    Task<Feed> GetItemsFromChapter(OpdsItemsFromCompoundEntityIdsRequest request, CancellationToken ct = default);

    Task<Feed> Search(OpdsSearchRequest request, CancellationToken ct = default);

    string SerializeXml(Feed? feed);
}
