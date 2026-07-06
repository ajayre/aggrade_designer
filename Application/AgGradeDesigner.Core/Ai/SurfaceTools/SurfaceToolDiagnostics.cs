using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Survey;

using NetTopologySuite.Geometries;

namespace AgGradeDesigner.Core.Ai.SurfaceTools
{
    /// <summary>
    /// Optional diagnostic sink for AI surface tool execution (wired to the app debug log when enabled).
    /// </summary>
    static public class SurfaceToolDiagnostics
    {
        /// <summary>
        /// Receives diagnostic lines when the application debug log is enabled.
        /// </summary>
        static public Action<string, string, string?>? WriteSink { get; set; }

        /// <summary>
        /// Maximum JSON argument length written to the log.</summary>
        private const int MAX_ARGUMENT_JSON_LENGTH = 4000;

        /// <summary>
        /// Logs the start of a surface tool call.
        /// </summary>
        /// <param name="ToolName">Tool identifier.</param>
        /// <param name="ArgumentsJson">Raw tool arguments JSON.</param>
        /// <param name="Grid">Active design grid.</param>
        static public void LogToolExecuteStart
            (
            string ToolName,
            string ArgumentsJson,
            DesignGrid? Grid
            )
        {
            Write(
                "AiTool",
                $"{ToolName} START",
                $"args={Truncate(ArgumentsJson)} {DescribeGrid(Grid)}");
        }

        /// <summary>
        /// Logs the outcome of a surface tool call.
        /// </summary>
        /// <param name="ToolName">Tool identifier.</param>
        /// <param name="Result">Tool execution result.</param>
        static public void LogToolExecuteEnd
            (
            string ToolName,
            SurfaceToolResult Result
            )
        {
            if (Result.Success)
            {
                Write(
                    "AiTool",
                    $"{ToolName} OK",
                    Truncate(Result.Json));
                return;
            }

            Write("AiTool", $"{ToolName} FAIL", Result.ErrorMessage);
        }

        /// <summary>
        /// Logs parsed road build parameters and mutation summary.
        /// </summary>
        /// <param name="Grid">Live design grid after preview apply.</param>
        /// <param name="Parameters">Parsed road parameters.</param>
        /// <param name="RoadResult">Road build summary.</param>
        /// <param name="PreviewApplied">True when preview elevations were copied to the live grid.</param>
        static public void LogBuildRoad
            (
            DesignGrid Grid,
            RoadBuildParameters Parameters,
            RoadBuildResult RoadResult,
            bool PreviewApplied
            )
        {
            NetTopologySuite.Geometries.Coordinate[] Vertices = Parameters.Centerline.Coordinates;
            NetTopologySuite.Geometries.Coordinate First = Vertices[0];
            NetTopologySuite.Geometries.Coordinate Last = Vertices[^1];
            bool UniformPlane = DesignGridProposedSurfaceBuilder.IsUniformPlaneDesign(Grid);
            (int OffPlaneBins, double MaxOffPlaneM, int OffPlaneX, int OffPlaneY) = SummarizeOffPlaneBins(Grid);

            Write(
                "AiRoad",
                "build_road summary",
                $"roadId={RoadResult.RoadId} vertices={Vertices.Length} " +
                $"centerline=({First.X:0.###},{First.Y:0.###})->({Last.X:0.###},{Last.Y:0.###}) " +
                $"crown={Parameters.CrownWidthM:0.###}m offset={Parameters.VerticalOffsetM:0.###}m " +
                $"sideSlope={(Parameters.SideSlope.HasValue ? "yes" : "no")} " +
                $"affected={RoadResult.AffectedBinCount} offPlane={OffPlaneBins} maxOffPlane={MaxOffPlaneM:0.###}m " +
                $"offPlaneBin=({OffPlaneX},{OffPlaneY}) fill={RoadResult.OperationFillM3:0.###}m3 " +
                $"cut={RoadResult.OperationCutM3:0.###}m3 previewApplied={PreviewApplied} " +
                $"uniformPlane={UniformPlane} {DescribeGrid(Grid)}");

            if (RoadResult.AffectedBinCount == 0)
            {
                Write(
                    "AiRoad",
                    "build_road ZERO affected bins",
                    "Centerline may be outside valid bins or raise/slope parameters produced no change. " +
                    $"gridNorthing=[{Grid.OriginNorthingM:0.###},{Grid.OriginNorthingM + Grid.Height * Grid.BinSizeM:0.###}] " +
                    $"gridEasting=[{Grid.OriginEastingM:0.###},{Grid.OriginEastingM + Grid.Width * Grid.BinSizeM:0.###}]");
            }
        }

        /// <summary>
        /// Logs field geometry payload highlights.
        /// </summary>
        /// <param name="Project">Active project document.</param>
        /// <param name="Grid">Design grid.</param>
        static public void LogFieldGeometry
            (
            ProjectDocument Project,
            DesignGrid Grid
            )
        {
            SurfaceSummary Summary = SurfaceAnalysis.BuildSurfaceSummary(Grid);

            Write(
                "AiRoad",
                "get_field_geometry",
                $"boundaryPoints={Project.Survey?.BoundaryPoints.Count ?? 0} boundarySource={ResolveBoundarySourceLabel(Project)} " +
                $"extents E[{Summary.MinEastingM:0.###},{Summary.MaxEastingM:0.###}] " +
                $"N[{Summary.MinNorthingM:0.###},{Summary.MaxNorthingM:0.###}] " +
                $"grid={Grid.Width}x{Grid.Height} valid={Grid.Bins.Count(B => B.Valid)} {DescribeGrid(Grid)}");
        }

        /// <summary>
        /// Returns the boundary source label used in geometry payloads.
        /// </summary>
        /// <param name="Project">Active project document.</param>
        /// <returns>Boundary source token.</returns>
        static private string ResolveBoundarySourceLabel
            (
            ProjectDocument Project
            )
        {
            return Project.Survey != null && Project.Survey.BoundaryPoints.Count >= 3
                ? "survey"
                : "grid_extent";
        }

        /// <summary>
        /// Logs preview session transitions.
        /// </summary>
        /// <param name="Phase">Preview phase label.</param>
        /// <param name="Operation">Mutating tool name.</param>
        /// <param name="Project">Active project document.</param>
        /// <param name="PreviewGrid">Preview grid when applicable.</param>
        static public void LogPreviewSession
            (
            string Phase,
            string Operation,
            ProjectDocument Project,
            DesignGrid? PreviewGrid
            )
        {
            Write(
                "AiPreview",
                Phase,
                $"operation={Operation} proposedDirty={Project.TopoGeometryDirty.ProposedIsDirty} " +
                $"live={DescribeGrid(Project.DesignGrid)} preview={DescribeGrid(PreviewGrid)}");
        }

        /// <summary>
        /// Logs viewer surface invalidation requests.
        /// </summary>
        /// <param name="Source">Caller label.</param>
        /// <param name="Grid">Project design grid.</param>
        /// <param name="SurfaceDirty">Whether the viewer surface mesh is marked stale.</param>
        /// <param name="ContentView">Active field content view label.</param>
        /// <param name="ProposedIsDirty">Whether proposed topo geometry is marked stale.</param>
        static public void LogViewerSurfaceInvalidate
            (
            string Source,
            DesignGrid? Grid,
            bool SurfaceDirty,
            string ContentView,
            bool ProposedIsDirty
            )
        {
            bool UniformPlane = Grid != null && DesignGridProposedSurfaceBuilder.IsUniformPlaneDesign(Grid);
            (int OffPlaneBins, double MaxOffPlaneM, _, _) = Grid == null
                ? (0, 0.0, -1, -1)
                : SummarizeOffPlaneBins(Grid);

            Write(
                "AiViewer",
                Source,
                $"surfaceDirty={SurfaceDirty} contentView={ContentView} topoProposedDirty={ProposedIsDirty} " +
                $"uniformPlane={UniformPlane} offPlaneBins={OffPlaneBins} maxOffPlane={MaxOffPlaneM:0.###}m " +
                $"{DescribeGrid(Grid)}");
        }

        /// <summary>
        /// Logs synchronous viewer surface rebuild completion.
        /// </summary>
        /// <param name="Grid">Design grid used for rebuild.</param>
        /// <param name="SampleWidth">Viewer sample width.</param>
        /// <param name="SampleHeight">Viewer sample height.</param>
        static public void LogViewerSurfaceRebuild
            (
            DesignGrid? Grid,
            int SampleWidth,
            int SampleHeight
            )
        {
            Write(
                "AiViewer",
                "SyncSurfaceRebuild",
                $"sample={SampleWidth}x{SampleHeight} uniformPlane=" +
                $"{(Grid != null && DesignGridProposedSurfaceBuilder.IsUniformPlaneDesign(Grid))} " +
                $"{DescribeGrid(Grid)}");
        }

        /// <summary>
        /// Writes one diagnostic line when a sink is configured.
        /// </summary>
        /// <param name="Category">Log category.</param>
        /// <param name="Message">Short message.</param>
        /// <param name="Detail">Optional detail text.</param>
        static public void Write
            (
            string Category,
            string Message,
            string? Detail = null
            )
        {
            WriteSink?.Invoke(Category, Message, Detail);
        }

        /// <summary>
        /// Summarizes bins whose proposed elevation deviates from the design plane.</summary>
        static private (int OffPlaneBins, double MaxOffPlaneM, int SampleX, int SampleY) SummarizeOffPlaneBins
            (
            DesignGrid Grid
            )
        {
            int OffPlaneBins = 0;
            double MaxOffPlaneM = 0.0;
            int SampleX = -1;
            int SampleY = -1;

            foreach (GridBinData Bin in Grid.Bins)
            {
                if (!Bin.Valid)
                    continue;

                UTM.UTMCoordinate Utm = UTM.FromLatLon(Bin.CentroidLat, Bin.CentroidLon);
                double PlaneM = DesignGridProposedSurfaceBuilder.EvaluateProposedPlaneAtUtm(
                    Grid,
                    Utm.Easting,
                    Utm.Northing);
                double Delta = Math.Abs(Bin.ProposedM - PlaneM);
                if (Delta <= 0.001)
                    continue;

                OffPlaneBins++;
                if (Delta > MaxOffPlaneM)
                {
                    MaxOffPlaneM = Delta;
                    SampleX = Bin.X;
                    SampleY = Bin.Y;
                }
            }

            return (OffPlaneBins, MaxOffPlaneM, SampleX, SampleY);
        }

        /// <summary>
        /// Returns a compact grid identity string for logs.</summary>
        static private string DescribeGrid
            (
            DesignGrid? Grid
            )
        {
            if (Grid == null)
                return "grid=null";

            int ValidCount = Grid.Bins.Count(B => B.Valid);
            return
                $"grid#{Grid.GetHashCode()} {Grid.Width}x{Grid.Height} bin={Grid.BinSizeM:0.###}m valid={ValidCount} " +
                $"origin=({Grid.OriginEastingM:0.###},{Grid.OriginNorthingM:0.###})";
        }

        /// <summary>
        /// Truncates long JSON payloads for logging.</summary>
        static private string Truncate
            (
            string? Text
            )
        {
            if (string.IsNullOrEmpty(Text))
                return string.Empty;

            string Normalized = Text.Replace('\r', ' ').Replace('\n', ' ');
            if (Normalized.Length <= MAX_ARGUMENT_JSON_LENGTH)
                return Normalized;

            return Normalized[..MAX_ARGUMENT_JSON_LENGTH] + "...";
        }
    }
}
