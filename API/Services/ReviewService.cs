using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using API.DTOs.SeriesDetail;
using HtmlAgilityPack;


namespace API.Services;

public static class ReviewService
{
    private const int BodyTextLimit = 175;
    public static IEnumerable<UserReviewDto> SelectSpectrumOfReviews(IList<UserReviewDto> reviews)
    {
        IList<UserReviewDto> externalReviews;
        var totalReviews = reviews.Count;

        if (totalReviews > 10)
        {
            var stepSize = Math.Max((totalReviews - 4) / 8, 1);

            var selectedReviews = new List<UserReviewDto>()
            {
                reviews[0],
                reviews[1],
            };
            for (var i = 2; i < totalReviews - 2; i += stepSize)
            {
                selectedReviews.Add(reviews[i]);

                if (selectedReviews.Count >= 8)
                    break;
            }

            selectedReviews.Add(reviews[totalReviews - 2]);
            selectedReviews.Add(reviews[totalReviews - 1]);

            externalReviews = selectedReviews;
        }
        else
        {
            externalReviews = reviews;
        }

        return externalReviews.OrderByDescending(r => r.Score);
    }

    public static string GetCharacters(string body)
    {
        if (string.IsNullOrEmpty(body)) return body;

        var doc = new HtmlDocument();
        doc.LoadHtml(body);

        var textNodes = doc.DocumentNode.SelectNodes("//text()[not(parent::script)]");
        if (textNodes == null) return string.Empty;
        var plainText =  string.Join(" ", textNodes
            .Select(node => node.InnerText)
            .Where(s => !s.Equals("\n")));

        // Clean any leftover markdown out
        plainText = Regex.Replace(plainText, @"[_*\[\]~]", string.Empty);
        plainText = Regex.Replace(plainText, @"img\d*\((.*?)\)", string.Empty);
        plainText = Regex.Replace(plainText, @"~~~(.*?)~~~", "$1");
        plainText = Regex.Replace(plainText, @"\+{3}(.*?)\+{3}", "$1");
        plainText = Regex.Replace(plainText, @"~~(.*?)~~", "$1");
        plainText = Regex.Replace(plainText, @"__(.*?)__", "$1");
        plainText = Regex.Replace(plainText, @"#\s(.*?)", "$1");

        // Just strip symbols
        plainText = Regex.Replace(plainText, @"[_*\[\]~]", string.Empty);
        plainText = Regex.Replace(plainText, @"img\d*\((.*?)\)", string.Empty);
        plainText = Regex.Replace(plainText, @"~~~", string.Empty);
        plainText = Regex.Replace(plainText, @"\+", string.Empty);
        plainText = Regex.Replace(plainText, @"~~", string.Empty);
        plainText = Regex.Replace(plainText, @"__", string.Empty);

        // Take the first 100 characters
        plainText = plainText.Length > 100 ? plainText.Substring(0, BodyTextLimit) : plainText;

        return plainText + "…";
    }

}
