using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Import;

namespace AgGradeDesigner.Core.Tests.Haul
{
    /// <summary>
    /// Captures haul 3D overlay extent and border-width diagnostics for regression analysis.
    /// </summary>
    public sealed class HaulThreeDOverlayDiagnosticsTests
    {
        /// <summary>Minimum valid sampler cells expected for The Shop 2 overlay analysis.</summary>
        private const int MIN_VALID_SAMPLER_COUNT = 1000;

        /// <summary>
        /// Generates a diagnostic report for The Shop 2 and writes it beside test output.
        /// </summary>
        [Fact]
        public void Analyze_TheShop2_WritesOverlayDiagnosticReport
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            HaulData Data = HaulEngineService.Generate(Project, new HaulSettings());
            Assert.True(Data.RouteStepCount >= 1, $"Expected haul routes, got {Data.RouteStepCount}.");

            HaulThreeDOverlayDiagnosticsSnapshot Snapshot =
                HaulThreeDOverlayDiagnostics.Analyze(Data, Project, RouteScrubIndex: 0);

            Assert.True(
                Snapshot.ValidSamplerCount >= MIN_VALID_SAMPLER_COUNT,
                Snapshot.FormatReport());

            string ReportDirectory = Path.Combine(
                AppContext.BaseDirectory,
                "HaulOverlayDiagnostics");
            Directory.CreateDirectory(ReportDirectory);
            string ReportPath = Path.Combine(
                ReportDirectory,
                $"TheShop2_overlay_route0_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            HaulThreeDOverlayDiagnostics.WriteReportToFile(Snapshot, ReportPath);

            Assert.True(File.Exists(ReportPath), $"Expected diagnostic report at {ReportPath}.");
            Assert.True(
                Snapshot.NotUpdatedValidCount > 0,
                $"Expected some valid samples outside haul overlay coverage: {Snapshot.FormatReport()}");
        }

        /// <summary>
        /// Verifies later route scrub indices still produce overlay diagnostics.
        /// </summary>
        [Fact]
        public void Analyze_TheShop2_LaterRouteScrubIndex_ProducesOverlayCounts
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            HaulData Data = HaulEngineService.Generate(Project, new HaulSettings());
            if (Data.RouteStepCount < 2)
                return;

            HaulThreeDOverlayDiagnosticsSnapshot Snapshot =
                HaulThreeDOverlayDiagnostics.Analyze(Data, Project, RouteScrubIndex: 1);

            Assert.True(Snapshot.UpdatedSampleCount > 0, Snapshot.FormatReport());
            Assert.True(
                Snapshot.NotUpdatedValidCount >= Snapshot.OutsideHaulUtmBoxCount,
                Snapshot.FormatReport());
        }
    }
}
