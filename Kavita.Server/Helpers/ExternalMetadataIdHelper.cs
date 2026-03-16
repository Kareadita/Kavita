using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Common;
using Kavita.Models.Entities.Interfaces;

namespace Kavita.Server.Helpers;

public static class ExternalMetadataIdHelper
{
    public static void SetExternalMetadataIds(IHasMetadataIds entity, IUpdateExternalMetadataIds dto)
    {
        if (dto.AniListId is > 0)
        {
            entity.AniListId = dto.AniListId.Value;
        }

        if (dto.MalId is > 0)
        {
            entity.MalId = dto.MalId.Value;
        }

        if (dto.MangaBakaId is > 0)
        {
            entity.MangaBakaId = dto.MangaBakaId.Value;
        }

        if (dto.HardcoverId is > 0)
        {
            entity.HardcoverId = dto.HardcoverId.Value;
        }

        if (dto.MetronId is > 0)
        {
            entity.MetronId = dto.MetronId.Value;
        }

        if (!string.IsNullOrWhiteSpace(dto.ComicVineId))
        {
            entity.ComicVineId = dto.ComicVineId;
        }
    }
}
