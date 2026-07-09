using System.Text;

using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Rendering;

namespace AgGradeDesigner.Diagnostics
{
    /// <summary>
    /// Structured haul 3D viewer diagnostics for fast post-mortem triage in session logs.
    /// </summary>
    internal static class HaulThreeDInstrumentation
    {
        /// <summary>
        /// Logs why committed shared-surface reuse was rejected.
        /// </summary>
        /// <param name="Reason">Short rejection code.</param>
        /// <param name="Detail">Structured state summary.</param>
        public static void LogCommittedSurfaceReuseRejected
            (
            string Reason,
            string Detail
            )
        {
            if (!ImportDiagnosticsLog.ShouldLog())
                return;

            ImportDiagnosticsLog.WriteHaulThreeDSurface(
                "CommittedSurfaceReuse.Rejected",
                $"reason={Reason} {Detail}");
        }

        /// <summary>
        /// Logs display-layer toggles while haul 3D is pinned.
        /// </summary>
        /// <param name="Previous">Display settings before the refresh.</param>
        /// <param name="Current">Display settings after the refresh.</param>
        /// <param name="PinnedRouteScrubIndex">Pinned haul route scrub index.</param>
        /// <param name="CommittedRouteScrubIndex">Route scrub index baked into the committed surface.</param>
        public static void LogDisplayToggle
            (
            DisplaySettings Previous,
            DisplaySettings Current,
            int PinnedRouteScrubIndex,
            int CommittedRouteScrubIndex
            )
        {
            if (!ImportDiagnosticsLog.ShouldLog())
                return;

            string Changed = FormatDisplayToggleDelta(Previous, Current);
            ImportDiagnosticsLog.WriteHaulThreeDSurface(
                "DisplayToggle",
                $"pin={PinnedRouteScrubIndex} committed={CommittedRouteScrubIndex} changed=[{Changed}] " +
                $"now={FormatDisplayFlags(Current)}");
            ImportDiagnosticsLog.WriteToolTraceJsonLine(
                "{" +
                $"\"kind\":\"haul3d_display_toggle\",\"pin\":{PinnedRouteScrubIndex}," +
                $"\"committed\":{CommittedRouteScrubIndex},\"changed\":\"{Changed}\"," +
                $"\"satellite\":{Current.Satellite.ToString().ToLowerInvariant()}," +
                $"\"contours\":{Current.Contours.ToString().ToLowerInvariant()}," +
                $"\"hillshade\":{Current.Hillshade.ToString().ToLowerInvariant()}," +
                $"\"benchmarks\":{Current.Benchmarks.ToString().ToLowerInvariant()}," +
                $"\"water\":{Current.Water.ToString().ToLowerInvariant()}," +
                $"\"haulFlow\":{Current.HaulFlow.ToString().ToLowerInvariant()}}}");
        }

        /// <summary>
        /// Logs scalar-channel health for the surface used during a haul 3D frame build.
        /// </summary>
        /// <param name="Phase">Diagnostic phase identifier.</param>
        /// <param name="Sampler">Surface sampler supplying mesh scalars.</param>
        /// <param name="RouteScrubIndex">Pinned haul route scrub index.</param>
        public static void LogScalarChannelHealth
            (
            string Phase,
            FieldSurfaceSampler Sampler,
            int RouteScrubIndex
            )
        {
            if (!ImportDiagnosticsLog.ShouldLog())
                return;

            HaulThreeDScalarChannelHealth Health = HaulThreeDScalarChannelHealthAnalyzer.Analyze(Sampler);
            ImportDiagnosticsLog.WriteHaulThreeDSurface(
                "ScalarHealth",
                $"phase={Phase} pin={RouteScrubIndex} {HaulThreeDScalarChannelHealthAnalyzer.FormatSummary(Health)}");
            if (Health.MatchesExistingCount > 0)
            {
                ImportDiagnosticsLog.WriteHaulThreeDSurface(
                    "ScalarHealth.WARN",
                    $"phase={Phase} pin={RouteScrubIndex} matchesExisting={Health.MatchesExistingCount} " +
                    "possible survey Existing contamination in haul channel");
            }

            ImportDiagnosticsLog.WriteToolTraceJsonLine(
                HaulThreeDScalarChannelHealthAnalyzer.FormatJsonLine(Health, Phase, RouteScrubIndex));
        }

        /// <summary>
        /// Logs a haul 3D frame-build decision summary after packets are rendered.
        /// </summary>
        /// <param name="ElapsedMs">Wall-clock frame build duration.</param>
        /// <param name="CacheKey">Frame cache fingerprint.</param>
        /// <param name="CommittedReuse">True when the committed shared surface was reused.</param>
        /// <param name="BuildPath">True when the isolated build path was taken.</param>
        /// <param name="HaulChannelBuilt">True when the haul-leveled channel was rebuilt on this build.</param>
        /// <param name="ShouldReplaceSharedSurface">True when isolated scalars will replace the shared surface on commit.</param>
        /// <param name="RenderSurface">Surface sampler used for GPU rendering.</param>
        /// <param name="RouteScrubIndex">Pinned haul route scrub index.</param>
        /// <param name="CommittedRouteScrubIndex">Route scrub index baked into the committed surface.</param>
        public static void LogFrameBuildSummary
            (
            long ElapsedMs,
            string CacheKey,
            bool CommittedReuse,
            bool BuildPath,
            bool HaulChannelBuilt,
            bool ShouldReplaceSharedSurface,
            FieldSurfaceSampler RenderSurface,
            int RouteScrubIndex,
            int CommittedRouteScrubIndex
            )
        {
            if (!ImportDiagnosticsLog.ShouldLog())
                return;

            HaulThreeDScalarChannelHealth Health = HaulThreeDScalarChannelHealthAnalyzer.Analyze(RenderSurface);
            ImportDiagnosticsLog.WriteHaulThreeDSurface(
                "FrameBuild.Summary",
                $"{ElapsedMs}ms pin={RouteScrubIndex} committed={CommittedRouteScrubIndex} " +
                $"committedReuse={CommittedReuse} buildPath={BuildPath} haulBuilt={HaulChannelBuilt} " +
                $"replaceShared={ShouldReplaceSharedSurface} renderBuilt={RenderSurface.HaulLeveledIsBuilt} " +
                HaulThreeDScalarChannelHealthAnalyzer.FormatSummary(Health));
            ImportDiagnosticsLog.WriteToolTraceJsonLine(
                "{" +
                $"\"kind\":\"haul3d_frame_build\",\"ms\":{ElapsedMs},\"pin\":{RouteScrubIndex}," +
                $"\"committed\":{CommittedRouteScrubIndex},\"committedReuse\":{CommittedReuse.ToString().ToLowerInvariant()}," +
                $"\"buildPath\":{BuildPath.ToString().ToLowerInvariant()},\"haulBuilt\":{HaulChannelBuilt.ToString().ToLowerInvariant()}," +
                $"\"replaceShared\":{ShouldReplaceSharedSurface.ToString().ToLowerInvariant()}," +
                $"\"matchesExisting\":{Health.MatchesExistingCount},\"nanHaul\":{Health.NanHaulSampleCount}," +
                $"\"cacheKey\":\"{EscapeJson(CacheKey)}\"}}");
        }

        /// <summary>
        /// Logs orbit-invariant 3D geometry cache hit/miss decisions for haul meshes.
        /// </summary>
        /// <param name="Hit">True when cached geometry was reused.</param>
        /// <param name="Key">Geometry cache fingerprint.</param>
        /// <param name="MeshVertexCount">Coloured mesh vertices after build or from cache.</param>
        /// <param name="LineVertexCount">Contour/line vertices after build or from cache.</param>
        /// <param name="BuildElapsedMs">Geometry rebuild duration; 0 on cache hit.</param>
        public static void LogGeometryCacheDecision
            (
            bool Hit,
            Field3DGeometryCacheKey Key,
            int MeshVertexCount,
            int LineVertexCount,
            long BuildElapsedMs
            )
        {
            if (!ImportDiagnosticsLog.ShouldLog() || Key.ContentView != FieldContentView.Haul)
                return;

            string Decision = Hit ? "HIT" : "MISS";
            ImportDiagnosticsLog.WriteHaulThreeDSurface(
                "GeometryCache",
                $"{Decision} content={Key.ContentView} pin={Key.HaulRouteScrubIndex} hillshade={Key.Hillshade} " +
                $"meshVerts={MeshVertexCount} lineVerts={LineVertexCount} buildMs={BuildElapsedMs}");
            ImportDiagnosticsLog.WriteToolTraceJsonLine(
                "{" +
                $"\"kind\":\"haul3d_geometry_cache\",\"decision\":\"{Decision}\",\"pin\":{Key.HaulRouteScrubIndex}," +
                $"\"hillshade\":{Key.Hillshade.ToString().ToLowerInvariant()}," +
                $"\"meshVerts\":{MeshVertexCount},\"lineVerts\":{LineVertexCount},\"buildMs\":{BuildElapsedMs}}}");
        }

        /// <summary>
        /// Logs haul static-geometry build results including scalar range and contour extraction counts.
        /// </summary>
        /// <param name="Sampler">Surface sampler supplying mesh scalars.</param>
        /// <param name="VMin">Minimum scalar used for mesh colouring.</param>
        /// <param name="VMax">Maximum scalar used for mesh colouring.</param>
        /// <param name="MeshVertexCount">Coloured mesh vertices written to static geometry.</param>
        /// <param name="LineVertexCount">Contour line vertices written to static geometry.</param>
        /// <param name="ContourLevelCount">Contour levels returned by polyline extraction.</param>
        /// <param name="ContourPolylinePointCount">Total polyline points across all contour levels.</param>
        /// <param name="HaulRouteScrubIndex">Pinned haul route scrub index.</param>
        /// <param name="HillshadeMs">Milliseconds spent precomputing mesh hillshade factors.</param>
        /// <param name="MeshMs">Milliseconds spent building coloured mesh vertices.</param>
        /// <param name="ContourFetchMs">Milliseconds spent fetching cached contour polylines.</param>
        /// <param name="ContourGpuMs">Milliseconds spent converting contour polylines to GPU line vertices.</param>
        public static void LogStaticGeometryBuild
            (
            FieldSurfaceSampler Sampler,
            double VMin,
            double VMax,
            int MeshVertexCount,
            int LineVertexCount,
            int ContourLevelCount,
            int ContourPolylinePointCount,
            int HaulRouteScrubIndex,
            long HillshadeMs = 0,
            long MeshMs = 0,
            long ContourFetchMs = 0,
            long ContourGpuMs = 0
            )
        {
            if (!ImportDiagnosticsLog.ShouldLog())
                return;

            HaulThreeDScalarChannelHealth Health = HaulThreeDScalarChannelHealthAnalyzer.Analyze(Sampler);
            ImportDiagnosticsLog.WriteHaulThreeDSurface(
                "StaticGeometry",
                $"pin={HaulRouteScrubIndex} vMin={VMin:F3} vMax={VMax:F3} meshVerts={MeshVertexCount} " +
                $"lineVerts={LineVertexCount} contourLevels={ContourLevelCount} contourPoints={ContourPolylinePointCount} " +
                $"hillshadeMs={HillshadeMs} meshMs={MeshMs} contourFetchMs={ContourFetchMs} contourGpuMs={ContourGpuMs} " +
                HaulThreeDScalarChannelHealthAnalyzer.FormatSummary(Health));
            ImportDiagnosticsLog.WriteToolTraceJsonLine(
                "{" +
                $"\"kind\":\"haul3d_static_geometry\",\"pin\":{HaulRouteScrubIndex}," +
                $"\"vMin\":{VMin},\"vMax\":{VMax},\"meshVerts\":{MeshVertexCount},\"lineVerts\":{LineVertexCount}," +
                $"\"contourLevels\":{ContourLevelCount},\"contourPoints\":{ContourPolylinePointCount}," +
                $"\"hillshadeMs\":{HillshadeMs},\"meshMs\":{MeshMs},\"contourFetchMs\":{ContourFetchMs},\"contourGpuMs\":{ContourGpuMs}," +
                $"\"matchesExisting\":{Health.MatchesExistingCount},\"nanHaul\":{Health.NanHaulSampleCount}}}");
            if (LineVertexCount == 0 && ContourLevelCount > 0)
            {
                ImportDiagnosticsLog.WriteHaulThreeDSurface(
                    "StaticGeometry.WARN",
                    $"pin={HaulRouteScrubIndex} contourLevels={ContourLevelCount} but lineVerts=0");
            }
        }

        /// <summary>
        /// Logs haul frame assembly decisions that gate contour and mesh visibility at draw time.
        /// </summary>
        /// <param name="Display">Active display toggles.</param>
        /// <param name="IncludeColoredMesh">True when coloured mesh vertices are included in the frame.</param>
        /// <param name="IncludeLineDecorations">True when line decorations are eligible for the frame.</param>
        /// <param name="DrawContours">True when contour line vertices are included in the frame.</param>
        /// <param name="CachedLineVertexCount">Contour line vertices in cached static geometry.</param>
        /// <param name="FrameLineVertexCount">Contour line vertices emitted in the assembled frame.</param>
        /// <param name="HaulRouteScrubIndex">Pinned haul route scrub index.</param>
        public static void LogFrameAssembly
            (
            DisplaySettings Display,
            bool IncludeColoredMesh,
            bool IncludeLineDecorations,
            bool DrawContours,
            int CachedLineVertexCount,
            int FrameLineVertexCount,
            int HaulRouteScrubIndex
            )
        {
            if (!ImportDiagnosticsLog.ShouldLog())
                return;

            ImportDiagnosticsLog.WriteHaulThreeDSurface(
                "FrameAssembly",
                $"pin={HaulRouteScrubIndex} includeMesh={IncludeColoredMesh} includeLines={IncludeLineDecorations} " +
                $"drawContours={DrawContours} contoursOn={Display.Contours} cachedLineVerts={CachedLineVertexCount} " +
                $"frameLineVerts={FrameLineVertexCount}");
            ImportDiagnosticsLog.WriteToolTraceJsonLine(
                "{" +
                $"\"kind\":\"haul3d_frame_assembly\",\"pin\":{HaulRouteScrubIndex}," +
                $"\"includeMesh\":{IncludeColoredMesh.ToString().ToLowerInvariant()}," +
                $"\"includeLines\":{IncludeLineDecorations.ToString().ToLowerInvariant()}," +
                $"\"drawContours\":{DrawContours.ToString().ToLowerInvariant()}," +
                $"\"contoursOn\":{Display.Contours.ToString().ToLowerInvariant()}," +
                $"\"cachedLineVerts\":{CachedLineVertexCount},\"frameLineVerts\":{FrameLineVertexCount}}}");
            if (Display.Contours && FrameLineVertexCount == 0)
            {
                ImportDiagnosticsLog.WriteHaulThreeDSurface(
                    "FrameAssembly.WARN",
                    $"pin={HaulRouteScrubIndex} contoursOn=True but frameLineVerts=0 " +
                    $"drawContours={DrawContours} cachedLineVerts={CachedLineVertexCount}");
            }
        }

        /// <summary>
        /// Formats display toggles as a compact flag string.
        /// </summary>
        /// <param name="Display">Display settings to summarize.</param>
        /// <returns>Pipe-separated boolean flags.</returns>
        private static string FormatDisplayFlags
            (
            DisplaySettings Display
            )
        {
            return string.Join(
                "|",
                Display.Satellite,
                Display.Contours,
                Display.Hillshade,
                Display.Benchmarks,
                Display.HaulFlow,
                Display.Water);
        }

        /// <summary>
        /// Lists display fields that changed between two settings snapshots.
        /// </summary>
        /// <param name="Previous">Settings before the toggle.</param>
        /// <param name="Current">Settings after the toggle.</param>
        /// <returns>Comma-separated changed field names, or "none".</returns>
        private static string FormatDisplayToggleDelta
            (
            DisplaySettings Previous,
            DisplaySettings Current
            )
        {
            List<string> Changed = new List<string>();
            if (Previous.Satellite != Current.Satellite)
                Changed.Add("Satellite");
            if (Previous.Contours != Current.Contours)
                Changed.Add("Contours");
            if (Previous.Hillshade != Current.Hillshade)
                Changed.Add("Hillshade");
            if (Previous.Benchmarks != Current.Benchmarks)
                Changed.Add("Benchmarks");
            if (Previous.HaulFlow != Current.HaulFlow)
                Changed.Add("HaulFlow");
            if (Previous.Water != Current.Water)
                Changed.Add("Water");
            if (Math.Abs(Previous.VerticalExaggeration - Current.VerticalExaggeration) > 0.0001)
                Changed.Add("VerticalExaggeration");
            if (Math.Abs(Previous.TopoCutFillOpacityPercent - Current.TopoCutFillOpacityPercent) > 0.0001)
                Changed.Add("TopoOpacity");

            return Changed.Count == 0 ? "none" : string.Join(",", Changed);
        }

        /// <summary>
        /// Escapes a string for embedding in a JSON quoted value.
        /// </summary>
        /// <param name="Value">Raw string.</param>
        /// <returns>Escaped JSON string contents without surrounding quotes.</returns>
        private static string EscapeJson
            (
            string Value
            )
        {
            return Value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
