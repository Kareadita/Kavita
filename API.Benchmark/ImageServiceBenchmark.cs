using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using System.Drawing;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using NetVips;
using Image = NetVips.Image;



namespace API.Benchmark;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class ImageBenchmarks
{
	private readonly string _testDirectoryColorScapes = "C:/Users/User/Documents/GitHub/Kavita/API.Tests/Services/Test Data/ImageService/ColorScapes";
	
	private List<List<Vector3>> allRgbPixels;

    [GlobalSetup]
    public void Setup()
    {
       allRgbPixels = new List<List<Vector3>>();

		var imageFiles = Directory.GetFiles(_testDirectoryColorScapes, "*.*")
			.Where(file => !file.EndsWith("html"))
			.Where(file => !file.Contains("_output") && !file.Contains("_baseline"))
			.ToList();

		foreach (var imagePath in imageFiles)
		{
			using var image = Image.NewFromFile(imagePath);
			// Resize the image to speed up processing
			var resizedImage = image.Resize(0.1);
			// Convert image to RGB array
			var pixels = resizedImage.WriteToMemory().ToArray();
			// Convert to list of Vector3 (RGB)
			var rgbPixels = new List<Vector3>();

			for (var i = 0; i < pixels.Length - 2; i += 3)
			{
				rgbPixels.Add(new Vector3(pixels[i], pixels[i + 1], pixels[i + 2]));
			}

			// Add the rgbPixels list to allRgbPixels
			allRgbPixels.Add(rgbPixels);
		}
	}
	
	[Benchmark]
    public void CalculateColorScape_original()
    {
        foreach (var rgbPixels in allRgbPixels)
        {
            Original_KMeansClustering(rgbPixels, 4);
		}
    }
	
	[Benchmark]
    public void CalculateColorScape_optimized()
    {
        foreach (var rgbPixels in allRgbPixels)
        {
            Services.ImageService.KMeansClustering(rgbPixels, 4);
		}
    }
	
	private static List<Vector3> Original_KMeansClustering(List<Vector3> points, int k, int maxIterations = 100)
    {
        var random = new Random();
        var centroids = points.OrderBy(x => random.Next()).Take(k).ToList();

        for (var i = 0; i < maxIterations; i++)
        {
            var clusters = new List<Vector3>[k];
            for (var j = 0; j < k; j++)
            {
                clusters[j] = [];
            }

            foreach (var point in points)
            {
                var nearestCentroidIndex = centroids
                    .Select((centroid, index) => new { Index = index, Distance = Vector3.DistanceSquared(centroid, point) })
                    .OrderBy(x => x.Distance)
                    .First().Index;
                clusters[nearestCentroidIndex].Add(point);
            }

            var newCentroids = clusters.Select(cluster =>
                cluster.Count != 0 ? new Vector3(
                    cluster.Average(p => p.X),
                    cluster.Average(p => p.Y),
                    cluster.Average(p => p.Z)
                ) : Vector3.Zero
            ).ToList();

            if (centroids.SequenceEqual(newCentroids))
                break;

            centroids = newCentroids;
        }

        return centroids;
    }
	
}