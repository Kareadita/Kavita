using Kavita.Models.DTOs.Common;
using Kavita.Models.Entities.Interfaces;

namespace Kavita.Server.Helpers;

public static class ExternalMetadataIdHelper
{
    public static void SetExternalMetadataIds(IHasMetadataIds entity, IUpdateExternalMetadataIds dto)
    {
        entity.AniListId = dto.AniListId ?? 0;
        entity.MalId = dto.MalId ?? 0;
        entity.MangaBakaId = dto.MangaBakaId ?? 0;
        entity.HardcoverId = dto.HardcoverId ?? 0;
        entity.MetronId = dto.MetronId ?? 0;
        entity.CbrId = dto.CbrId ?? 0;
        entity.ComicVineId = dto.ComicVineId;
    }

    public static void ResetExternalMetadataIds(this IHasMetadataIds entity)
    {
        entity.AniListId = 0;
        entity.MalId = 0;
        entity.MangaBakaId = 0;
        entity.HardcoverId = 0;
        entity.MetronId = 0;
        entity.CbrId = 0;
        entity.ComicVineId = string.Empty;
    }
}
