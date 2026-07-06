using System.Text.Json;

using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Ai.SurfaceTools;
using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Tests.Import;
using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Ai.SurfaceTools
{
    /// <summary>
    /// Verifies north-edge road building on the shared <c>Test Data/design.agd</c> fixture.
    /// </summary>
    public sealed class DesignAgdNorthEdgeRoadTests
    {
        /// <summary>12 ft crown width in metres.</summary>
        private const double CROWN_WIDTH_M = 3.6576;

        /// <summary>1.5 ft raise in metres.</summary>
        private const double RAISE_ABOVE_PROPOSED_M = 0.4572;

        /// <summary>Minimum bins a full-width north-edge road should affect.</summary>
        private const int MIN_AFFECTED_BIN_COUNT = 100;

        /// <summary>Minimum elevated viewer samples along the northern road stripe.</summary>
        private const int MIN_ELEVATED_VIEWER_SAMPLES = 20;

        /// <summary>Plane tolerance when detecting localized road edits in the viewer overlay.</summary>
        private const double PLANE_TOLERANCE_M = 0.001;

        /// <summary>Path to the design.agd fixture at the repository root.</summary>
        private static readonly string DesignAgdPath = Path.Combine(
            TestDataPaths.TestDataDirectory,
            "design.agd");

        /// <summary>
        /// Verifies a bbox north-edge road affects a corridor of bins, not a single spike.
        /// </summary>
        [Fact]
        public void BuildRoad_DesignAgd_NorthEdge_AffectsCorridor
            (
            )
        {
            ProjectDocument Project = LoadDesignAgdProject();
            DesignGrid Grid = Project.DesignGrid!;
            SurfaceSummary Summary = SurfaceAnalysis.BuildSurfaceSummary(Grid);
            string Arguments = BuildNorthEdgeRoadArguments(Summary);

            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher(AiDeliveryPhase.Phase3);
            SurfaceToolResult Result = Dispatcher.Execute(Project, "build_road", Arguments);
            Assert.True(Result.Success, Result.ErrorMessage);

            using JsonDocument Document = JsonDocument.Parse(Result.Json);
            int AffectedBins = Document.RootElement.GetProperty("affected_bin_count").GetInt32();
            double FillM3 = Document.RootElement.GetProperty("fill_m3").GetDouble();

            Assert.True(AffectedBins >= MIN_AFFECTED_BIN_COUNT, Result.Json);
            Assert.True(FillM3 > 0.0, Result.Json);
        }

        /// <summary>
        /// Verifies the viewer overlay shows a broad north-edge stripe after the road build.
        /// </summary>
        [Fact]
        public void BuildRoad_DesignAgd_NorthEdge_ViewerShowsStripe
            (
            )
        {
            ProjectDocument Project = LoadDesignAgdProject();
            DesignGrid Grid = Project.DesignGrid!;
            SurfaceSummary Summary = SurfaceAnalysis.BuildSurfaceSummary(Grid);
            string Arguments = BuildNorthEdgeRoadArguments(Summary);

            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher(AiDeliveryPhase.Phase3);
            SurfaceToolResult Result = Dispatcher.Execute(Project, "build_road", Arguments);
            Assert.True(Result.Success, Result.ErrorMessage);

            FieldSurfaceSampler Surface = new FieldSurfaceSampler();
            Surface.BuildFromProject(Project);

            int ElevatedSamples = CountElevatedNorthernSamples(Surface, Grid);
            Assert.True(
                ElevatedSamples >= MIN_ELEVATED_VIEWER_SAMPLES,
                $"Expected a north-edge road stripe in the viewer overlay, got {ElevatedSamples} elevated samples.");
        }

        /// <summary>
        /// Verifies get_field_geometry exposes valid-bin extents for design.agd.
        /// </summary>
        [Fact]
        public void GetFieldGeometry_DesignAgd_IncludesValidBinExtents
            (
            )
        {
            ProjectDocument Project = LoadDesignAgdProject();
            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher(AiDeliveryPhase.Phase3);

            SurfaceToolResult Result = Dispatcher.Execute(Project, "get_field_geometry", "{}");
            Assert.True(Result.Success, Result.ErrorMessage);
            Assert.Contains("valid_bin_extents", Result.Json, StringComparison.Ordinal);
        }

        /// <summary>
        /// Loads the design.agd fixture as a project document.
        /// </summary>
        /// <returns>Imported project.</returns>
        static private ProjectDocument LoadDesignAgdProject
            (
            )
        {
            Assert.True(File.Exists(DesignAgdPath), $"Missing fixture: {DesignAgdPath}");
            return ImportHandoffProjectFactory.CreateDesignAgdProject();
        }

        /// <summary>
        /// Builds north-edge road arguments using grid envelope coordinates like the AI trace.
        /// </summary>
        /// <param name="Summary">Field summary.</param>
        /// <returns>Tool arguments JSON.</returns>
        static private string BuildNorthEdgeRoadArguments
            (
            SurfaceSummary Summary
            )
        {
            return $$"""
                {
                  "centerline": [
                    { "x": {{Summary.MinEastingM}}, "y": {{Summary.MaxNorthingM}} },
                    { "x": {{Summary.MaxEastingM}}, "y": {{Summary.MaxNorthingM}} }
                  ],
                  "crown_width_m": {{CROWN_WIDTH_M}},
                  "raise_above_proposed_m": {{RAISE_ABOVE_PROPOSED_M}}
                }
                """;
        }

        /// <summary>
        /// Counts viewer proposed samples along the northern third that exceed the design plane.
        /// </summary>
        /// <param name="Surface">Built viewer surface.</param>
        /// <param name="Grid">Design grid.</param>
        /// <returns>Elevated sample count.</returns>
        static private int CountElevatedNorthernSamples
            (
            FieldSurfaceSampler Surface,
            DesignGrid Grid
            )
        {
            int ElevatedSamples = 0;
            int NorthernRowStart = Surface.SampleHeight * 2 / 3;

            for (int Sy = NorthernRowStart; Sy < Surface.SampleHeight; Sy++)
            {
                double Lat = Surface.MaxLat - (Surface.MaxLat - Surface.MinLat) * Sy / Math.Max(1, Surface.SampleHeight - 1);
                for (int Sx = 0; Sx < Surface.SampleWidth; Sx++)
                {
                    int Idx = Sy * Surface.SampleWidth + Sx;
                    if (!Surface.Valid[Idx])
                        continue;

                    double Lon = Surface.MinLon + (Surface.MaxLon - Surface.MinLon) * Sx / Math.Max(1, Surface.SampleWidth - 1);
                    UTM.UTMCoordinate Utm = UTM.FromLatLon(Lat, Lon);
                    double PlaneM = DesignGridProposedSurfaceBuilder.EvaluateProposedPlaneAtUtm(
                        Grid,
                        Utm.Easting,
                        Utm.Northing);
                    if (Surface.Proposed[Idx] - PlaneM >= RAISE_ABOVE_PROPOSED_M - PLANE_TOLERANCE_M)
                        ElevatedSamples++;
                }
            }

            return ElevatedSamples;
        }
    }
}
