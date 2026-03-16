using System.Collections.Generic;
using Kavita.Models.Entities.MetadataMatching;

namespace Kavita.Models.Entities.Interfaces;

public interface IHasKPlusMetadata
{
    /// <summary>
    /// Tracks which metadata has been set by K+
    /// </summary>
    public IList<MetadataSettingField> KPlusOverrides { get; set; }
}
