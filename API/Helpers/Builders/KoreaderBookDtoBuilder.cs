using System;
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
            document = documentHash,
            device = "Kavita"
        };
    }

    public KoreaderBookDtoBuilder WithDocument(string documentHash)
    {
        _dto.document = documentHash;
        return this;
    }

    public KoreaderBookDtoBuilder WithProgress(string progress)
    {
        _dto.progress = progress;
        return this;
    }

    public KoreaderBookDtoBuilder WithPercentage(int? pageNum, int pages)
    {
        _dto.percentage = (pageNum ?? 0) / (float) pages;
        return this;
    }

    public KoreaderBookDtoBuilder WithDeviceId(string installId, int userId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(installId + userId));
        _dto.device_id = Convert.ToHexString(hash);
        return this;
    }

    public KoreaderBookDtoBuilder WithTimestamp(DateTime? lastModifiedUtc)
    {
       var time = lastModifiedUtc ?? new DateTime(0, DateTimeKind.Utc);
       _dto.timestamp = new DateTimeOffset(time).ToUnixTimeSeconds();
        return this;
    }
}
