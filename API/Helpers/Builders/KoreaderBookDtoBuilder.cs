using System.Security.Cryptography;
using System.Text;
using API.DTOs.Koreader;

namespace API.Helpers.Builders;

public class KoreaderBookDtoBuilder : IEntityBuilder<KoreaderBookDto>
{
    private readonly KoreaderBookDto _dto;
    public KoreaderBookDto Build() => _dto;

    public KoreaderBookDtoBuilder(string documentHash)
    {
        _dto = new KoreaderBookDto()
        {
            Document = documentHash,
            Device = "Kavita"
        };
    }

    public KoreaderBookDtoBuilder WithDocument(string documentHash)
    {
        _dto.Document = documentHash;
        return this;
    }

    public KoreaderBookDtoBuilder WithProgress(string progress)
    {
        _dto.Progress = progress;
        return this;
    }

    public KoreaderBookDtoBuilder WithPercentage(int? pageNum, int pages)
    {
        _dto.Percentage = (pageNum ?? 0) / (float) pages;
        return this;
    }

    public KoreaderBookDtoBuilder WithDeviceId(string installId, int userId)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(installId + userId));
        _dto.Device_id = hash.ToString();
        return this;
    }
}
