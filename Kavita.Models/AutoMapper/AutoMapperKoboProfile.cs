using System.Linq;
using AutoMapper;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.Kobo;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Person;
using Kavita.Models.Entities.User;
using Kavita.Models.Extensions;

namespace Kavita.Models.AutoMapper;

public class AutoMapperKoboProfile: Profile
{
    public AutoMapperKoboProfile()
    {
        CreateMap<ChapterPeople, KoboPublisherDto>()
            .ForMemberSimple(d => d.Name, p => p.Person.Name);

        CreateMap<ChapterPeople, KoboContributorRoleDto>()
            .ForMemberSimple(d => d.Name, p => p.Person.Name)
            .ForMemberSimple(d => d.Role, p => p.Role.ToString());

        CreateMap<Chapter, KoboSeriesDto>()
            .ForMemberSimple(d => d.Id, c => c.Volume.SeriesId.ToString())
            .ForMemberSimple(d => d.Name, c => c.Volume.Series.Name)
            // TODO: Revisit number mapping
            .ForMemberSimple(d => d.Number,
                c => c.MinNumber == ParserConstants.DefaultChapterNumber
                    ? c.Volume.MinNumber.ToString()
                    : c.MinNumber.ToString())
            .ForMemberSimple(d => d.NumberFloat,
                c => c.MinNumber == ParserConstants.DefaultChapterNumber ? c.Volume.MinNumber : c.MinNumber);

        CreateMap<Chapter, KoboBookMetadataDto>()
            .ForMemberSimple(d => d.Title, c => c.TitleName)
            .ForMemberSimple(d => d.LibraryType, c => c.Volume.Series.Library.Type)
            .ForMemberSimple(d => d.WorkId, c => c.Id.ToString())
            .ForMemberSimple(d => d.CoverImageId, c => c.Id.ToString())
            .ForMemberSimple(d => d.RevisionId, c => c.Id.ToString())
            .ForMemberSimple(d => d.CrossRevisionId, c => c.Id.ToString())
            .ForMemberSimple(d => d.EntitlementId, c => c.Id.ToString())
            .ForMember(d => d.Contributors, opt => opt.MapFrom(c => c.People
                .Where(p => p.Role == PersonRole.Writer)
                .Select(p => p.Person.Name)))
            .ForMemberSimple(d => d.Description, c => c.Summary)
            // Kobo has an aneurysm if there is no language present, default to English
            .ForMemberSimple(d => d.Language, c => c.Language ?? "en")
            .ForMember(d => d.Publisher, opt => opt.MapFrom(c => c.People
                .Where(p => p.Role == PersonRole.Publisher)
                .OrderBy(p => p.PersonId)
                .FirstOrDefault()))
            .ForMemberSimple(d => d.FileSize, c => c.Files.Sum(f => f.Bytes))
            .ForMemberSimple(d => d.FileCount, c => c.Files.Count)
            .ForMemberSimple(d => d.Format, c => c.Volume.Series.Format)
            .ForMember(d => d.Series, opt => opt.MapFrom(c => c));

        CreateMap<Chapter, KoboBookRecommendationDto>()
            .ForMemberSimple(d => d.WorkId, c => c.Id.ToString())
            .ForMemberSimple(d => d.KoboId, c => c.Id.ToString())
            .ForMemberSimple(d => d.Id, c => c.Id.ToString())
            .ForMemberSimple(d => d.CrossRevisionId, c => c.Id.ToString())
            .ForMemberSimple(d => d.ImageId, c => c.Id.ToString())
            .ForMemberSimple(d => d.Title, c => c.TitleName)
            .ForMemberSimple(d => d.Description, c => c.Summary)
            .ForMemberSimple(d => d.Language, c => c.Language ?? "en")
            .ForMember(d => d.PublisherName, opt => opt.MapFrom(c => c.People
                .Where(p => p.Role == PersonRole.Publisher)
                .OrderBy(p => p.PersonId)
                .Select(p => p.Person.Name)
                .FirstOrDefault()))
            .ForMember(d => d.Contributors, opt => opt.MapFrom(c => string.Join(", ",
                c.People
                    .Where(p => p.Role == PersonRole.Writer)
                    .OrderBy(p => p.PersonId)
                    .Select(p => p.Person.Name))))
            .ForMember(d => d.ContributorRoles, opt => opt.MapFrom(c => c.People
                .Where(p => p.Role == PersonRole.Writer
                            || p.Role == PersonRole.Translator
                            || p.Role == PersonRole.Editor)))
            .ForMember(d => d.Series, opt => opt.MapFrom(c => c))
            ;

        CreateMap<Chapter, KoboBookDetailsDto>()
            .ForMemberSimple(d => d.KoboId, c => c.Id.ToString())
            .ForMemberSimple(d => d.WorkId, c => c.Id.ToString())
            .ForMemberSimple(d => d.CrossRevisionId, c => c.Id.ToString())
            .ForMemberSimple(d => d.ImageId, c => c.Id.ToString())
            .ForMemberSimple(d => d.Title, c => c.TitleName)
            .ForMemberSimple(d => d.Description, c => c.Summary)
            .ForMemberSimple(d => d.Language, c => c.Language ?? "en")
            .ForMemberSimple(d => d.Locale, c => new KoboLocaleDto
            {
                LanguageCode = ToIso6392((c.Language ?? "en"))
            })
            .ForMemberSimple(d => d.ISBN, c => c.ISBN ?? string.Empty)
            .ForMember(d => d.Contributors, opt => opt.MapFrom(c => string.Join(", ",
                c.People
                    .Where(p => p.Role == PersonRole.Writer)
                    .OrderBy(p => p.PersonId)
                    .Select(p => p.Person.Name))))
            .ForMember(d => d.ContributorRoles, opt => opt.MapFrom(c => c.People
                .Where(p => p.Role == PersonRole.Writer
                            || p.Role == PersonRole.Translator
                            || p.Role == PersonRole.Editor)))
            .ForMemberSimple(d => d.PublisherName, c => c.People
                .Where(p => p.Role == PersonRole.Publisher)
                .OrderBy(p => p.PersonId)
                .Select(p => p.Person.Name)
                .FirstOrDefault() ?? string.Empty)
            .ForMember(d => d.SeriesName, opt => opt.MapFrom(c => c.Volume.Series.Name))
            .ForMember(d => d.SeriesNumber, opt => opt.MapFrom(c =>
                c.MinNumber == ParserConstants.DefaultChapterNumber
                    ? c.Volume.MinNumber.ToString()
                    : c.MinNumber.ToString()));

        CreateMap<AppUserChapterRating, KoboReviewDto>()
            .ForMemberSimple(d => d.AuthorDisplayName, r => r.AppUser.UserName)
            .ForMemberSimple(d => d.Body, r => r.Review)
            .ForMemberSimple(d => d.CreationDate, r => r.Created)
            .ForMemberSimple(d => d.CrossRevisionId, r => r.ChapterId.ToString())
            .ForMemberSimple(d => d.KoboId, r => r.Id.ToString())
            .ForMemberSimple(d => d.ProductId, r => r.ChapterId.ToString())
            .ForMemberSimple(d => d.PublicationId, r => r.ChapterId.ToString())
            .ForMember(d => d.Rating, opt => opt.MapFrom(c => (int) c.Rating))
            .ForMemberSimple(d => d.RevisionId, r => r.ChapterId.ToString())
            .ForMemberSimple(d => d.Title, r => r.AppUser.UserName + "'s review")
            ;
    }

    /// <summary>
    /// Maps a 2-letter language code to a 3-letter ISO 639-2 code.
    /// Used to populate <see cref="KoboLocaleDto.LanguageCode"/>.
    /// </summary>
    private static string ToIso6392(string twoLetter) => twoLetter.ToLowerInvariant() switch
    {
        "fr" => "fre",
        "en" => "eng",
        "de" => "ger",
        "es" => "spa",
        "it" => "ita",
        "pt" => "por",
        "ja" => "jpn",
        "ko" => "kor",
        "zh" => "chi",
        "ru" => "rus",
        _ => twoLetter,
    };
}
