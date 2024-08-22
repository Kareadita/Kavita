using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
using API.Services;
using API.Services.Tasks.Scanner.Parser;
using API.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nager.ArticleNumber;

namespace API.Controllers;

public class ChapterController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizationService _localizationService;
    private readonly IEventHub _eventHub;

    public ChapterController(IUnitOfWork unitOfWork, ILocalizationService localizationService, IEventHub eventHub)
    {
        _unitOfWork = unitOfWork;
        _localizationService = localizationService;
        _eventHub = eventHub;
    }

    /// <summary>
    /// Gets a single chapter
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [HttpGet]
    public async Task<ActionResult<ChapterDto>> GetChapter(int chapterId)
    {
        var chapter =
            await _unitOfWork.ChapterRepository.GetChapterDtoAsync(chapterId,
                ChapterIncludes.People | ChapterIncludes.Files);

        return Ok(chapter);
    }

    /// <summary>
    /// Removes a Chapter
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpDelete]
    public async Task<ActionResult<bool>> DeleteChapter(int chapterId)
    {
        var chapter = await _unitOfWork.ChapterRepository.GetChapterAsync(chapterId);
        if (chapter == null)
            return BadRequest(_localizationService.Translate(User.GetUserId(), "chapter-doesnt-exist"));

        var vol = (await _unitOfWork.VolumeRepository.GetVolumeAsync(chapter.VolumeId))!;
        _unitOfWork.ChapterRepository.Remove(chapter);

        if (await _unitOfWork.CommitAsync())
        {
            await _eventHub.SendMessageAsync(MessageFactory.ChapterRemoved, MessageFactory.ChapterRemovedEvent(chapter.Id, vol.SeriesId), false);
            return Ok(true);
        }

        return Ok(false);
    }

    /// <summary>
    /// Update chapter metadata
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("update")]
    public async Task<ActionResult> UpdateChapterMetadata(UpdateChapterDto dto)
    {
        var chapter = await _unitOfWork.ChapterRepository.GetChapterAsync(dto.Id, ChapterIncludes.People | ChapterIncludes.Genres | ChapterIncludes.Tags);
        if (chapter == null)
            return BadRequest(_localizationService.Translate(User.GetUserId(), "chapter-doesnt-exist"));

        if (chapter.AgeRating != dto.AgeRating)
        {
            chapter.AgeRating = dto.AgeRating;
        }

        dto.Summary ??= string.Empty;

        if (chapter.Summary != dto.Summary.Trim())
        {
            chapter.Summary = dto.Summary.Trim();
        }

        if (chapter.Language != dto.Language)
        {
            chapter.Language = dto.Language ?? string.Empty;
        }

        if (chapter.SortOrder.IsNot(dto.SortOrder))
        {
            chapter.SortOrder = dto.SortOrder; // TODO: Figure out validation
        }

        if (chapter.TitleName != dto.TitleName)
        {
            chapter.TitleName = dto.TitleName;
        }

        if (chapter.ReleaseDate != dto.ReleaseDate)
        {
            chapter.ReleaseDate = dto.ReleaseDate;
        }

        if (!string.IsNullOrEmpty(dto.ISBN) && ArticleNumberHelper.IsValidIsbn10(dto.ISBN) ||
            ArticleNumberHelper.IsValidIsbn13(dto.ISBN))
        {
            chapter.ISBN = dto.ISBN;
        }

        if (string.IsNullOrEmpty(dto.WebLinks))
        {
            chapter.WebLinks = string.Empty;
        } else
        {
            chapter.WebLinks = string.Join(',', dto.WebLinks
                    .Split(',')
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Select(s => s.Trim())!
            );
        }


        #region Genres
        if (dto.Genres != null &&
            dto.Genres.Count != 0)
        {
            var allGenres = (await _unitOfWork.GenreRepository.GetAllGenresByNamesAsync(dto.Genres.Select(t => Parser.Normalize(t.Title)))).ToList();
            chapter.Genres ??= new List<Genre>();
            GenreHelper.UpdateGenreList(dto.Genres, chapter, allGenres, genre =>
            {
                chapter.Genres.Add(genre);
            }, () => chapter.GenresLocked = true);
        }
        #endregion

        #region Tags
        if (dto.Tags is {Count: > 0})
        {
            var allTags = (await _unitOfWork.TagRepository
                    .GetAllTagsByNameAsync(dto.Tags.Select(t => Parser.Normalize(t.Title))))
                .ToList();
            chapter.Tags ??= new List<Tag>();
            TagHelper.UpdateTagList(dto.Tags, chapter, allTags, tag =>
            {
                chapter.Tags.Add(tag);
            }, () => chapter.TagsLocked = true);
        }
        #endregion

        #region People
        if (PersonHelper.HasAnyPeople(dto))
        {
            void HandleAddPerson(Person person)
            {
                PersonHelper.AddPersonIfNotExists(chapter.People, person);
            }

            chapter.People ??= new List<Person>();
            var allWriters = await _unitOfWork.PersonRepository.GetAllPeopleByRoleAndNames(PersonRole.Writer,
                dto.Writers.Select(p => Parser.Normalize(p.Name)));
            PersonHelper.UpdatePeopleList(PersonRole.Writer, dto.Writers, chapter, allWriters.AsReadOnly(),
                HandleAddPerson,  () => chapter.WriterLocked = true);

            var allCharacters = await _unitOfWork.PersonRepository.GetAllPeopleByRoleAndNames(PersonRole.Character,
                dto.Characters.Select(p => Parser.Normalize(p.Name)));
            PersonHelper.UpdatePeopleList(PersonRole.Character, dto.Characters, chapter, allCharacters.AsReadOnly(),
                HandleAddPerson,  () => chapter.CharacterLocked = true);

            var allColorists = await _unitOfWork.PersonRepository.GetAllPeopleByRoleAndNames(PersonRole.Colorist,
                dto.Colorists.Select(p => Parser.Normalize(p.Name)));
            PersonHelper.UpdatePeopleList(PersonRole.Colorist, dto.Colorists, chapter, allColorists.AsReadOnly(),
                HandleAddPerson,  () => chapter.ColoristLocked = true);

            var allEditors = await _unitOfWork.PersonRepository.GetAllPeopleByRoleAndNames(PersonRole.Editor,
                dto.Editors.Select(p => Parser.Normalize(p.Name)));
            PersonHelper.UpdatePeopleList(PersonRole.Editor, dto.Editors, chapter, allEditors.AsReadOnly(),
                HandleAddPerson,  () => chapter.EditorLocked = true);

            var allInkers = await _unitOfWork.PersonRepository.GetAllPeopleByRoleAndNames(PersonRole.Inker,
                dto.Inkers.Select(p => Parser.Normalize(p.Name)));
            PersonHelper.UpdatePeopleList(PersonRole.Inker, dto.Inkers, chapter, allInkers.AsReadOnly(),
                HandleAddPerson,  () => chapter.InkerLocked = true);

            var allLetterers = await _unitOfWork.PersonRepository.GetAllPeopleByRoleAndNames(PersonRole.Letterer,
                dto.Letterers.Select(p => Parser.Normalize(p.Name)));
            PersonHelper.UpdatePeopleList(PersonRole.Letterer, dto.Letterers, chapter, allLetterers.AsReadOnly(),
                HandleAddPerson,  () => chapter.LettererLocked = true);

            var allPencillers = await _unitOfWork.PersonRepository.GetAllPeopleByRoleAndNames(PersonRole.Penciller,
                dto.Pencillers.Select(p => Parser.Normalize(p.Name)));
            PersonHelper.UpdatePeopleList(PersonRole.Penciller, dto.Pencillers, chapter, allPencillers.AsReadOnly(),
                HandleAddPerson,  () => chapter.PencillerLocked = true);

            var allPublishers = await _unitOfWork.PersonRepository.GetAllPeopleByRoleAndNames(PersonRole.Publisher,
                dto.Publishers.Select(p => Parser.Normalize(p.Name)));
            PersonHelper.UpdatePeopleList(PersonRole.Publisher, dto.Publishers, chapter, allPublishers.AsReadOnly(),
                HandleAddPerson,  () => chapter.PublisherLocked = true);

            var allImprints = await _unitOfWork.PersonRepository.GetAllPeopleByRoleAndNames(PersonRole.Imprint,
                dto.Imprints.Select(p => Parser.Normalize(p.Name)));
            PersonHelper.UpdatePeopleList(PersonRole.Imprint, dto.Imprints, chapter, allImprints.AsReadOnly(),
                HandleAddPerson,  () => chapter.ImprintLocked = true);

            var allTeams = await _unitOfWork.PersonRepository.GetAllPeopleByRoleAndNames(PersonRole.Team,
                dto.Imprints.Select(p => Parser.Normalize(p.Name)));
            PersonHelper.UpdatePeopleList(PersonRole.Team, dto.Teams, chapter, allTeams.AsReadOnly(),
                HandleAddPerson,  () => chapter.TeamLocked = true);

            var allLocations = await _unitOfWork.PersonRepository.GetAllPeopleByRoleAndNames(PersonRole.Location,
                dto.Imprints.Select(p => Parser.Normalize(p.Name)));
            PersonHelper.UpdatePeopleList(PersonRole.Location, dto.Locations, chapter, allLocations.AsReadOnly(),
                HandleAddPerson,  () => chapter.LocationLocked = true);

            var allTranslators = await _unitOfWork.PersonRepository.GetAllPeopleByRoleAndNames(PersonRole.Translator,
                dto.Translators.Select(p => Parser.Normalize(p.Name)));
            PersonHelper.UpdatePeopleList(PersonRole.Translator, dto.Translators, chapter, allTranslators.AsReadOnly(),
                HandleAddPerson,  () => chapter.TranslatorLocked = true);

            var allCoverArtists = await _unitOfWork.PersonRepository.GetAllPeopleByRoleAndNames(PersonRole.CoverArtist,
                dto.CoverArtists.Select(p => Parser.Normalize(p.Name)));
            PersonHelper.UpdatePeopleList(PersonRole.CoverArtist, dto.CoverArtists, chapter, allCoverArtists.AsReadOnly(),
                HandleAddPerson,  () => chapter.CoverArtistLocked = true);
        }
        #endregion

        #region Locks
        chapter.AgeRatingLocked = dto.AgeRatingLocked;
        chapter.LanguageLocked = dto.LanguageLocked;
        chapter.TitleNameLocked = dto.TitleNameLocked;
        chapter.SortOrderLocked = dto.SortOrderLocked;
        chapter.GenresLocked = dto.GenresLocked;
        chapter.TagsLocked = dto.TagsLocked;
        chapter.CharacterLocked = dto.CharacterLocked;
        chapter.ColoristLocked = dto.ColoristLocked;
        chapter.EditorLocked = dto.EditorLocked;
        chapter.InkerLocked = dto.InkerLocked;
        chapter.ImprintLocked = dto.ImprintLocked;
        chapter.LettererLocked = dto.LettererLocked;
        chapter.PencillerLocked = dto.PencillerLocked;
        chapter.PublisherLocked = dto.PublisherLocked;
        chapter.TranslatorLocked = dto.TranslatorLocked;
        chapter.CoverArtistLocked = dto.CoverArtistLocked;
        chapter.WriterLocked = dto.WriterLocked;
        chapter.SummaryLocked = dto.SummaryLocked;
        chapter.ISBNLocked = dto.ISBNLocked;
        chapter.ReleaseDateLocked = dto.ReleaseDateLocked;
        #endregion


        _unitOfWork.ChapterRepository.Update(chapter);

        if (!_unitOfWork.HasChanges())
        {
            return Ok();
        }

        // TODO: Emit a ChapterMetadataUpdate out

        await _unitOfWork.CommitAsync();


        return Ok();
    }



}
