using API.Entities.Interfaces;
using API.Entities.MetadataMatching;

namespace API.Extensions;

public static class IHasKPlusMetadataExtensions
{

    public static bool HasSetKPlusMetadata(this IHasKPlusMetadata hasKPlusMetadata, MetadataSettingField field)
    {
        return hasKPlusMetadata.KPlusOverrides.Contains(field);
    }

    public static void AddKPlusOverride(this IHasKPlusMetadata hasKPlusMetadata, MetadataSettingField field)
    {
        if (hasKPlusMetadata.KPlusOverrides.Contains(field)) return;

        hasKPlusMetadata.KPlusOverrides.Add(field);
    }

}
