using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Import;

namespace AgGradeDesigner.Core.Tests.Haul
{
    public sealed class HaulScrubFrameDiagnosticsTests
    {
        [Fact]
        public void Generate_TheShop2_IncludesGuidanceAndRoutePixelsOnFrameZero
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            HaulData Data = HaulEngineService.Generate(Project, new HaulSettings());

            IReadOnlyList<HaulBuilder.GuidancePoint> Guidance = Data.ResolveGuidancePoints();
            Assert.True(Guidance.Count >= 2, $"Expected guidance points, got {Guidance.Count}");

            int PassOneCount = Guidance.Count(Point => Point.PassNo == 1);
            Assert.True(PassOneCount >= 2, $"Expected pass-1 guidance, got {PassOneCount}");

            HaulBuilder.AgdField Field = BuildAgdField(Project);
            byte[] Frame = HaulScrubFrameBuilder.BuildFramePng(Data, Field, 0);

            AssertRouteColoredPixels(Frame);
        }

        [Fact]
        public void BuildFramePng_TheShop2_DifferentRouteIndicesProduceDifferentFrames
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            HaulData Data = HaulEngineService.Generate(Project, new HaulSettings());
            Assert.True(Data.RouteStepCount >= 3, $"Expected multiple routes, got {Data.RouteStepCount}.");

            HaulBuilder.AgdField Field = BuildAgdField(Project);
            byte[] Frame0 = HaulScrubFrameBuilder.BuildFramePng(Data, Field, 0);
            byte[] Frame1 = HaulScrubFrameBuilder.BuildFramePng(Data, Field, 1);
            byte[] Frame2 = HaulScrubFrameBuilder.BuildFramePng(Data, Field, 2);

            Assert.False(Frame0.AsSpan().SequenceEqual(Frame1));
            Assert.False(Frame1.AsSpan().SequenceEqual(Frame2));
            AssertBaseMapHasCutFillColors(Frame1);
        }

        [Fact]
        public void BuildFieldBoundaryPng_TheShop2_MatchesExteriorDataBoundary
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            HaulData Data = HaulEngineService.Generate(Project, new HaulSettings());
            HaulBuilder.AgdField Field = BuildAgdField(Project);
            HaulBuilder.Grid Grid = Data.ResolveGrid(Field);
            int PixelsPerBin = Data.Payload.PixelsPerBin;

            byte[]? Boundary = HaulScrubFrameBuilder.BuildFieldBoundaryPng(Project, Data, Field);

            Assert.NotNull(Boundary);

            HaulBoundaryAlignmentAnalyzer.AlignmentMetrics Metrics =
                HaulBoundaryAlignmentAnalyzer.AnalyzeExteriorValidBoundary(Grid, Boundary, PixelsPerBin);

            Assert.True(
                Metrics.StrokePixelCount >= HaulBoundaryAlignmentAnalyzer.MIN_STROKE_PIXEL_COUNT,
                $"Expected a visible boundary stroke, got {Metrics.Describe()}.");
            Assert.True(
                Metrics.EdgeAlignmentFraction >= HaulBoundaryAlignmentAnalyzer.MIN_EXTERIOR_EDGE_ALIGNMENT_FRACTION,
                $"Boundary stroke must follow the outer data edge: {Metrics.Describe()}.");
            Assert.True(
                Metrics.StrokeOnEdgeFraction >= HaulBoundaryAlignmentAnalyzer.MIN_STROKE_ON_EXTERIOR_EDGE_FRACTION,
                $"Boundary stroke must sit on the outer data perimeter: {Metrics.Describe()}.");
            HaulBoundaryAlignmentAnalyzer.AssertNotGridRectangle(Boundary);
        }

        private static void AssertBaseMapHasCutFillColors
            (
            byte[] PngBytes
            )
        {
            using SkiaSharp.SKBitmap Bitmap = SkiaSharp.SKBitmap.Decode(PngBytes)
                ?? throw new InvalidOperationException("Failed to decode frame PNG.");

            int Colored = 0;
            for (int Y = 0; Y < Bitmap.Height; Y++)
            {
                for (int X = 0; X < Bitmap.Width; X++)
                {
                    SkiaSharp.SKColor Color = Bitmap.GetPixel(X, Y);
                    if (Color.Alpha < 200)
                        continue;

                    bool NearWhite = Color.Red > 240 && Color.Green > 240 && Color.Blue > 240;
                    if (!NearWhite)
                        Colored++;
                }
            }

            Assert.True(Colored > 1000, $"Expected leveled cut/fill base map pixels, got {Colored}.");
        }

        private static HaulBuilder.AgdField BuildAgdField
            (
            ProjectDocument Project
            )
        {
            string TempAgd = Path.Combine(Path.GetTempPath(), $"aggrade-haul-test-{Guid.NewGuid():N}.agd");
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

        private static void AssertRouteColoredPixels
            (
            byte[] PngBytes
            )
        {
            using SkiaSharp.SKBitmap Bitmap = SkiaSharp.SKBitmap.Decode(PngBytes)
                ?? throw new InvalidOperationException("Failed to decode frame PNG.");

            int Red = 0;
            int Blue = 0;
            int Black = 0;
            for (int Y = 0; Y < Bitmap.Height; Y++)
            {
                for (int X = 0; X < Bitmap.Width; X++)
                {
                    SkiaSharp.SKColor Color = Bitmap.GetPixel(X, Y);
                    if (Color.Red > 200 && Color.Green < 80 && Color.Blue < 80)
                        Red++;
                    if (Color.Blue > 200 && Color.Red < 80 && Color.Green < 120)
                        Blue++;
                    if (Color.Red < 40 && Color.Green < 40 && Color.Blue < 40 && Color.Alpha > 200)
                        Black++;
                }
            }

            Assert.True(Red > 0, $"Expected red cut segments, got {Red} red pixels.");
            Assert.True(Blue > 0, $"Expected blue fill segments, got {Blue} blue pixels.");
            Assert.True(Black > 0, $"Expected black travel track, got {Black} black pixels.");
        }
    }
}
