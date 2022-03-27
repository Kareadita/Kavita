using API.Entities.Enums;

namespace API.DTOs.Stats
{
    public class ServerInfoDto
    {
        public string InstallId { get; set; }
        public string Os { get; set; }
        public bool IsDocker { get; set; }
        public string DotnetVersion { get; set; }
        public string KavitaVersion { get; set; }
        public int NumOfCores { get; set; }
        public int NumberOfLibraries { get; set; }
        public bool HasBookmarks { get; set; }
        /// <summary>
        /// The site theme the install is using
        /// </summary>
        public string ActiveSiteTheme { get; set; }

        /// <summary>
        /// The reading mode the main user has as a preference
        /// </summary>
        public ReaderMode MangaReaderMode { get; set; }

        /// <summary>
        /// Number of users on the install
        /// </summary>
        public int NumberOfUsers { get; set; }

        /// <summary>
        /// Number of collections on the install
        /// </summary>
        public int NumberOfCollections { get; set; }

        /// <summary>
        /// Number of reading lists on the install (Sum of all users)
        /// </summary>
        public int NumberOfReadingLists { get; set; }

        /// <summary>
        /// Is OPDS enabled
        /// </summary>
        public bool OPDSEnabled { get; set; }

        /// <summary>
        /// Total number of files in the instance
        /// </summary>
        public int TotalFiles { get; set; }
    }
}
