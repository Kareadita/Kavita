using System.Collections.Generic;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Parser;
using Xunit;

namespace API.Tests.Extensions
{
    public class ParserInfoListExtensions
    {
        [Fact]
        public void HasInfoTest()
        {
            var info = new ParserInfo()
            {
                Chapters = "0-6",
                Edition = "",
                Format = MangaFormat.Archive,
                FullFilePath = @"E:\Manga\Cynthia the Mission\Cynthia The Mission - c000-006 (v06) [Desudesu&Brolen].zip",
                Filename = "Cynthia The Mission - c000-006 (v06) [Desudesu&Brolen].zip",
                IsSpecial = false,
                Title = "Cynthia The Mission - c000-006 (v06) [Desudesu&Brolen]",
                Series = "cynthiathemission",
                Volumes = "6"
            };

            var infos = new ParserInfo[]
            {
                info
            };

            var chapters = new List<Chapter>()
            {
                new Chapter()
                {
                    Id = 0,
                    IsSpecial = false,
                    Number = "0",
                    Pages = 199,
                    Range = "0-6",
                    Title = "0-6",
                    Volume = null,
                    Files = new List<MangaFile>()
                    {
                        new MangaFile()
                        {
                            FilePath = @"E:\Manga\Cynthia the Mission\Cynthia The Mission - c000-006 (v06) [Desudesu&Brolen].zip",
                            Chapter = null,
                            Format = MangaFormat.Archive,
                            Id = 0,
                            Pages = 199
                        }
                        
                    }
                }
            };


            Assert.True(infos.HasInfo(chapters[0]));
        }
    }
}