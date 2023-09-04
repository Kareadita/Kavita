using API.DTOs.Filtering.v2;
using API.Entities;

namespace API.Helpers.Builders;

public class SmartFilterBuilder : IEntityBuilder<AppUserSmartFilter>
{
    private AppUserSmartFilter _smartFilter;
    public AppUserSmartFilter Build() => _smartFilter;

    public SmartFilterBuilder(FilterV2Dto filter)
    {
        _smartFilter = new AppUserSmartFilter()
        {
            Name = filter.Name,
            Filter = SmartFilterHelper.Encode(filter)
        };
    }

    // public SmartFilterBuilder WithName(string name)
    // {
    //
    // }
}
