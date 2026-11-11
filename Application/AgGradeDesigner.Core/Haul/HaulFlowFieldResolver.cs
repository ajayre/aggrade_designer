using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Resolves and caches haul transport <see cref="HaulBuilder.FlowField"/> data for map overlays.
    /// </summary>
    public static class HaulFlowFieldResolver
    {
        /// <summary>Default arrow subsampling stride in grid cells (HaulBuilder parity).</summary>
        public const int DEFAULT_ARROW_STRIDE = 2;

        /// <summary>Resolved haul grid, flow field, and render stride.</summary>
        public sealed class ResolvedHaulFlow
        {
            /// <summary>Processing grid used when haul was generated.</summary>
            public HaulBuilder.Grid Grid { get; init; } = null!;

            /// <summary>Per-cell transport flow vectors and arrow mask.</summary>
            public HaulBuilder.FlowField Flow { get; init; } = null!;

            /// <summary>Arrow subsampling stride in grid cells.</summary>
            public int ArrowStride { get; init; } = DEFAULT_ARROW_STRIDE;

            /// <summary>True when at least one valid cell exposes a flow arrow.</summary>
            public bool HasVisibleArrows { get; init; }
        }

        private static readonly object CacheLock = new object();
        private static HaulData? CachedData;
        private static ResolvedHaulFlow? CachedResolved;

        /// <summary>
        /// Returns whether the supplied haul session exposes flow arrow data without recomputing the flow field.
        /// </summary>
        /// <param name="Data">Haul payload to inspect.</param>
        /// <returns>True when flow arrows may be drawn.</returns>
        public static bool HasFlowArrowData
            (
            HaulData? Data
            )
        {
            if (Data == null || !Data.IsValid)
                return false;

            if (Data.Result?.Flow != null && Data.Result.Grid != null)
                return ScanHasVisibleArrows(Data.Result.Grid, Data.Result.Flow);

            if (HaulFlowFieldCodec.HasVisibleArrows(Data.Payload.FlowField))
                return true;

            // intent: legacy persisted haul reloads without flow cache rebuild arrows lazily on first draw
            return Data.Payload.FlowField == null;
        }

        /// <summary>
        /// Returns whether the supplied haul session exposes at least one flow arrow.
        /// </summary>
        /// <param name="Data">Haul payload to inspect.</param>
        /// <param name="Project">Open project supplying survey and design when flow must be rebuilt.</param>
        /// <param name="Settings">Active haul settings for flow recomputation.</param>
        /// <returns>True when flow arrows can be drawn.</returns>
        public static bool HasVisibleArrows
            (
            HaulData? Data,
            ProjectDocument? Project,
            HaulSettings? Settings
            )
        {
            ResolvedHaulFlow? Resolved = TryResolve(Data, Project, Settings);
            return Resolved?.HasVisibleArrows == true;
        }

        /// <summary>
        /// Resolves haul grid and flow field data, reusing an in-memory cache per <see cref="HaulData"/> instance.
        /// </summary>
        /// <param name="Data">Haul payload to resolve.</param>
        /// <param name="Project">Open project supplying survey and design when flow must be rebuilt.</param>
        /// <param name="Settings">Active haul settings for flow recomputation.</param>
        /// <returns>Resolved flow overlay data, or null when haul data is missing or invalid.</returns>
        public static ResolvedHaulFlow? TryResolve
            (
            HaulData? Data,
            ProjectDocument? Project,
            HaulSettings? Settings
            )
        {
            if (Data == null || !Data.IsValid)
                return null;

            lock (CacheLock)
            {
                if (CachedData == Data && CachedResolved != null)
                    return CachedResolved;
            }

            HaulBuilder.Grid? Grid = Data.Result?.Grid;
            HaulBuilder.FlowField? Flow = Data.Result?.Flow;
            int ArrowStride = Data.Result?.Options?.FlowArrowStrideCells
                ?? (Data.Payload.ArrowStride > 0 ? Data.Payload.ArrowStride : DEFAULT_ARROW_STRIDE);
            string TransportMode = Data.Payload.FlowField?.Mode
                ?? Settings?.TransportMode
                ?? "poisson";

            if (Grid == null || Flow == null)
            {
                if (HaulFlowFieldCodec.TryDecode(Data.Payload.FlowField, out Grid, out Flow))
                {
                    // intent: use persisted flow snapshot so project open does not recompute transport field
                }
                else if (Project == null || Settings == null || !Project.HasSurvey || !Project.HasDesign)
                {
                    return null;
                }
                else
                {
                    string TempAgd = Path.Combine(Path.GetTempPath(), $"aggrade-haul-flow-{Guid.NewGuid():N}.agd");
                    try
                    {
                        AgdService.Export(
                            TempAgd,
                            Project.Survey!,
                            Project.DesignGrid!,
                            Project.DesignParameters ?? new DesignParameters());

                        HaulBuilder.AgdField Field = HaulBuilder.Agd.Parse(TempAgd);
                        Grid = Data.ResolveGrid(Field);
                        Flow = HaulBuilder.FlowFieldSolver.Compute(Grid, TransportMode, 0.0);
                    }
                    finally
                    {
                        if (File.Exists(TempAgd))
                            File.Delete(TempAgd);
                    }
                }
            }

            if (Grid == null || Flow == null)
                return null;

            ResolvedHaulFlow Resolved = new ResolvedHaulFlow
            {
                Grid = Grid,
                Flow = Flow,
                ArrowStride = Math.Max(1, ArrowStride),
                HasVisibleArrows = ScanHasVisibleArrows(Grid, Flow)
            };

            lock (CacheLock)
            {
                CachedData = Data;
                CachedResolved = Resolved;
            }

            return Resolved;
        }

        /// <summary>
        /// Drops cached flow resolution so the next resolve rebuilds from current haul state.
        /// </summary>
        public static void ClearCache
            (
            )
        {
            lock (CacheLock)
            {
                CachedData = null;
                CachedResolved = null;
            }
        }

        /// <summary>
        /// Returns true when any valid grid cell is marked for arrow display.
        /// </summary>
        private static bool ScanHasVisibleArrows
            (
            HaulBuilder.Grid Grid,
            HaulBuilder.FlowField Flow
            )
        {
            for (int By = 0; By < Grid.Height; By++)
            {
                for (int Bx = 0; Bx < Grid.Width; Bx++)
                {
                    if (!Grid.Valid[By, Bx])
                        continue;

                    if (Flow.HasArrow[By, Bx])
                        return true;
                }
            }

            return false;
        }
    }
}
