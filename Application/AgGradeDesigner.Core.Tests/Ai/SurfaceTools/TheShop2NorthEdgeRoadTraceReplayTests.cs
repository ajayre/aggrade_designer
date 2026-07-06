using System.Text.Json;

using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Ai.SurfaceTools;
using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Tests.Import;

namespace AgGradeDesigner.Core.Tests.Ai.SurfaceTools
{
    /// <summary>
    /// Replays captured AI tool traces for TheShop2 north-edge road requests without an LLM.
    /// </summary>
    public sealed class TheShop2NorthEdgeRoadTraceReplayTests
    {
        /// <summary>Minimum bins a full-width north-edge road should affect on TheShop2.</summary>
        private const int MIN_AFFECTED_BIN_COUNT = 500;

        /// <summary>Minimum viewer samples elevated along the northern road stripe.</summary>
        private const int MIN_ELEVATED_VIEWER_SAMPLES = 40;

        /// <summary>Road raise above proposed in metres from the captured trace.</summary>
        private const double ROAD_RAISE_M = 0.4572;

        /// <summary>Plane tolerance when detecting localized road edits in the viewer overlay.</summary>
        private const double PLANE_TOLERANCE_M = 0.001;

        /// <summary>Captured trace from a real chat session.</summary>
        private static readonly string TracePath = Path.Combine(
            AppContext.BaseDirectory,
            "Traces",
            "TheShop2_NorthEdgeRoad_20260705_140234.jsonl");

        /// <summary>
        /// Replays the captured build_road call and verifies a corridor of bins is modified.
        /// </summary>
        [Fact]
        public void ReplayTrace_BuildRoad_AffectsNorthEdgeCorridor
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            AiToolTraceStep BuildRoadStep = ReadBuildRoadStep();
            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher(AiDeliveryPhase.Phase3);

            SurfaceToolResult Result = Dispatcher.Execute(
                Project,
                BuildRoadStep.Tool,
                BuildRoadStep.ArgumentsJson);
            Assert.True(Result.Success, Result.ErrorMessage);

            using JsonDocument Document = JsonDocument.Parse(Result.Json);
            int AffectedBins = Document.RootElement.GetProperty("affected_bin_count").GetInt32();
            Assert.True(
                AffectedBins >= MIN_AFFECTED_BIN_COUNT,
                $"Expected a north-edge road corridor, not a spike. affected={AffectedBins} result={Result.Json}");
        }

        /// <summary>
        /// Replays the captured build_road call and verifies the viewer overlay shows a road stripe.
        /// </summary>
        [Fact]
        public void ReplayTrace_BuildRoad_ViewerShowsRoadStripeNotSpike
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            AiToolTraceStep BuildRoadStep = ReadBuildRoadStep();
            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher(AiDeliveryPhase.Phase3);

            SurfaceToolResult Result = Dispatcher.Execute(
                Project,
                BuildRoadStep.Tool,
                BuildRoadStep.ArgumentsJson);
            Assert.True(Result.Success, Result.ErrorMessage);

            FieldSurfaceSampler Surface = new FieldSurfaceSampler();
            Surface.BuildFromProject(Project);

            int ElevatedSamples = CountElevatedNorthernSamples(Surface, Project.DesignGrid!);
            Assert.True(
                ElevatedSamples >= MIN_ELEVATED_VIEWER_SAMPLES,
                $"Expected a broad north-edge road stripe in the viewer overlay, got {ElevatedSamples} elevated samples.");
        }

        /// <summary>
        /// Returns the build_road step from the captured trace file.
        /// </summary>
        /// <returns>Captured build_road step.</returns>
        static private AiToolTraceStep ReadBuildRoadStep
            (
            )
        {
            IReadOnlyList<AiToolTraceStep> Steps = AiToolTraceReader.ReadToolSteps(TracePath);
            AiToolTraceStep? BuildRoadStep = Steps.SingleOrDefault(Step => Step.Tool == "build_road");
            Assert.NotNull(BuildRoadStep);
            return BuildRoadStep.Value;
        }

        /// <summary>
        /// Counts viewer proposed samples along the northern third of the field that exceed the design plane.
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
                    if (Surface.Proposed[Idx] - PlaneM >= ROAD_RAISE_M - PLANE_TOLERANCE_M)
                        ElevatedSamples++;
                }
            }

            return ElevatedSamples;
        }
    }
}
