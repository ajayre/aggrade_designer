using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Tests.TestFixtures;

using SkiaSharp;

namespace AgGradeDesigner.Core.Tests.Performance
{
    /// <summary>
    /// Guards 2D/3D view-mode switch preparation against unnecessary surface rebuilds and re-rendering.
    /// </summary>
    public sealed class ViewModeSwitchPerformanceTests
    {
        /// <summary>Viewport width in pixels used by view-mode switch benchmarks.</summary>
        private const int ViewportWidth = 1280;

        /// <summary>Viewport height in pixels used by view-mode switch benchmarks.</summary>
        private const int ViewportHeight = 720;

        /// <summary>
        /// A warm surface should not be rebuilt when toggling view mode.
        /// </summary>
        [Fact]
        public void NeedsSurfaceRebuild_WhenSurfaceIsWarm_ReturnsFalse
            (
            )
        {
            Assert.False(ViewerFrameCacheKey.NeedsSurfaceRebuild(SurfaceDirty: false, SampleWidth: 256));
        }

        /// <summary>
        /// Repeated cache-key lookup for an unchanged view should stay well under interactive budgets.
        /// </summary>
        [Fact]
        public void CacheKeyLookup_RepeatedSwitchToSameView_CompletesWithinBudget
            (
            )
        {
            ViewerFrameState State = CreateSampleFrameState(ViewMode.TwoD);
            string ExpectedKey = ViewerFrameCacheKey.Build(State);

            double MedianMs = ImportBenchmark.MedianMilliseconds(() =>
            {
                string Key = ViewerFrameCacheKey.Build(State);
                Assert.Equal(ExpectedKey, Key);
            });

            Assert.True(MedianMs < 1.0,
                $"Cache-key lookup too slow: {MedianMs:F3} ms (budget 1 ms).");
        }

        /// <summary>
        /// Simulates toggling 2D → 3D → 2D with warm caches: no surface rebuild on the return trip.
        /// </summary>
        [Fact]
        public void ViewModeToggle_WarmSurface_SecondSwitchSkipsSurfaceRebuild
            (
            )
        {
            FieldSurfaceSampler Surface = BuildWarmSurface();
            Dictionary<ViewMode, string> CacheKeys = new Dictionary<ViewMode, string>();

            double MedianMs = ImportBenchmark.MedianMilliseconds(() =>
            {
                PrepareModeSwitch(Surface, ViewMode.TwoD, CacheKeys, out bool RebuiltSurface);
                Assert.False(RebuiltSurface);

                PrepareModeSwitch(Surface, ViewMode.ThreeD, CacheKeys, out RebuiltSurface);
                Assert.False(RebuiltSurface);

                PrepareModeSwitch(Surface, ViewMode.TwoD, CacheKeys, out RebuiltSurface);
                Assert.False(RebuiltSurface);
            });

            Assert.True(MedianMs < 250.0,
                $"Warm view-mode toggle too slow: {MedianMs:F1} ms (budget 250 ms).");
        }

        /// <summary>
        /// First render of each mode on FakeField_01 should stay within a generous cold-start budget.
        /// </summary>
        [Fact]
        public void ViewModeToggle_ColdFrames_FirstVisitToEachModeWithinBudget
            (
            )
        {
            FieldSurfaceSampler Surface = BuildWarmSurface();
            Dictionary<ViewMode, string> CacheKeys = new Dictionary<ViewMode, string>();

            double MedianMs = ImportBenchmark.MedianMilliseconds(() =>
            {
                CacheKeys.Clear();
                PrepareModeSwitch(Surface, ViewMode.TwoD, CacheKeys, out _);
                PrepareModeSwitch(Surface, ViewMode.ThreeD, CacheKeys, out _);
            });

            Assert.True(MedianMs < 4000.0,
                $"Cold 2D+3D frame preparation too slow: {MedianMs:F1} ms (budget 4000 ms).");
        }

        /// <summary>
        /// After both modes are cached, round-trip switching should be much faster than cold rendering.
        /// </summary>
        [Fact]
        public void ViewModeToggle_WarmCaches_RoundTripIsSignificantlyFasterThanCold
            (
            )
        {
            FieldSurfaceSampler Surface = BuildWarmSurface();
            Dictionary<ViewMode, string> CacheKeys = new Dictionary<ViewMode, string>();

            double ColdMs = ImportBenchmark.MedianMilliseconds(
                () =>
                {
                    CacheKeys.Clear();
                    PrepareModeSwitch(Surface, ViewMode.TwoD, CacheKeys, out _, PrewarmCaches: false);
                    PrepareModeSwitch(Surface, ViewMode.ThreeD, CacheKeys, out _, PrewarmCaches: false);
                },
                WarmupRuns: 1,
                MeasuredRuns: 3);

            double WarmMs = ImportBenchmark.MedianMilliseconds(
                () =>
                {
                    PrepareModeSwitch(Surface, ViewMode.TwoD, CacheKeys, out _, PrewarmCaches: true);
                    PrepareModeSwitch(Surface, ViewMode.ThreeD, CacheKeys, out _, PrewarmCaches: true);
                    PrepareModeSwitch(Surface, ViewMode.TwoD, CacheKeys, out _, PrewarmCaches: true);
                    PrepareModeSwitch(Surface, ViewMode.ThreeD, CacheKeys, out _, PrewarmCaches: true);
                },
                WarmupRuns: 2,
                MeasuredRuns: 5);

            Assert.True(ColdMs < 8000.0,
                $"Cold 2D+3D frame preparation too slow: {ColdMs:F1} ms (budget 8000 ms).");

            Assert.True(WarmMs < 50.0,
                $"Warm cached round-trip too slow: {WarmMs:F1} ms (budget 50 ms).");

            Assert.True(WarmMs < ColdMs,
                $"Warm toggle ({WarmMs:F1} ms) should beat cold ({ColdMs:F1} ms).");
        }

        /// <summary>
        /// Contour precomputation should complete quickly once display caches are warm.
        /// </summary>
        [Fact]
        public void PrewarmDisplayCaches_SecondContourFetch_IsNearInstant
            (
            )
        {
            FieldSurfaceSampler Surface = BuildWarmSurface();
            DisplaySettings Display = new DisplaySettings { Contours = true };

            Surface.PrewarmDisplayCaches(FieldContentView.Existing, Display);
            Surface.ComputeScalarRange(FieldContentView.Existing, out double VMin, out double VMax);

            double MedianMs = ImportBenchmark.MedianMilliseconds(() =>
            {
                Surface.GetContourPolylinesByLevel(FieldContentView.Existing, VMin, VMax);
            });

            Assert.True(MedianMs < 5.0,
                $"Cached contour lookup too slow: {MedianMs:F2} ms (budget 5 ms).");
        }

        static private FieldSurfaceSampler BuildWarmSurface
            (
            )
        {
            ProjectDocument Project = CreateFakeFieldProject();
            FieldSurfaceSampler Surface = new FieldSurfaceSampler();
            Surface.BuildFromProject(Project);
            return Surface;
        }

        static private ProjectDocument CreateFakeFieldProject
            (
            )
        {
            AgdImportResult Result = AgdService.Import(TestDataPaths.FakeField01Agd);
            return new ProjectDocument
            {
                Survey = Result.Survey,
                DesignParameters = Result.Parameters,
                DesignGrid = Result.Grid,
                SurveySourceFormat = SurveySourceFormat.Agd
            };
        }

        static private ViewerFrameState CreateSampleFrameState
            (
            ViewMode Mode
            )
        {
            return new ViewerFrameState
            {
                ViewMode = Mode,
                Width = ViewportWidth,
                Height = ViewportHeight,
                ContentView = FieldContentView.Existing,
                Display = new DisplaySettings(),
                ProjectKey = TestDataPaths.FakeField01Agd,
                CentreLat = 40.0,
                CentreLon = -93.5,
                ZoomFactor = 1.0,
                OrbitHeadingDeg = 45.0,
                OrbitPitchDeg = 30.0,
                OrbitDistanceM = 800.0,
                OrbitCentreLat = 40.0,
                OrbitCentreLon = -93.5
            };
        }

        /// <summary>
        /// Mirrors the viewer prepare path for a single mode switch using in-memory frame caches.
        /// </summary>
        static private void PrepareModeSwitch
            (
            FieldSurfaceSampler Surface,
            ViewMode Mode,
            Dictionary<ViewMode, string> CacheKeys,
            out bool RebuiltSurface,
            bool PrewarmCaches = true
            )
        {
            RebuiltSurface = false;
            if (ViewerFrameCacheKey.NeedsSurfaceRebuild(SurfaceDirty: false, Surface.SampleWidth))
            {
                RebuiltSurface = true;
                Surface.BuildFromProject(CreateFakeFieldProject());
            }

            ViewerFrameState State = CreateSampleFrameState(Mode);
            if (PrewarmCaches)
                Surface.PrewarmDisplayCaches(State.ContentView, State.Display);

            string CacheKey = ViewerFrameCacheKey.Build(State);
            if (CacheKeys.TryGetValue(Mode, out string? ExistingKey) && (ExistingKey == CacheKey))
                return;

            RenderFrame(Surface, State);
            CacheKeys[Mode] = CacheKey;
        }

        static private void RenderFrame
            (
            FieldSurfaceSampler Surface,
            ViewerFrameState State
            )
        {
            using SKBitmap FrameBitmap = new SKBitmap(State.Width, State.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using SKCanvas Canvas = new SKCanvas(FrameBitmap);
            SKRect Viewport = SKRect.Create(0, 0, State.Width, State.Height);
            Canvas.Clear(SKColors.White);

            if (State.ViewMode == ViewMode.TwoD)
            {
                Surface.ComputeScalarRange(State.ContentView, out double VMin, out double VMax);
                SKBitmap ColorMap = Surface.GetPlanViewColorMap(State.ContentView, VMin, VMax, ApplyHillshade: false);
                Canvas.DrawBitmap(ColorMap, Viewport);
            }
            else
            {
                Surface.ComputeScalarRange(State.ContentView, out _, out _);
                int Width = Surface.SampleWidth;
                int Height = Surface.SampleHeight;
                for (int Row = 0; Row < Height; Row += 8)
                {
                    for (int Col = 0; Col < Width; Col += 8)
                    {
                        int Index = (Row * Width) + Col;
                        if ((Index < Surface.Valid.Length) && Surface.Valid[Index])
                            Canvas.DrawPoint(Col, Row, SKColors.Green);
                    }
                }
            }

            Canvas.Flush();
        }
    }
}
