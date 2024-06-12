using System;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
    Task<Tuple<AppUserCollection?, bool>> GetCollectionTag(string? tag, AppUser userWithCollections);
}

/// <summary>
/// This is responsible for handling existing and new tags during the scan. When a new tag doesn't exist, it will create it.
/// This is Thread Safe.
/// </summary>
public class TagManagerService : ITagManagerService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TagManagerService> _logger;
    private Dictionary<string, Genre> _genres;
    private Dictionary<string, Tag> _tags;
    private Dictionary<string, Person> _people;
    private Dictionary<string, AppUserCollection> _collectionTags;

    private readonly SemaphoreSlim _genreSemaphore = new SemaphoreSlim(1, 1);
    private readonly SemaphoreSlim _tagSemaphore = new SemaphoreSlim(1, 1);
    private readonly SemaphoreSlim _personSemaphore = new SemaphoreSlim(1, 1);
    private readonly SemaphoreSlim _collectionTagSemaphore = new SemaphoreSlim(1, 1);

    public TagManagerService(IUnitOfWork unitOfWork, ILogger<TagManagerService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        Reset();

    }

    public void Reset()
    {
        _genres = [];
        _tags = [];
        _people = [];
        _collectionTags = [];
    }

    public async Task Prime()
    {
        _genres = (await _unitOfWork.GenreRepository.GetAllGenresAsync()).ToDictionary(t => t.NormalizedTitle);
        _tags = (await _unitOfWork.TagRepository.GetAllTagsAsync()).ToDictionary(t => t.NormalizedTitle);
        _people = (await _unitOfWork.PersonRepository.GetAllPeople())
            .GroupBy(GetPersonKey)
            .Select(g => g.First())
            .ToDictionary(GetPersonKey);
        var defaultAdmin = await _unitOfWork.UserRepository.GetDefaultAdminUser()!;
        _collectionTags = (await _unitOfWork.CollectionTagRepository.GetCollectionsForUserAsync(defaultAdmin.Id, CollectionIncludes.Series))
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
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "There was an exception when creating a new Tag. Scan again to get this included: {Tag}", tag);
            return null;
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
        catch (DbUpdateConcurrencyException ex)
        {
            foreach (var entry in ex.Entries)
            {
                if (entry.Entity is Person)
                {
                    var proposedValues = entry.CurrentValues;
                    var databaseValues = await entry.GetDatabaseValuesAsync();

                    foreach (var property in proposedValues.Properties)
                    {
                        var proposedValue = proposedValues[property];
                        var databaseValue = databaseValues[property];

                        // TODO: decide which value should be written to database
                        _logger.LogDebug(ex, "There was an exception when creating a new Person: {PersonName} ({Role})", name, role);
                        _logger.LogDebug("Property conflict, proposed: {Proposed} vs db: {Database}", proposedValue, databaseValue);
                        // proposedValues[property] = <value to be saved>;
                    }

                    // Refresh original values to bypass next concurrency check
                    entry.OriginalValues.SetValues(databaseValues);
                    //return (Person) entry.Entity;
                    return null;
                }
                // else
                // {
                //     throw new NotSupportedException(
                //         "Don't know how to handle concurrency conflicts for "
                //         + entry.Metadata.Name);
                // }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "There was an exception when creating a new Person. Scan again to get this included: {PersonName} ({Role})", name, role);
            return null;
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
    public async Task<Tuple<AppUserCollection?, bool>> GetCollectionTag(string? tag, AppUser userWithCollections)
    {
        if (string.IsNullOrEmpty(tag)) return Tuple.Create<AppUserCollection?, bool>(null, false);

        await _collectionTagSemaphore.WaitAsync();
        AppUserCollection? result;
        try
        {
            if (_collectionTags.TryGetValue(tag.ToNormalized(), out result))
            {
                return Tuple.Create<AppUserCollection?, bool>(result, false);
            }

            // We need to create a new Genre
            result = new AppUserCollectionBuilder(tag).Build();
            userWithCollections.Collections.Add(result);
            _unitOfWork.UserRepository.Update(userWithCollections);
            await _unitOfWork.CommitAsync();
            _collectionTags.Add(result.NormalizedTitle, result);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "There was an exception when creating a new Collection. Scan again to get this included: {Tag}", tag);
            return Tuple.Create<AppUserCollection?, bool>(null, false);
        }
        finally
        {
            _collectionTagSemaphore.Release();
        }
        return Tuple.Create<AppUserCollection?, bool>(result, true);
    }
}
