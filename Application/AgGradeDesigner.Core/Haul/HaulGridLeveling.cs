namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Applies ordered haul passes to a grid copy for scrub base-map rendering.
    /// Field trips move toward the cell target. Road and pad trips are not on those
    /// cells, so their finished section is written from the haul instead.
    /// </summary>
    public static class HaulGridLeveling
    {
        /// <summary>
        /// Returns a grid snapshot with the supplied pass prefix applied to existing elevations.
        /// </summary>
        /// <param name="Source">Source processing grid.</param>
        /// <param name="OrderedPasses">Ordered pass prefix to apply.</param>
        /// <returns>Grid copy after pass application.</returns>
        public static HaulBuilder.Grid BuildAfterPasses
            (
            HaulBuilder.Grid Source,
            IReadOnlyList<HaulBuilder.Pass> OrderedPasses
            )
        {
            return BuildAfterPasses(Source, OrderedPasses, null);
        }

        /// <summary>
        /// Returns a grid snapshot with the supplied pass prefix applied to existing elevations.
        /// </summary>
        /// <param name="Source">Source processing grid.</param>
        /// <param name="OrderedPasses">Ordered pass prefix to apply.</param>
        /// <param name="Road">Finished road and pad section for this frame, or null during field work.</param>
        /// <returns>Grid copy after pass application.</returns>
        internal static HaulBuilder.Grid BuildAfterPasses
            (
            HaulBuilder.Grid Source,
            IReadOnlyList<HaulBuilder.Pass> OrderedPasses,
            HaulRoadSurface? Road
            )
        {
            HaulBuilder.Grid Copy = CloneGrid(Source);
            foreach (HaulBuilder.Pass Pass in OrderedPasses)
            {
                // intent: road fills are pan depths on cells the section does not follow
                if (Pass.Stage == HaulBuilder.HaulStage.Road && Road != null)
                    ApplyRecordedCuts(Copy, Pass);
                else if (Pass.Stage == HaulBuilder.HaulStage.Road)
                    ApplyRecordedDepths(Copy, Pass);
                else
                    ApplyPass(Copy, Pass);
            }

            // intent: the section replaces the pan, including side slopes the pan never lists
            Road?.StampCellCentres(Copy);
            return Copy;
        }

        /// <summary>
        /// Deep-copies a haul processing grid for non-destructive pass simulation.
        /// </summary>
        private static HaulBuilder.Grid CloneGrid
            (
            HaulBuilder.Grid Source
            )
        {
            double[,] Existing = (double[,])Source.Existing.Clone();
            double[,] Target = (double[,])Source.Target.Clone();
            bool[,] Valid = (bool[,])Source.Valid.Clone();

            return new HaulBuilder.Grid(
                Source.GridSizeM,
                Source.Width,
                Source.Height,
                Source.MinX,
                Source.MinY,
                Source.Zone,
                Source.IsNorthern,
                Source.MeanLat,
                Source.MeanLon,
                Existing,
                Target,
                Valid,
                Source.SwellFactor,
                Source.Compaction,
                Source.MaxDepthMm,
                Source.Exclusions);
        }

        /// <summary>
        /// Applies one road or pad trip as the haul recorded it, with no cell-target clamp.
        /// </summary>
        /// <param name="Grid">Grid being leveled.</param>
        /// <param name="Pass">Road-stage trip.</param>
        private static void ApplyRecordedDepths
            (
            HaulBuilder.Grid Grid,
            HaulBuilder.Pass Pass
            )
        {
            ApplyRecordedCuts(Grid, Pass);
            foreach ((ValueTuple<int, int> Cell, double Depth) Entry in Pass.FillCells)
                AddRecordedDepth(Grid, Entry.Cell.Item1, Entry.Cell.Item2, Entry.Depth);
        }

        /// <summary>
        /// Applies the borrow cuts of a road trip and leaves its fills for the finished section.
        /// </summary>
        /// <param name="Grid">Grid being leveled.</param>
        /// <param name="Pass">Road-stage trip.</param>
        private static void ApplyRecordedCuts
            (
            HaulBuilder.Grid Grid,
            HaulBuilder.Pass Pass
            )
        {
            foreach ((ValueTuple<int, int> Cell, double Depth) Entry in Pass.CutCells)
                AddRecordedDepth(Grid, Entry.Cell.Item1, Entry.Cell.Item2, -Entry.Depth);
        }

        /// <summary>
        /// Adds a recorded depth at one cell. Negative depths are cuts.
        /// </summary>
        /// <param name="Grid">Grid being leveled.</param>
        /// <param name="X">Column.</param>
        /// <param name="Y">Row.</param>
        /// <param name="DeltaM">Elevation change in metres.</param>
        private static void AddRecordedDepth
            (
            HaulBuilder.Grid Grid,
            int X,
            int Y,
            double DeltaM
            )
        {
            if (!IsCellInBounds(Grid, X, Y))
                return;

            Grid.Existing[Y, X] += DeltaM;
        }

        /// <summary>
        /// Applies one field pass by moving cut/fill cells toward the design target elevation.
        /// </summary>
        private static void ApplyPass
            (
            HaulBuilder.Grid Grid,
            HaulBuilder.Pass Pass
            )
        {
            foreach ((ValueTuple<int, int> Cell, double Depth) Entry in Pass.CutCells)
                ApplyCutCell(Grid, Entry.Cell.Item1, Entry.Cell.Item2, Entry.Depth);

            foreach ((ValueTuple<int, int> Cell, double Depth) Entry in Pass.FillCells)
                ApplyFillCell(Grid, Entry.Cell.Item1, Entry.Cell.Item2, Entry.Depth);
        }

        /// <summary>
        /// Lowers existing elevation at one cut cell without crossing the target surface.
        /// </summary>
        private static void ApplyCutCell
            (
            HaulBuilder.Grid Grid,
            int X,
            int Y,
            double DepthM
            )
        {
            if (!IsCellInBounds(Grid, X, Y) || !Grid.Valid[Y, X])
                return;

            double Target = Grid.Target[Y, X];
            Grid.Existing[Y, X] = Math.Max(Target, Grid.Existing[Y, X] - DepthM);
        }

        /// <summary>
        /// Raises existing elevation at one fill cell without crossing the target surface.
        /// </summary>
        private static void ApplyFillCell
            (
            HaulBuilder.Grid Grid,
            int X,
            int Y,
            double DepthM
            )
        {
            if (!IsCellInBounds(Grid, X, Y) || !Grid.Valid[Y, X])
                return;

            double Target = Grid.Target[Y, X];
            Grid.Existing[Y, X] = Math.Min(Target, Grid.Existing[Y, X] + DepthM);
        }

        /// <summary>
        /// Returns whether a grid cell index lies inside the processing grid.
        /// </summary>
        private static bool IsCellInBounds
            (
            HaulBuilder.Grid Grid,
            int X,
            int Y
            )
        {
            return X >= 0 && Y >= 0 && X < Grid.Width && Y < Grid.Height;
        }
    }
}
