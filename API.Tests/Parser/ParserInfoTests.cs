﻿using API.Entities.Enums;
using API.Parser;
using Xunit;

namespace API.Tests.Parser
{
    public class ParserInfoTests
    {
        [Fact]
        public void MergeFromTest()
        {
            var p1 = new ParserInfo()
            {
                Chapters = "0",
                Edition = "",
                Format = MangaFormat.Archive,
                FullFilePath = "/manga/darker than black.cbz",
                IsSpecial = false,
                Series = "darker than black",
                Title = "darker than black",
                Volumes = "0"
            };
            
            var p2 = new ParserInfo()
            {
                Chapters = "1",
                Edition = "",
                Format = MangaFormat.Archive,
                FullFilePath = "/manga/darker than black.cbz",
                IsSpecial = false,
                Series = "darker than black",
                Title = "Darker Than Black",
                Volumes = "0"
            };
            
            var expected = new ParserInfo()
            {
                Chapters = "1",
                Edition = "",
                Format = MangaFormat.Archive,
                FullFilePath = "/manga/darker than black.cbz",
                IsSpecial = false,
                Series = "darker than black",
                Title = "darker than black",
                Volumes = "0"
            };
            p1.Merge(p2);
            
            AssertSame(expected, p1);

        }
        
        [Fact]
        public void MergeFromTest2()
        {
            var p1 = new ParserInfo()
            {
                Chapters = "1",
                Edition = "",
                Format = MangaFormat.Archive,
                FullFilePath = "/manga/darker than black.cbz",
                IsSpecial = true,
                Series = "darker than black",
                Title = "darker than black",
                Volumes = "0"
            };
            
            var p2 = new ParserInfo()
            {
                Chapters = "0",
                Edition = "",
                Format = MangaFormat.Archive,
                FullFilePath = "/manga/darker than black.cbz",
                IsSpecial = false,
                Series = "darker than black",
                Title = "Darker Than Black",
                Volumes = "1"
            };
            
            var expected = new ParserInfo()
            {
                Chapters = "1",
                Edition = "",
                Format = MangaFormat.Archive,
                FullFilePath = "/manga/darker than black.cbz",
                IsSpecial = true,
                Series = "darker than black",
                Title = "darker than black",
                Volumes = "1"
            };
            p1.Merge(p2);

            AssertSame(expected, p1);

        }
        

        private void AssertSame(ParserInfo expected, ParserInfo actual)
        {
            Assert.Equal(expected.Chapters, actual.Chapters);
            Assert.Equal(expected.Volumes, actual.Volumes);
            Assert.Equal(expected.Edition, actual.Edition);
            Assert.Equal(expected.Filename, actual.Filename);
            Assert.Equal(expected.Format, actual.Format);
            Assert.Equal(expected.Series, actual.Series);
            Assert.Equal(expected.IsSpecial, actual.IsSpecial);
            Assert.Equal(expected.FullFilePath, actual.FullFilePath);
        }
    }
}