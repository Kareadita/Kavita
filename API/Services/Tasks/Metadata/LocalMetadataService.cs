using System.Collections.Generic;
using System.Threading.Tasks;
using API.Comparators;
using API.Data;
using API.Entities;
using API.Interfaces;
using API.Interfaces.Services;
using API.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks.Metadata
{
    public class LocalMetadataService : ILocalMetadataService
    {
        private readonly IArchiveService _archiveService;
        private readonly ILogger<LocalMetadataService> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IBookService _bookService;
        private readonly IHubContext<MessageHub> _messageHub;
        private readonly ChapterSortComparerZeroFirst _chapterSortComparerForInChapterSorting = new ChapterSortComparerZeroFirst();
        private IDictionary<string, Person> _persons;

        public LocalMetadataService(IArchiveService archiveService, ILogger<LocalMetadataService> logger,
            IUnitOfWork unitOfWork, IBookService bookService, IHubContext<MessageHub> messageHub)
        {
            _archiveService = archiveService;
            _logger = logger;
            _unitOfWork = unitOfWork;
            _bookService = bookService;
            _messageHub = messageHub;
        }


        public async Task RefreshMetadataForSeries(Series series, bool forceUpdate)
        {
            if (series == null) return;

            var chapterIds = await _unitOfWork.SeriesRepository.GetChapterIdWithSeriesIdForSeriesAsync(new []{series.Id});
            var chapterMetadatas =
                await _unitOfWork.ChapterMetadataRepository.GetMetadataForChapterIds(chapterIds[series.Id]);

            series.Volumes ??= new List<Volume>();
            foreach (var volume in series.Volumes)
            {
                foreach (var chapter in volume.Chapters)
                {
                    if (!chapterMetadatas.ContainsKey(chapter.Id) || chapterMetadatas[chapter.Id].Count == 0)
                    {
                        var metadata = DbFactory.ChapterMetadata(chapter.Id);
                        _unitOfWork.ChapterMetadataRepository.Attach(metadata);
                        chapterMetadatas[chapter.Id].Add(metadata);
                    }
                    //chapterUpdated = UpdateMetadata(chapter, chapterMetadatas[chapter.Id][0], forceUpdate);
                }

                //volumeUpdated = UpdateMetadata(volume, chapterUpdated || forceUpdate);
            }



            //return UpdateSeriesMetadata(series, forceUpdate) || madeUpdate;
        }
    }
}
