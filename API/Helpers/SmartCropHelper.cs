using System;
using System.Collections.Generic;

using System.Threading.Tasks;
using ImageMagick;


namespace API.Helpers
{
    /**
     * C# port of smartcrop.js
     * A javascript library implementing content aware image cropping
     *
     * Copyright (C) 2018 Jonas Wagner
     *
     * Permission is hereby granted, free of charge, to any person obtaining
     * a copy of this software and associated documentation files (the
     * "Software"), to deal in the Software without restriction, including
     * without limitation the rights to use, copy, modify, merge, publish,
     * distribute, sublicense, and/or sell copies of the Software, and to
     * permit persons to whom the Software is furnished to do so, subject to
     * the following conditions:
     *
     * The above copyright notice and this permission notice shall be
     * included in all copies or substantial portions of the Software.
     *
     * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
     * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
     * MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
     * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
     * LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
     * OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
     * WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
     */

    public static class SmartCropHelper
    {

        public static MagickGeometry SmartCrop(MagickImage inputImage, int width, int height)
        {
            SmartCropOptions options = new SmartCropOptions
            {
                Width = width,
                Height = height
            };
            CropResult result = SmartCrop(inputImage, options);
            return new MagickGeometry(result.TopCrop.X, result.TopCrop.Y, result.TopCrop.Width, result.TopCrop.Height) { FillArea = false };
        }


        public static CropResult SmartCrop(MagickImage inputImage, SmartCropOptions options = null)
        {
            if (options.Aspect != 0)
            {
                options.Width = (int)options.Aspect;
                options.Height = 1;
            }
            var scale = 1.0f;
            var prescale = 1.0f;

            // Open the image
            var image = (MagickImage)inputImage.Clone();
            if (options.Width != 0 && options.Height != 0)
            {
                scale = Math.Min(image.Width / options.Width, image.Height / options.Height);
                options.CropWidth = (int)(options.Width * scale);
                options.CropHeight = (int)(options.Height * scale);
                options.MinScale = Math.Min(options.MaxScale, Math.Max(1.0f / scale, options.MinScale));

                // Prescale if possible
                if (options.Prescale)
                {
                    prescale = Math.Min(Math.Max(256.0f / image.Width, 256.0f / image.Height), 1);
                    if (prescale < 1)
                    {
                        // Resample the image
                        image.Resize((int)(image.Width * prescale), (int)(image.Height * prescale));
                        options.CropWidth = (int)(options.CropWidth * prescale);
                        options.CropHeight = (int)(options.CropHeight * prescale);
                        foreach (Boost boost in options.Boosts)
                        {
                            boost.X = (float)Math.Floor(boost.X * prescale);
                            boost.Y = (float)Math.Floor(boost.Y * prescale);
                            boost.Width = (float)Math.Floor(boost.Width * prescale);
                            boost.Height = (float)Math.Floor(boost.Height * prescale);
                        }
                    }
                }
            }

            var data = new ImageData(image);
            var result = Analyse(options, data);

            // Adjust for prescale
            foreach (var crop in result.Crops)
            {
                crop.X = (int)(crop.X / prescale);
                crop.Y = (int)(crop.Y / prescale);
                crop.Width = (int)(crop.Width / prescale);
                crop.Height = (int)(crop.Height / prescale);
            }

            return result;
        }
        private static CropResult Analyse(SmartCropOptions options, ImageData input)
        {
            CropResult result = new CropResult();
            ImageData output = new ImageData(input.Width, input.Height);

            EdgeDetect(input, output);
            SkinDetect(options, input, output);
            SaturationDetect(options, input, output);
            ApplyBoosts(options, output);

            var scoreOutput = DownSample(output, options.ScoreDownSample);

            var topScore = double.NegativeInfinity;
            Crop topCrop = null;
            result.Crops = GenerateCrops(options, input.Width, input.Height);

            foreach (var crop in result.Crops)
            {
                crop.Score = Score(options, scoreOutput, crop);
                if (crop.Score.Total > topScore)
                {
                    topCrop = crop;
                    topScore = crop.Score.Total;
                }
            }

            result.TopCrop = topCrop;
            return result;
        }
        private static float Thirds(double x)
        {
            x = (((x - 1.0 / 3.0 + 1.0) % 2.0) * 0.5 - 0.5) * 16.0;
            return (float)Math.Max(1.0 - x * x, 0.0);
        }
        private static float Importance(SmartCropOptions options, Crop crop, double x, double y)
        {
            if (crop.X > x || x >= crop.X + crop.Width || crop.Y > y || y >= crop.Y + crop.Height)
            {
                return options.OutsideImportance;
            }

            x = (x - crop.X) / crop.Width;
            y = (y - crop.Y) / crop.Height;
            double px = Math.Abs(0.5 - x) * 2;
            double py = Math.Abs(0.5 - y) * 2;

            // Distance from edge
            double dx = Math.Max(px - 1.0 + options.EdgeRadius, 0);
            double dy = Math.Max(py - 1.0 + options.EdgeRadius, 0);
            double d = (dx * dx + dy * dy) * options.EdgeWeight;
            double s = 1.41 - Math.Sqrt(px * px + py * py);

            if (options.RuleOfThirds)
            {
                s += Math.Max(0, s + d + 0.5) * 1.2 * (Thirds(px) + Thirds(py));
            }

            return (float)(s + d);
        }
        private static ScoreResult Score(SmartCropOptions options, ImageData output, Crop crop)
        {


            var od = output.Data;
            var downSample = options.ScoreDownSample;
            var invDownSample = 1.0 / downSample;
            var outputHeightDownSample = output.Height * downSample;
            var outputWidthDownSample = output.Width * downSample;
            var outputWidth = output.Width;

            double rskin = 0;
            double rdetail = 0;
            double rsaturation = 0;
            double rboost = 0;
            for (var y = 0; y < outputHeightDownSample; y += downSample)
            {
                for (var x = 0; x < outputWidthDownSample; x += downSample)
                {
                    var p = (int)((Math.Floor(y * invDownSample) * outputWidth + Math.Floor(x * invDownSample)) * 4);
                    var i = Importance(options, crop, x, y);
                    var detail = od[p + 1] / 255.0;

                    rskin += (od[p] / 255.0) * (detail + options.SkinBias) * i;
                    rdetail += detail * i;
                    rsaturation += (od[p + 2] / 255.0) * (detail + options.SaturationBias) * i;
                    rboost += (od[p + 3] / 255.0) * i;
                }
            }

            return new ScoreResult
            {
                Detail = (float)rdetail,
                Saturation = (float)rsaturation,
                Skin = (float)rskin,
                Boost = (float)rboost,
                Total = (float)((rdetail * options.DetailWeight + rskin * options.SkinWeight +
                                 rsaturation * options.SaturationWeight +
                                 rboost * options.BoostWeight) /
                                (double)(crop.Width * crop.Height))
            };
        }
        private static void EdgeDetect(ImageData input, ImageData output)
        {
            // Get the pixel collection of both input and output images
            float[] inputPixels = input.Data;
            float[] outputPixels = output.Data;
            
            int width = input.Width;
            int height = input.Height;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int p = (y * width + x) * 4;
                    float lightness;

                    // Handle edge cases for pixels at the borders
                    if (x == 0 || x >= width - 1 || y == 0 || y >= height - 1)
                    {
                        lightness = Sample(inputPixels, p);
                    }
                    else
                    {
                        lightness = Sample(inputPixels, p) * 4 -
                                    Sample(inputPixels, p - width * 4) - // Above pixel
                                    Sample(inputPixels, p - 4) - // Left pixel
                                    Sample(inputPixels, p + 4) - // Right pixel
                                    Sample(inputPixels, p + width * 4);  // Below pixel
                    }

                    outputPixels[p + 1] = lightness;
                }
            }
        }
        private static List<Crop> GenerateCrops(SmartCropOptions options, int width, int height)
        {
            var results = new List<Crop>();
            var minDimension = Math.Min(width, height);
            var cropWidth = options.CropWidth != 0 ? options.CropWidth : minDimension;
            var cropHeight = options.CropHeight!=0 ? options.CropHeight : minDimension;

            for (var scale = options.MaxScale; scale >= options.MinScale; scale -= options.ScaleStep)
            {
                for (var y = 0; y + cropHeight * scale <= height; y += options.Step)
                {
                    for (var x = 0; x + cropWidth * scale <= width; x += options.Step)
                    {
                        results.Add(new Crop
                        {
                            X = x,
                            Y = y,
                            Width = (int)Math.Round(cropWidth * scale),
                            Height = (int)Math.Round(cropHeight * scale)
                        });
                    }
                }
            }

            return results;
        }
        private static void ApplyBoosts(SmartCropOptions options, ImageData output)
        {
            if (options.Boosts.Count==0) return;
            var od = output.Data;
            for (int i = 0; i < output.Width; i += 4)
            {
                od[i + 3] = 0;
            }
            foreach(Boost boost in options.Boosts)
            {
                ApplyBoost(boost, options, output);
            }
        }
        private static ImageData DownSample(ImageData input, int factor)
        {
            var idata = input.Data;
            var iwidth = input.Width;
            var width = (int)Math.Floor((double)input.Width / factor);
            var height = (int)Math.Floor((double)input.Height / factor);
            var output = new ImageData(width, height);
            var data = output.Data;
            var ifactor2 = 1.0 / (factor * factor);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var i = (y * width + x) * 4;

                    double r = 0, g = 0, b = 0, a = 0;
                    double mr = 0, mg = 0;

                    for (var v = 0; v < factor; v++)
                    {
                        for (var u = 0; u < factor; u++)
                        {
                            var j = ((y * factor + v) * iwidth + (x * factor + u)) * 4;
                            r += idata[j];
                            g += idata[j + 1];
                            b += idata[j + 2];
                            a += idata[j + 3];
                            mr = Math.Max(mr, idata[j]);
                            mg = Math.Max(mg, idata[j + 1]);
                        }
                    }

                    data[i] = (byte)(r * ifactor2 * 0.5 + mr * 0.5);
                    data[i + 1] = (byte)(g * ifactor2 * 0.7 + mg * 0.3);
                    data[i + 2] = (byte)(b * ifactor2);
                    data[i + 3] = (byte)(a * ifactor2);
                }
            }

            return output;
        }
        private static void ApplyBoost(Boost boost, SmartCropOptions options, ImageData output)
        {
            var od = output.Data;
            var w = output.Width;
            var x0 = (int)(boost.X);
            var x1 = (int)(boost.X + boost.Width);
            var y0 = (int)(boost.Y);
            var y1 = (int)(boost.Y + boost.Height);
            var weight = boost.Weight * 255;
            for (var y = y0; y < y1; y++)
            {
                for (var x = x0; x < x1; x++)
                {
                    var i = (y * w + x) * 4;
                    od[i + 3] += weight;
                }
            }
        }
        private static void SaturationDetect(SmartCropOptions options, ImageData i, ImageData o)
        {
            var id = i.Data;
            var od = o.Data;
            var w = i.Width;
            var h = i.Height;
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var p = (y * w + x) * 4;

                    var lightness = CIE(id[p], id[p + 1], id[p + 2]) / 255;
                    var sat = Saturation(id[p], id[p + 1], id[p + 2]);

                    var acceptableSaturation = sat > options.SaturationThreshold;
                    var acceptableLightness = lightness >= options.SaturationBrightnessMin && lightness <= options.SaturationBrightnessMax;
                    if (acceptableLightness && acceptableSaturation)
                    {
                        od[p + 2] = (sat - options.SaturationThreshold) * (255 / (1 - options.SaturationThreshold));
                    }
                    else
                    {
                        od[p + 2] = 0;
                    }
                }
            }
        }
        private static float Saturation(float r, float g, float b)
        {
            var maximum = Math.Max(r / 255f, Math.Max(g / 255f, b / 255f));
            var minimum = Math.Min(r / 255f, Math.Min(g / 255f, b / 255f));

            if (Math.Abs(maximum - minimum) < float.Epsilon)
            {
                return 0;
            }

            var l = (maximum + minimum) / 2;
            var d = maximum - minimum;

            return l > 0.5f ? d / (2.0f - maximum - minimum) : d / (maximum + minimum);
        }
        private static void  SkinDetect(SmartCropOptions options, ImageData i, ImageData o)
        {
            float[] id = i.Data;
            float[] od = o.Data;
            int w = i.Width;
            int h = i.Height;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int p = (y * w + x) * 4;
                    float lightness = CIE(id[p], id[p + 1], id[p + 2]) / 255;
                    float skin = SkinColor(options, id[p], id[p + 1], id[p + 2]);
                    bool isSkinColor = skin > options.SkinThreshold;
                    bool isSkinBrightness = lightness >= options.SkinBrightnessMin && lightness <= options.SkinBrightnessMax;
                    if (isSkinColor && isSkinBrightness)
                    {
                        od[p] = (skin - options.SkinThreshold) * (255 / (1 - options.SkinThreshold));
                    }
                    else
                    {
                        od[p] = 0;
                    }
                }
            }
        }
        private static float Sample(float[] data, int p)
        {
            return CIE(data[p], data[p + 1], data[p + 2]);
        }
        private static float CIE(float r, float g,  float b)
        {
            return 0.5126f * b + 0.7152f * g + 0.0722f * r;
        }
        private static float SkinColor(SmartCropOptions options, float r, float g, float b)
        {
            double mag = Math.Sqrt(r * r + g * g + b * b);
            double rd = r / mag - options.SkinColor[0];
            double gd = g / mag - options.SkinColor[1];
            double bd = b / mag - options.SkinColor[2];
            double d = Math.Sqrt(rd * rd + gd * gd + bd * bd);
            return (float)(1.0 - d);
        }
        public class SmartCropOptions
        {
            public int Width { get; set; } = 0;
            public int Height { get; set; } = 0;
            public float Aspect { get; set; } = 0;
            public int CropWidth { get; set; } = 0;
            public int CropHeight { get; set; } = 0;
            public float DetailWeight { get; set; } = 0.2f;
            public float[] SkinColor { get; set; } = new float[] { 0.78f, 0.57f, 0.44f };
            public float SkinBias { get; set; } = 0.01f;
            public float SkinBrightnessMin { get; set; } = 0.2f;
            public float SkinBrightnessMax { get; set; } = 1.0f;
            public float SkinThreshold { get; set; } = 0.8f;
            public float SkinWeight { get; set; } = 1.8f;
            public float SaturationBrightnessMin { get; set; } = 0.05f;
            public float SaturationBrightnessMax { get; set; } = 0.9f;
            public float SaturationThreshold { get; set; } = 0.4f;
            public float SaturationBias { get; set; } = 0.2f;
            public float SaturationWeight { get; set; } = 0.1f;
            public int ScoreDownSample { get; set; } = 8;
            public int Step { get; set; } = 8;
            public float ScaleStep { get; set; } = 0.1f;
            public float MinScale { get; set; } = 1.0f;
            public float MaxScale { get; set; } = 1.0f;
            public float EdgeRadius { get; set; } = 0.4f;
            public float EdgeWeight { get; set; } = -20.0f;
            public float OutsideImportance { get; set; } = -0.5f;
            public float BoostWeight { get; set; } = 100.0f;
            public bool RuleOfThirds { get; set; } = true;
            public bool Prescale { get; set; } = true;
            public List<Boost> Boosts { get; set; } = new List<Boost>();
        }
        public class CropResult
        {
            public List<Crop> Crops { get; set; }
            public Crop TopCrop { get; set; }
        }
        public class ScoreResult
        {
            public float Detail { get; set; }
            public float Saturation { get; set; }
            public float Skin { get; set; }
            public float Boost { get; set; }
            public float Total { get; set; }
        }
        public class Crop
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }

            public ScoreResult Score { get; set; }
        }
        public class Boost
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float Width { get; set; }
            public float Height { get; set; }
            public float Weight { get; set; }
        }
        public class ImageData
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public float[] Data { get; set; }

            public ImageData(int width, int height)
            {
                Width = width;
                Height = height;
                Data = new float[width * height * 4];
            }

            public ImageData(MagickImage image)
            {
                Width = image.Width;
                Height = image.Height;
                float scale = 1.0f / 256;
                if (image.ChannelCount == 4)
                {
                    Data = image.GetPixels().GetValues();
                    for (int x = 0; x < Data.Length; x++)
                    {
                        Data[x] *= scale;
                    }
                }
                else if (image.ChannelCount == 3)
                {
                    float[] temp = image.GetPixels().GetValues();
                    Data = new float[Width * Height * 4];
                    int oi = 0;
                    int ii = 0;
                    for(int y = 0; y < Height*Width; y++)
                    {

                        Data[oi++] = temp[ii++] * scale;
                        Data[oi++] = temp[ii++] * scale;
                        Data[oi++] = temp[ii++] * scale;
                        Data[oi++] = 255F;
                    }
                }
                else if (image.ChannelCount == 1)
                {
                    float[] temp = image.GetPixels().GetValues();
                    Data = new float[Width * Height * 4];
                    int oi = 0;
                    int ii = 0;
                    for (int y = 0; y < Height * Width; y++)
                    {
                        Data[oi++] = temp[ii++] * scale;
                        Data[oi++] = temp[oi-1];
                        Data[oi++] = temp[oi-1];
                        Data[oi++] = 255F;
                    }
                }
            }
        }
    }
}
