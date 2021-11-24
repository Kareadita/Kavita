using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs.Metadata;
using API.Entities;

namespace API.Interfaces.Repositories
{
    public interface IChapterMetadataRepository
    {
        void Attach(ChapterMetadata metadata);
        void Update(ChapterMetadata metadata);
        Task<ChapterMetadata> GetMetadataForChapter(int chapterId);

        Task<IDictionary<int, IList<ChapterMetadata>>> GetMetadataForChapterIds(IList<int> chapterIds);
        Task<ChapterMetadataDto> GetMetadataDtoForChapter(int chapterId);
    }
}
