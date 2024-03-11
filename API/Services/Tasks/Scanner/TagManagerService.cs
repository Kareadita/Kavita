using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.Data;
using API.Entities;
using API.Extensions;
using API.Helpers.Builders;

namespace API.Services.Tasks.Scanner;

public interface ITagManagerService
{
    /// <summary>
    /// Should be called once before any usage
    /// </summary>
    /// <returns></returns>
    Task Prime();

    Task<Genre?> GetGenre(string genre);
}

/// <summary>
/// This is responsible for handling existing and new tags during the scan. When a new tag doesn't exist, it will create it.
/// This is Thread Safe.
/// </summary>
public class TagManagerService : ITagManagerService
{
    private readonly IUnitOfWork _unitOfWork;
    private Dictionary<string, Genre> _genres;

    private readonly SemaphoreSlim _genreSemaphore = new SemaphoreSlim(1, 1);

    public TagManagerService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;

        _genres = new Dictionary<string, Genre>();
    }

    public async Task Prime()
    {
        _genres = (await _unitOfWork.GenreRepository.GetAllGenresAsync()).ToDictionary(t => t.NormalizedTitle);
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
            return result;
        }
        finally
        {
            _genreSemaphore.Release();
        }
    }
}
