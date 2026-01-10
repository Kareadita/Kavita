using API.Entities;
using API.Entities.Enums;
using API.Extensions;

namespace API.Helpers.Builders;

public class AppUserReadingProfileBuilder
{
    private readonly AppUserReadingProfile _profile;

    public AppUserReadingProfile Build() => _profile;

    /// <summary>
    /// The profile's kind will be <see cref="ReadingProfileKind.User"/> unless overwritten with <see cref="WithKind"/>
    /// </summary>
    /// <param name="userId"></param>
    public AppUserReadingProfileBuilder(int userId)
    {
        _profile = new AppUserReadingProfile
        {
            AppUserId = userId,
            Kind = ReadingProfileKind.User,
            SeriesIds = [],
            LibraryIds = [],
            DeviceIds = [],
        };
    }

    public AppUserReadingProfileBuilder WithSeries(Series series)
    {
        _profile.SeriesIds.Add(series.Id);
        return this;
    }

    public AppUserReadingProfileBuilder WithLibrary(Library library)
    {
        _profile.LibraryIds.Add(library.Id);
        return this;
    }

    public AppUserReadingProfileBuilder WithKind(ReadingProfileKind kind)
    {
        _profile.Kind = kind;
        return this;
    }

    public AppUserReadingProfileBuilder WithName(string name)
    {
        _profile.Name = name;
        _profile.NormalizedName = name.ToNormalized();
        return this;
    }

    public AppUserReadingProfileBuilder WithDeviceId(int deviceId)
    {
        if (_profile.DeviceIds.Contains(deviceId)) return this;
        _profile.DeviceIds.Add(deviceId);

        return this;
    }


}
