using System.Threading.Tasks;
using Kavita.Models.DTOs.OPDS;
using Kavita.Models.DTOs.OPDS.Requests;

namespace Kavita.API.Services;

public interface IOpdsService
{
    Task<Feed> GetCatalogue(OpdsCatalogueRequest request);
    Task<Feed> GetSmartFilters(OpdsPaginatedCatalogueRequest request);
    Task<Feed> GetLibraries(OpdsPaginatedCatalogueRequest request);
    Task<Feed> GetWantToRead(OpdsPaginatedCatalogueRequest request);
    Task<Feed> GetCollections(OpdsPaginatedCatalogueRequest request);
    Task<Feed> GetReadingLists(OpdsPaginatedCatalogueRequest request);
    Task<Feed> GetRecentlyAdded(OpdsPaginatedCatalogueRequest request);
    Task<Feed> GetRecentlyUpdated(OpdsPaginatedCatalogueRequest request);
    Task<Feed> GetOnDeck(OpdsPaginatedCatalogueRequest request);

    Task<Feed> GetMoreInGenre(OpdsItemsFromEntityIdRequest request);
    Task<Feed> GetSeriesFromSmartFilter(OpdsItemsFromEntityIdRequest request);
    Task<Feed> GetSeriesFromCollection(OpdsItemsFromEntityIdRequest request);
    Task<Feed> GetSeriesFromLibrary(OpdsItemsFromEntityIdRequest request);
    Task<Feed> GetReadingListItems(OpdsItemsFromEntityIdRequest request);
    Task<Feed> GetSeriesDetail(OpdsItemsFromEntityIdRequest request);
    Task<Feed> GetItemsFromVolume(OpdsItemsFromCompoundEntityIdsRequest request);
    Task<Feed> GetItemsFromChapter(OpdsItemsFromCompoundEntityIdsRequest request);

    Task<Feed> Search(OpdsSearchRequest request);

    string SerializeXml(Feed? feed);
}
