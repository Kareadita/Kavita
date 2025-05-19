using API.Entities;
using API.Extensions;

namespace API.Helpers.Builders;

public class AppUserReadingProfileBuilder
{
    private readonly AppUserReadingProfile _profile;

    public AppUserReadingProfile Build() => _profile;

    public AppUserReadingProfileBuilder(int userId)
    {
        _profile = new AppUserReadingProfile
        {
            UserId = userId,
            Series = [],
            Libraries = [],
        };
    }

    public AppUserReadingProfileBuilder WithSeries(Series series)
    {
        _profile.Series.Add(new SeriesReadingProfile
        {
            Series = series,
            AppUserId = _profile.UserId,
        });
        return this;
    }

    public AppUserReadingProfileBuilder WithLibrary(Library library)
    {
        _profile.Libraries.Add(new LibraryReadingProfile
        {
            Library = library,
            AppUserId = _profile.UserId,
        });
        return this;
    }

    public AppUserReadingProfileBuilder WithImplicit(bool b)
    {
        _profile.Implicit = b;
        return this;
    }

    public AppUserReadingProfileBuilder WithName(string name)
    {
        _profile.Name = name;
        _profile.NormalizedName = name.ToNormalized();
        return this;
    }


}
