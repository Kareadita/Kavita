using System.Collections.Generic;
using API.Entities.MetadataMatching;

namespace API.Entities.Interfaces;

public interface IHasKPlusMetadata
{
    /// <summary>
    /// Tracks which metadata has been set by K+
    /// </summary>
    public IList<MetadataSettingField> KPlusOverrides { get; set; }
}
