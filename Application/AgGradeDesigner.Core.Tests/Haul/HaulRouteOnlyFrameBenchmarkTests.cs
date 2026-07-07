using System.Diagnostics;
using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Import;

namespace AgGradeDesigner.Core.Tests.Haul
{
    public sealed class HaulRouteOnlyFrameBenchmarkTests
    {
        [Fact]
        public void BuildRouteSprite_TheShop2_IsSmallFastAndTransparent
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            HaulData Data = HaulEngineService.Generate(Project, new HaulSettings());
            HaulBuilder.AgdField Field = BuildAgdField(Project);

            int MidIndex = Data.RouteStepCount / 2;
            HaulScrubFrame Warmup = HaulScrubFrameBuilder.BuildRouteSprite(Data, Field, MidIndex);

            double SingleMs = MeasureAverageMs(
                () => HaulScrubFrameBuilder.BuildRouteSprite(Data, Field, MidIndex),
                iterations: 5);
            HaulScrubFrame Sprite = HaulScrubFrameBuilder.BuildRouteSprite(Data, Field, MidIndex);

            int ParallelCount = Math.Min(8, Environment.ProcessorCount);
            int StartIndex = Math.Max(1, MidIndex);
            int[] Indices = Enumerable.Range(StartIndex, ParallelCount).ToArray();
            Stopwatch ParallelSw = Stopwatch.StartNew();
            Parallel.ForEach(Indices, Index => HaulScrubFrameBuilder.BuildRouteSprite(Data, Field, Index));
            ParallelSw.Stop();
            double ParallelAvgMs = ParallelSw.ElapsedMilliseconds / (double)ParallelCount;

            Assert.True(Sprite.HasPlacement);
            Assert.Equal(HaulScrubFrameKind.Partial, Sprite.Kind);
            AssertRouteSpriteTransparency(Sprite.PngBytes);
            Assert.True(Sprite.PngBytes.Length < 2_000_000, $"Expected cropped PNG, got {Sprite.PngBytes.Length / 1024}KB.");

            using SkiaSharp.SKBitmap Bitmap = SkiaSharp.SKBitmap.Decode(Sprite.PngBytes)
                ?? throw new InvalidOperationException("Failed to decode route sprite PNG.");
            Assert.True(Bitmap.Width < 8900, $"Expected cropped width, got {Bitmap.Width}.");
            Assert.True(Bitmap.Height < 8700, $"Expected cropped height, got {Bitmap.Height}.");

            Console.WriteLine(
                $"TheShop2 routes={Data.RouteStepCount} warmup={Warmup.PngBytes.Length / 1024}KB sprite={Sprite.PngBytes.Length / 1024}KB bitmap={Bitmap.Width}x{Bitmap.Height}");
            Console.WriteLine($"Route sprite index={MidIndex}: avg={SingleMs:F1}ms");
            Console.WriteLine(
                $"Route sprite parallel {ParallelCount} frames [{StartIndex}..{StartIndex + ParallelCount - 1}]: " +
                $"wall={ParallelSw.ElapsedMilliseconds}ms avg={ParallelAvgMs:F1}ms/frame");

            Assert.True(SingleMs < 1000, $"Route sprite render took {SingleMs:F1}ms; expected under 1s on TheShop2.");
        }

        private static double MeasureAverageMs
            (
            Func<HaulScrubFrame> Work,
            int iterations
            )
        {
            long TotalMs = 0;
            for (int i = 0; i < iterations; i++)
            {
                Stopwatch Sw = Stopwatch.StartNew();
                _ = Work();
                Sw.Stop();
                TotalMs += Sw.ElapsedMilliseconds;
            }

            return TotalMs / (double)iterations;
        }

        private static void AssertRouteSpriteTransparency
            (
            byte[] PngBytes
            )
        {
            using SkiaSharp.SKBitmap Bitmap = SkiaSharp.SKBitmap.Decode(PngBytes)
                ?? throw new InvalidOperationException("Failed to decode route sprite PNG.");

            int SampleStep = 20;
            int TransparentSamples = 0;
            int RouteSamples = 0;

            for (int Y = 0; Y < Bitmap.Height; Y += SampleStep)
            {
                for (int X = 0; X < Bitmap.Width; X += SampleStep)
                {
                    SkiaSharp.SKColor Color = Bitmap.GetPixel(X, Y);
                    if (Color.Alpha < 20)
                    {
                        TransparentSamples++;
                        continue;
                    }

                    if (IsRoutePixel(Color))
                        RouteSamples++;
                }
            }

            Assert.True(TransparentSamples > 0, "Expected transparent background pixels.");
            Assert.True(RouteSamples > 0, "Expected visible cut/fill/track pixels.");
        }

        private static bool IsRoutePixel
            (
            SkiaSharp.SKColor Color
            )
        {
            if (Color.Red > 200 && Color.Green < 80 && Color.Blue < 80)
                return true;

            if (Color.Blue > 200 && Color.Red < 80 && Color.Green < 120)
                return true;

            return Color.Red < 40 && Color.Green < 40 && Color.Blue < 40;
        }

        private static HaulBuilder.AgdField BuildAgdField
            (
            ProjectDocument Project
            )
        {
            string TempAgd = Path.Combine(Path.GetTempPath(), $"aggrade-haul-bench-{Guid.NewGuid():N}.agd");
            try
            {
                AgdService.Export(
                    TempAgd,
                    Project.Survey!,
                    Project.DesignGrid!,
                    Project.DesignParameters ?? new DesignParameters());
                return HaulBuilder.Agd.Parse(TempAgd);
            }
            finally
            {
                if (File.Exists(TempAgd))
                    File.Delete(TempAgd);
            }
        }
    }
}
