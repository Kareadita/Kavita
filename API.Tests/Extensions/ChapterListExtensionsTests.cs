using System.Collections.Generic;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Parser;
using Xunit;

namespace API.Tests.Extensions
{
    public class ChapterListExtensionsTests
    {
        private Chapter CreateChapter(string range, string number, MangaFile file, bool isSpecial)
        {
            return new Chapter()
            {
                Range = range,
                Number = number,
                Files = new List<MangaFile>() {file},
                IsSpecial = isSpecial
            };
        }

        private MangaFile CreateFile(string file, MangaFormat format)
        {
            return new MangaFile()
            {
                FilePath = file,
                Format = format
            };
        }
        
        [Fact]
        public void GetAnyChapterByRange_Test_ShouldBeNull()
        {
            var info = new ParserInfo()
            {
                Chapters = "0",
                Edition = "",
                Format = MangaFormat.Archive,
                FullFilePath = "/manga/darker than black.cbz",
                Filename = "darker than black.cbz",
                IsSpecial = false,
                Series = "darker than black",
                Title = "darker than black",
                Volumes = "0"
            };

            var chapterList = new List<Chapter>()
            {
                CreateChapter("darker than black - Some special", "0", CreateFile("/manga/darker than black.cbz", MangaFormat.Archive), true)
            };

            var actualChapter = chapterList.GetAnyChapterByRange(info);
            
            Assert.NotEqual(chapterList[0], actualChapter);
            
        }
        
        [Fact]
        public void GetAnyChapterByRange_Test_ShouldBeNotNull()
        {
            var info = new ParserInfo()
            {
                Chapters = "0",
                Edition = "",
                Format = MangaFormat.Archive,
                FullFilePath = "/manga/darker than black.cbz",
                Filename = "darker than black.cbz",
                IsSpecial = true,
                Series = "darker than black",
                Title = "darker than black",
                Volumes = "0"
            };

            var chapterList = new List<Chapter>()
            {
                CreateChapter("darker than black", "0", CreateFile("/manga/darker than black.cbz", MangaFormat.Archive), true)
            };

            var actualChapter = chapterList.GetAnyChapterByRange(info);
            
            Assert.Equal(chapterList[0], actualChapter);
            
        }
    }
}