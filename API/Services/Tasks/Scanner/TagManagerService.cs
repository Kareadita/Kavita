using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers.Builders;

namespace API.Services.Tasks.Scanner;
#nullable enable

public interface ITagManagerService
{
    /// <summary>
    /// Should be called once before any usage
    /// </summary>
    /// <returns></returns>
    Task Prime();
    /// <summary>
    /// Should be called after all work is done, will free up memory
    /// </summary>
    /// <returns></returns>
    void Reset();

    Task<Genre?> GetGenre(string genre);
    Task<Tag?> GetTag(string tag);
    Task<Person?> GetPerson(string name, PersonRole role);
    Task<CollectionTag?> GetCollectionTag(string name);
}

/// <summary>
/// This is responsible for handling existing and new tags during the scan. When a new tag doesn't exist, it will create it.
/// This is Thread Safe.
/// </summary>
public class TagManagerService : ITagManagerService
{
    private readonly IUnitOfWork _unitOfWork;
    private Dictionary<string, Genre> _genres;
    private Dictionary<string, Tag> _tags;
    private Dictionary<string, Person> _people;
    private Dictionary<string, CollectionTag> _collectionTags;

    private readonly SemaphoreSlim _genreSemaphore = new SemaphoreSlim(1, 1);
    private readonly SemaphoreSlim _tagSemaphore = new SemaphoreSlim(1, 1);
    private readonly SemaphoreSlim _personSemaphore = new SemaphoreSlim(1, 1);
    private readonly SemaphoreSlim _collectionTagSemaphore = new SemaphoreSlim(1, 1);

    public TagManagerService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
        Reset();

    }

    public void Reset()
    {
        _genres = new Dictionary<string, Genre>();
        _tags = new Dictionary<string, Tag>();
        _people = new Dictionary<string, Person>();
        _collectionTags = new Dictionary<string, CollectionTag>();
    }

    public async Task Prime()
    {
        _genres = (await _unitOfWork.GenreRepository.GetAllGenresAsync()).ToDictionary(t => t.NormalizedTitle);
        _tags = (await _unitOfWork.TagRepository.GetAllTagsAsync()).ToDictionary(t => t.NormalizedTitle);
        _people = (await _unitOfWork.PersonRepository.GetAllPeople()).ToDictionary(GetPersonKey);
        _collectionTags = (await _unitOfWork.CollectionTagRepository.GetAllTagsAsync(CollectionTagIncludes.SeriesMetadata))
            .ToDictionary(t => t.NormalizedTitle);

    }

    /// <summary>
    /// Gets the Genre entity for the given string. If one doesn't exist, one will be created and committed.
    /// </summary>
    /// <param name="genre"></param>
    /// <returns></returns>
    public async Task<Genre?> GetGenre(string genre)
    {
        if (string.IsNullOrEmpty(genre)) return null;

        await _genreSemaphore.WaitAsync();
        try
        {
            if (_genres.TryGetValue(genre.ToNormalized(), out var result))
            {
                return result;
            }

            // We need to create a new Genre
            result = new GenreBuilder(genre).Build();
            _unitOfWork.GenreRepository.Attach(result);
            await _unitOfWork.CommitAsync();
            _genres.Add(result.NormalizedTitle, result);
            return result;
        }
        finally
        {
            _genreSemaphore.Release();
        }
    }

    /// <summary>
    /// Gets the Tag entity for the given string. If one doesn't exist, one will be created and committed.
    /// </summary>
    /// <param name="tag"></param>
    /// <returns></returns>
    public async Task<Tag?> GetTag(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return null;

        await _tagSemaphore.WaitAsync();
        try
        {
            if (_tags.TryGetValue(tag.ToNormalized(), out var result))
            {
                return result;
            }

            // We need to create a new Genre
            result = new TagBuilder(tag).Build();
            _unitOfWork.TagRepository.Attach(result);
            await _unitOfWork.CommitAsync();
            _tags.Add(result.NormalizedTitle, result);
            return result;
        }
        finally
        {
            _tagSemaphore.Release();
        }
    }

    /// <summary>
    /// Gets the Person entity for the given string and role. If one doesn't exist, one will be created and committed.
    /// </summary>
    /// <param name="name">Person Name</param>
    /// <param name="role"></param>
    /// <returns></returns>
    public async Task<Person?> GetPerson(string name, PersonRole role)
    {
        if (string.IsNullOrEmpty(name)) return null;

        await _personSemaphore.WaitAsync();
        try
        {
            var key = GetPersonKey(name.ToNormalized(), role);
            if (_people.TryGetValue(key, out var result))
            {
                return result;
            }

            // We need to create a new Genre
            result = new PersonBuilder(name, role).Build();
            _unitOfWork.PersonRepository.Attach(result);
            await _unitOfWork.CommitAsync();
            _people.Add(key, result);
            return result;
        }
        finally
        {
            _personSemaphore.Release();
        }
    }

    private static string GetPersonKey(string normalizedName, PersonRole role)
    {
        return normalizedName + "_" + role;
    }

    private static string GetPersonKey(Person p)
    {
        return GetPersonKey(p.NormalizedName, p.Role);
    }

    /// <summary>
    /// Gets the CollectionTag entity for the given string. If one doesn't exist, one will be created and committed.
    /// </summary>
    /// <param name="tag"></param>
    /// <returns></returns>
    public async Task<CollectionTag?> GetCollectionTag(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return null;

        await _collectionTagSemaphore.WaitAsync();
        try
        {
            if (_collectionTags.TryGetValue(tag.ToNormalized(), out var result))
            {
                return result;
            }

            // We need to create a new Genre
            result = new CollectionTagBuilder(tag).Build();
            _unitOfWork.CollectionTagRepository.Add(result);
            await _unitOfWork.CommitAsync();
            _collectionTags.Add(result.NormalizedTitle, result);
            return result;
        }
        finally
        {
            _collectionTagSemaphore.Release();
        }
    }
}
