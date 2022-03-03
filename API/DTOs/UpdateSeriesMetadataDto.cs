using System.Collections.Generic;
using API.DTOs.CollectionTags;

namespace API.DTOs
{
    public class UpdateSeriesMetadataDto
    {
        public SeriesMetadataDto SeriesMetadata { get; set; }
        public ICollection<CollectionTagDto> CollectionTags { get; set; }
        /// <summary>
        /// User has asked to forcibly unlock the field
        /// </summary>
        public bool UnlockAgeRating { get; set; }
        public bool UnlockPublicationStatus { get; set; }
        public bool UnlockGenres { get; set; }
        public bool UnlockTags { get; set; }
        public bool UnlockWriter { get; set; }
        public bool UnlockCharacter { get; set; }
        public bool UnlockColorist { get; set; }
        public bool UnlockEditor { get; set; }
        public bool UnlockInker { get; set; }
        public bool UnlockLetterer { get; set; }
        public bool UnlockPenciller { get; set; }
        public bool UnlockPublisher { get; set; }
        public bool UnlockTranslator { get; set; }
        public bool UnlockCoverArtist { get; set; }
        public bool UnlockName { get; set; }
        public bool UnlockSortName { get; set; }
        public bool UnlockSummary { get; set; }
    }
}
