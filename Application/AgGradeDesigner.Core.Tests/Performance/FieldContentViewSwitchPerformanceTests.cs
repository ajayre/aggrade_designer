using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Tests.TestFixtures;

using SkiaSharp;

namespace AgGradeDesigner.Core.Tests.Performance
{
    /// <summary>
    /// Guards existing/proposed field-content switches against unnecessary cache rebuilds.
    /// </summary>
    public sealed class FieldContentViewSwitchPerformanceTests
    {
        /// <summary>Default overlay toggles used by field-content switch benchmarks.</summary>
        private static readonly DisplaySettings DefaultDisplay = new DisplaySettings
        {
            Hillshade = true,
            Contours = true
        };

        /// <summary>
        /// Diagnostic breakdown of existing prewarm phases (for profiling only).
        /// </summary>
        [Fact(Skip = "Profiling helper — run manually to inspect phase timings.")]
        public void ProfileExistingPrewarmPhases_Manual
            (
            )
        {
            FieldSurfaceSampler Surface = BuildWarmSurface();
            (double ColorMapMs, double ContourMs) = ContentViewSwitchBenchmark.MeasureExistingPrewarmPhases(
                Surface,
                DefaultDisplay);
            Assert.Fail($"Existing prewarm phases: colorMap={ColorMapMs:F0} ms, contours={ContourMs:F0} ms.");
        }

        /// <summary>
        /// Profiles the cold proposed → existing switch path used after AGD import.
        /// </summary>
        [Fact]
        public void FakeField01_ProposedToExistingSwitch_ColdPathWithinBudget
            (
            )
        {
            FieldSurfaceSampler Surface = BuildWarmSurface();
            ContentViewSwitchBenchmark.PhaseTimings Timings =
                ContentViewSwitchBenchmark.MeasureColdProposedToExistingSwitch(Surface, DefaultDisplay);

            Assert.True(Timings.ExistingPrewarmMs < 8000.0,
                $"Existing display prewarm too slow: {Timings.ExistingPrewarmMs:F0} ms (budget 8000 ms).");
            Assert.True(Timings.ExistingFrameMs < 4000.0,
                $"Existing frame render too slow: {Timings.ExistingFrameMs:F0} ms (budget 4000 ms).");
            Assert.True(Timings.ExistingSwitchTotalMs < 10000.0,
                $"Cold existing switch too slow: {Timings.ExistingSwitchTotalMs:F0} ms " +
                $"(prewarm {Timings.ExistingPrewarmMs:F0} ms, frame {Timings.ExistingFrameMs:F0} ms, budget 10000 ms).");
        }

        /// <summary>
        /// After background display-cache warming, switching to existing should only pay frame cost.
        /// </summary>
        [Fact]
        public void FakeField01_ProposedToExistingSwitch_WarmDisplayCache_FrameOnlyWithinBudget
            (
            )
        {
            FieldSurfaceSampler Surface = BuildWarmSurface();
            double FrameOnlyMs = ContentViewSwitchBenchmark.MeasureWarmDisplayCacheExistingSwitch(
                Surface,
                DefaultDisplay);

            Assert.True(Surface.AreDisplayCachesWarm(FieldContentView.Existing, DefaultDisplay));
            Assert.True(FrameOnlyMs < 2500.0,
                $"Warm-cache existing frame render too slow: {FrameOnlyMs:F0} ms (budget 2500 ms).");
        }

        /// <summary>
        /// Progress during a cold existing switch should advance past the initial 5% stall quickly.
        /// </summary>
        [Fact]
        public void FakeField01_ProposedToExistingSwitch_ProgressAdvancesPast10PercentQuickly
            (
            )
        {
            FieldSurfaceSampler Surface = BuildWarmSurface();
            ContentViewSwitchBenchmark.PhaseTimings Timings =
                ContentViewSwitchBenchmark.MeasureColdProposedToExistingSwitch(
                    Surface,
                    DefaultDisplay,
                    WarmProposedFrame: false);

            Assert.True(
                Timings.ProgressSamplesBefore10Percent > 0 || Timings.ExistingPrewarmMs < 500.0,
                "Progress did not advance past 10% during existing switch preparation.");
        }

        /// <summary>
        /// Repeated prewarm calls should be near-instant when caches are already warm.
        /// </summary>
        [Fact]
        public void PrewarmDisplayCaches_WhenAlreadyWarm_IsNearInstant
            (
            )
        {
            FieldSurfaceSampler Surface = BuildWarmSurface();
            Surface.PrewarmDisplayCaches(FieldContentView.Existing, DefaultDisplay);

            double MedianMs = ImportBenchmark.MedianMilliseconds(() =>
            {
                Surface.PrewarmDisplayCaches(FieldContentView.Existing, DefaultDisplay);
            });

            Assert.True(Surface.AreDisplayCachesWarm(FieldContentView.Existing, DefaultDisplay));
            Assert.True(MedianMs < 5.0,
                $"Warm display-cache prewarm too slow: {MedianMs:F2} ms (budget 5 ms).");
        }

        /// <summary>
        /// Warming proposed display caches must not discard existing colour-map caches.
        /// </summary>
        [Fact]
        public void PrewarmDisplayCaches_SecondViewRetainsFirstViewColorMap
            (
            )
        {
            FieldSurfaceSampler Surface = BuildWarmSurface();
            DisplaySettings Display = new DisplaySettings { Hillshade = false, Contours = false };

            Surface.ComputeScalarRange(FieldContentView.Existing, out double ExistingMin, out double ExistingMax);
            Surface.ComputeScalarRange(FieldContentView.Proposed, out double ProposedMin, out double ProposedMax);

            SKBitmap ExistingMap = Surface.GetPlanViewColorMap(
                FieldContentView.Existing, ExistingMin, ExistingMax, ApplyHillshade: false);
            Surface.PrewarmDisplayCaches(FieldContentView.Proposed, Display);
            SKBitmap ExistingMapAgain = Surface.GetPlanViewColorMap(
                FieldContentView.Existing, ExistingMin, ExistingMax, ApplyHillshade: false);

            Assert.Same(ExistingMap, ExistingMapAgain);
        }

        /// <summary>
        /// After both views are warm, toggling back should stay well under interactive budgets.
        /// </summary>
        [Fact]
        public void FieldContentViewToggle_WarmCaches_RoundTripIsNearInstant
            (
            )
        {
            FieldSurfaceSampler Surface = BuildWarmSurface();
            DisplaySettings Display = new DisplaySettings { Hillshade = false, Contours = true };

            Surface.PrewarmAllFieldContentViews(Display, HasDesign: true);

            double MedianMs = ImportBenchmark.MedianMilliseconds(() =>
            {
                Surface.ComputeScalarRange(FieldContentView.Existing, out double VMin, out double VMax);
                Surface.GetPlanViewColorMap(FieldContentView.Existing, VMin, VMax, ApplyHillshade: false);
                Surface.GetContourPolylinesByLevel(FieldContentView.Existing, VMin, VMax);

                Surface.ComputeScalarRange(FieldContentView.Proposed, out VMin, out VMax);
                Surface.GetPlanViewColorMap(FieldContentView.Proposed, VMin, VMax, ApplyHillshade: false);
                Surface.GetContourPolylinesByLevel(FieldContentView.Proposed, VMin, VMax);

                Surface.ComputeScalarRange(FieldContentView.Existing, out VMin, out VMax);
                Surface.GetPlanViewColorMap(FieldContentView.Existing, VMin, VMax, ApplyHillshade: false);
                Surface.GetContourPolylinesByLevel(FieldContentView.Existing, VMin, VMax);
            });

            Assert.True(MedianMs < 50.0,
                $"Warm existing/proposed toggle too slow: {MedianMs:F1} ms (budget 50 ms).");
        }

        static private FieldSurfaceSampler BuildWarmSurface
            (
            )
        {
            AgdImportResult Result = AgdService.Import(TestDataPaths.FakeField01Agd);
            ProjectDocument Project = new ProjectDocument
            {
                Survey = Result.Survey,
                DesignParameters = Result.Parameters,
                DesignGrid = Result.Grid,
                SurveySourceFormat = SurveySourceFormat.Agd
            };

            FieldSurfaceSampler Surface = new FieldSurfaceSampler();
            Surface.BuildFromProject(Project);
            return Surface;
        }
    }
}
