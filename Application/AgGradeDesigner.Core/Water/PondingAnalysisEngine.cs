namespace AgGradeDesigner.Core.Water
{
    /// <summary>
    /// SCS curve-number ponding analysis compatible with AgGrade <c>PondingMapGenerator</c> (WATER-SPEC §8.1).
    /// </summary>
    public static class PondingAnalysisEngine
    {
        /// <summary>Eight-connected neighbour offsets for grid traversal.</summary>
        private static readonly (int Dr, int Dc)[] Neigh8 =
        [
            (-1, -1), (-1, 0), (-1, 1),
            (0, -1),           (0, 1),
            (1, -1),  (1, 0),  (1, 1),
        ];

        /// <summary>RGBA bytes per overlay cell.</summary>
        private const int RGBA_BYTES_PER_CELL = 4;

        /// <summary>Midpoint factor used in binary search for volume-to-depth mapping.</summary>
        private const double BINARY_SEARCH_MIDPOINT_FACTOR = 0.5;

        /// <summary>
        /// Runs ponding analysis on a prepared elevation grid and returns a display-ready cache entry.
        /// </summary>
        /// <param name="Grid">Elevation grid for the target basis surface.</param>
        /// <param name="Settings">Ponding parameters.</param>
        /// <param name="BasisSurface">Basis surface kind stored on the cache entry.</param>
        /// <param name="RouteScrubIndex">Route scrub index for haul caches; otherwise null.</param>
        /// <param name="Progress">Optional progress reporter.</param>
        /// <returns>Computed cache entry including depth grid and overlay RGBA.</returns>
        public static WaterLayerCacheEntry Compute
            (
            WaterElevationGridBuilder.ElevationGrid Grid,
            WaterSettings Settings,
            WaterBasisSurfaceKind BasisSurface,
            int? RouteScrubIndex,
            IProgress<int>? Progress = null
            )
        {
            Progress?.Report(WaterConstants.PROGRESS_START_PERCENT);
            RunPondingModel(
                Grid.Dem,
                Grid.Rows,
                Grid.Cols,
                Grid.CellSizeM,
                WaterElevationGridBuilder.NO_DATA_VALUE,
                Settings,
                Progress,
                out float[] PondDepthFlat);

            byte[] PayloadBlob = Array.Empty<byte>();
            WaterLayerCacheEntry Entry = new WaterLayerCacheEntry
            {
                BasisSurface = BasisSurface,
                RouteScrubIndex = RouteScrubIndex,
                PondingRainfallMm = Settings.PondingRainfallMm,
                PondingCurveNumber = Settings.PondingCurveNumber,
                GridWidth = Grid.Cols,
                GridHeight = Grid.Rows,
                CellSizeM = Grid.CellSizeM,
                MinLat = Grid.MinLat,
                MaxLat = Grid.MaxLat,
                MinLon = Grid.MinLon,
                MaxLon = Grid.MaxLon,
                PondDepthM = PondDepthFlat,
            };
            AssignSmoothOverlay(Entry, Settings);
            Entry.PayloadBlob = WaterLayerBlobCodec.Encode(Entry);
            Progress?.Report(WaterConstants.PROGRESS_COMPLETE_PERCENT);
            return Entry;
        }

        /// <summary>
        /// Rebuilds the RGBA overlay on a cache entry using current display settings.
        /// </summary>
        /// <param name="Entry">Cache entry whose overlay should be refreshed.</param>
        /// <param name="Settings">Display settings controlling minimum depth and colour scale.</param>
        public static void RefreshOverlay
            (
            WaterLayerCacheEntry Entry,
            WaterSettings Settings
            )
        {
            int ExpectedCells = Entry.GridWidth * Entry.GridHeight;
            if ((ExpectedCells <= 0) ||
                (Entry.PondDepthM == null) ||
                (Entry.PondDepthM.Length != ExpectedCells))
            {
                return;
            }

            AssignSmoothOverlay(Entry, Settings);
        }

        /// <summary>
        /// Builds and assigns a supersampled smooth overlay raster on a cache entry.
        /// </summary>
        /// <param name="Entry">Cache entry receiving overlay bytes and raster dimensions.</param>
        /// <param name="Settings">Display settings for minimum depth and colour scale.</param>
        public static void AssignSmoothOverlay
            (
            WaterLayerCacheEntry Entry,
            WaterSettings Settings
            )
        {
            (byte[] OverlayRgba, int RasterWidth, int RasterHeight) = BuildSmoothOverlayRgba(
                Entry.PondDepthM,
                Entry.GridWidth,
                Entry.GridHeight,
                Settings);
            Entry.OverlayRgba = OverlayRgba;
            Entry.OverlayRasterWidth = RasterWidth;
            Entry.OverlayRasterHeight = RasterHeight;
        }

        /// <summary>
        /// Builds a supersampled RGBA overlay raster with bilinear depth sampling and softened alpha.
        /// </summary>
        /// <param name="PondDepthFlat">Row-major pond depths in metres.</param>
        /// <param name="Cols">Pond depth grid column count.</param>
        /// <param name="Rows">Pond depth grid row count.</param>
        /// <param name="Settings">Display settings for minimum depth and colour scale.</param>
        /// <returns>RGBA bytes and raster dimensions.</returns>
        public static (byte[] Rgba, int Width, int Height) BuildSmoothOverlayRgba
            (
            float[] PondDepthFlat,
            int Cols,
            int Rows,
            WaterSettings Settings
            )
        {
            int Factor = WaterConstants.WATER_OVERLAY_SUPERSAMPLE_FACTOR;
            int Width = Math.Max(2, ((Cols - 1) * Factor) + 1);
            int Height = Math.Max(2, ((Rows - 1) * Factor) + 1);
            uint MinimumDepthMm = Settings.MinimumPondDepthMm;
            uint OverlayMaxDepthMm = WaterConstants.POND_DEPTH_OVERLAY_MAX_MM;
            byte[] Overlay = new byte[Width * Height * RGBA_BYTES_PER_CELL];

            for (Int32 Y = 0; Y < Height; Y++)
            {
                double RowF = (Y / (double)Math.Max(1, Height - 1)) * Math.Max(0, Rows - 1);
                for (Int32 X = 0; X < Width; X++)
                {
                    double ColF = (X / (double)Math.Max(1, Width - 1)) * Math.Max(0, Cols - 1);
                    int Offset = ((Y * Width) + X) * RGBA_BYTES_PER_CELL;
                    if (!TrySamplePondDepthBilinear(PondDepthFlat, Cols, Rows, ColF, RowF, out float DepthM))
                        continue;

                    WaterOverlayPixel Pixel = ComputeOverlayPixel(DepthM, MinimumDepthMm, OverlayMaxDepthMm);
                    if (!Pixel.IsVisible)
                        continue;

                    Overlay[Offset + 0] = Pixel.R;
                    Overlay[Offset + 1] = Pixel.G;
                    Overlay[Offset + 2] = Pixel.B;
                    Overlay[Offset + 3] = Pixel.A;
                }
            }

            WaterOverlayAlphaBlur.ApplyToAlphaChannel(
                Overlay,
                Width,
                Height,
                WaterConstants.WATER_OVERLAY_ALPHA_BLUR_RADIUS);
            return (Overlay, Width, Height);
        }

        /// <summary>
        /// Builds RGBA overlay bytes from pond depth samples using absolute depth scaling.
        /// </summary>
        /// <param name="PondDepthFlat">Row-major pond depths in metres.</param>
        /// <param name="Cols">Grid column count.</param>
        /// <param name="Rows">Grid row count.</param>
        /// <param name="Settings">Display settings for minimum depth and colour scale.</param>
        /// <returns>RGBA byte buffer with four bytes per cell.</returns>
        public static byte[] BuildOverlayRgba
            (
            float[] PondDepthFlat,
            int Cols,
            int Rows,
            WaterSettings Settings
            )
        {
            return BuildOverlayRgba(
                PondDepthFlat,
                Cols,
                Rows,
                Settings.MinimumPondDepthMm,
                WaterConstants.POND_DEPTH_OVERLAY_MAX_MM);
        }

        /// <summary>
        /// Maps a pond depth sample to overlay alpha using the absolute depth colour scale.
        /// </summary>
        /// <param name="DepthM">Realized pond depth in metres.</param>
        /// <param name="MinimumDepthMm">Depths below this value are hidden.</param>
        /// <param name="OverlayMaxDepthMm">Depth mapped to full overlay opacity.</param>
        /// <returns>Alpha byte, or zero when the depth is hidden.</returns>
        public static byte ComputeOverlayAlpha
            (
            float DepthM,
            uint MinimumDepthMm,
            uint OverlayMaxDepthMm
            )
        {
            return ComputeOverlayPixel(DepthM, MinimumDepthMm, OverlayMaxDepthMm).A;
        }

        /// <summary>
        /// Maps a pond depth sample to a depth-tinted overlay pixel with soft minimum-depth fading.
        /// </summary>
        /// <param name="DepthM">Realized pond depth in metres.</param>
        /// <param name="MinimumDepthMm">Depths below this value fade to transparent.</param>
        /// <param name="OverlayMaxDepthMm">Depth mapped to full overlay opacity and deepest tint.</param>
        /// <returns>RGBA pixel, or transparent when the depth is hidden.</returns>
        public static WaterOverlayPixel ComputeOverlayPixel
            (
            float DepthM,
            uint MinimumDepthMm,
            uint OverlayMaxDepthMm
            )
        {
            double MinimumDepthM = MinimumDepthMm / 1000.0;
            double FadeDepthM = WaterConstants.WATER_OVERLAY_MIN_DEPTH_FADE_MM / 1000.0;
            double OverlayMaxDepthM = OverlayMaxDepthMm / 1000.0;
            if ((float.IsNaN(DepthM)) || (OverlayMaxDepthM <= 0.0))
                return WaterOverlayPixel.Transparent;

            double FadeLowM = MinimumDepthM - FadeDepthM;
            if (DepthM <= FadeLowM)
                return WaterOverlayPixel.Transparent;

            double EdgeFade = 1.0;
            if (DepthM < MinimumDepthM + FadeDepthM)
                EdgeFade = SmoothStep(FadeLowM, MinimumDepthM + FadeDepthM, DepthM);

            double Normalized = DepthM / OverlayMaxDepthM;
            if (Normalized > 1.0)
                Normalized = 1.0;

            double Scaled = Math.Pow(Normalized, WaterConstants.POND_DEPTH_GRAYSCALE_EXPONENT);
            byte Alpha = (byte)Math.Clamp(Scaled * WaterConstants.WATER_OVERLAY_MAX_ALPHA * EdgeFade, 0, WaterConstants.WATER_OVERLAY_MAX_ALPHA);
            if (Alpha == 0)
                return WaterOverlayPixel.Transparent;

            byte Red = LerpByte(WaterConstants.WATER_OVERLAY_SHALLOW_RED, WaterConstants.WATER_OVERLAY_RED, Normalized);
            byte Green = LerpByte(WaterConstants.WATER_OVERLAY_SHALLOW_GREEN, WaterConstants.WATER_OVERLAY_GREEN, Normalized);
            byte Blue = LerpByte(WaterConstants.WATER_OVERLAY_SHALLOW_BLUE, WaterConstants.WATER_OVERLAY_BLUE, Normalized);
            return new WaterOverlayPixel
            {
                R = Red,
                G = Green,
                B = Blue,
                A = Alpha,
            };
        }

        /// <summary>
        /// Hermite smoothstep between two edge values.
        /// </summary>
        /// <param name="Edge0">Lower edge.</param>
        /// <param name="Edge1">Upper edge.</param>
        /// <param name="Value">Value to interpolate.</param>
        /// <returns>Smoothly interpolated factor in [0, 1].</returns>
        private static double SmoothStep
            (
            double Edge0,
            double Edge1,
            double Value
            )
        {
            if (Math.Abs(Edge1 - Edge0) <= double.Epsilon)
                return Value >= Edge1 ? 1.0 : 0.0;

            double T = Math.Clamp((Value - Edge0) / (Edge1 - Edge0), 0.0, 1.0);
            return T * T * (3.0 - (2.0 * T));
        }

        /// <summary>
        /// Linearly interpolates two byte colour channels.
        /// </summary>
        /// <param name="Shallow">Shallow-water channel value.</param>
        /// <param name="Deep">Deep-water channel value.</param>
        /// <param name="NormalizedDepth">Normalized depth in [0, 1].</param>
        /// <returns>Interpolated channel value.</returns>
        private static byte LerpByte
            (
            byte Shallow,
            byte Deep,
            double NormalizedDepth
            )
        {
            double Clamped = Math.Clamp(NormalizedDepth, 0.0, 1.0);
            return (byte)Math.Clamp((Shallow * (1.0 - Clamped)) + (Deep * Clamped), 0, 255);
        }

        /// <summary>
        /// Bilinearly samples pond depth from a row-major depth grid, ignoring invalid neighbours.
        /// </summary>
        /// <param name="PondDepthFlat">Row-major pond depths in metres.</param>
        /// <param name="Cols">Grid column count.</param>
        /// <param name="Rows">Grid row count.</param>
        /// <param name="ColF">Continuous column coordinate.</param>
        /// <param name="RowF">Continuous row coordinate.</param>
        /// <param name="DepthM">Interpolated depth when any valid neighbour contributes.</param>
        /// <returns>True when at least one valid neighbour contributed.</returns>
        public static bool TrySamplePondDepthBilinear
            (
            float[] PondDepthFlat,
            int Cols,
            int Rows,
            double ColF,
            double RowF,
            out float DepthM
            )
        {
            DepthM = float.NaN;
            if ((Cols <= 0) || (Rows <= 0) || (PondDepthFlat.Length != Cols * Rows))
                return false;

            if ((Cols == 1) && (Rows == 1))
            {
                DepthM = PondDepthFlat[0];
                return !float.IsNaN(DepthM);
            }

            ColF = Math.Clamp(ColF, 0.0, Cols - 1);
            RowF = Math.Clamp(RowF, 0.0, Rows - 1);
            int Col0 = (int)Math.Floor(ColF);
            int Row0 = (int)Math.Floor(RowF);
            int Col1 = Math.Min(Col0 + 1, Cols - 1);
            int Row1 = Math.Min(Row0 + 1, Rows - 1);
            double DeltaCol = ColF - Col0;
            double DeltaRow = RowF - Row0;

            double WeightSum = 0.0;
            double DepthSum = 0.0;
            AccumulateBilinearCorner(PondDepthFlat, Cols, Row0, Col0, (1.0 - DeltaCol) * (1.0 - DeltaRow), ref WeightSum, ref DepthSum);
            AccumulateBilinearCorner(PondDepthFlat, Cols, Row0, Col1, DeltaCol * (1.0 - DeltaRow), ref WeightSum, ref DepthSum);
            AccumulateBilinearCorner(PondDepthFlat, Cols, Row1, Col0, (1.0 - DeltaCol) * DeltaRow, ref WeightSum, ref DepthSum);
            AccumulateBilinearCorner(PondDepthFlat, Cols, Row1, Col1, DeltaCol * DeltaRow, ref WeightSum, ref DepthSum);
            if (WeightSum <= 0.0)
                return false;

            DepthM = (float)(DepthSum / WeightSum);
            return true;
        }

        /// <summary>
        /// Adds one bilinear corner sample when the source depth is valid.
        /// </summary>
        private static void AccumulateBilinearCorner
            (
            float[] PondDepthFlat,
            int Cols,
            int Row,
            int Col,
            double Weight,
            ref double WeightSum,
            ref double DepthSum
            )
        {
            if (Weight <= 0.0)
                return;

            float Depth = PondDepthFlat[(Row * Cols) + Col];
            if (float.IsNaN(Depth))
                return;

            WeightSum += Weight;
            DepthSum += Depth * Weight;
        }

        /// <summary>
        /// Bilinearly samples a precomputed overlay pixel from a row-major RGBA raster.
        /// </summary>
        /// <param name="OverlayRgba">Row-major RGBA bytes.</param>
        /// <param name="Width">Overlay raster width.</param>
        /// <param name="Height">Overlay raster height.</param>
        /// <param name="ColF">Continuous column coordinate in overlay space.</param>
        /// <param name="RowF">Continuous row coordinate in overlay space.</param>
        /// <param name="Pixel">Interpolated overlay pixel when any neighbour is visible.</param>
        /// <returns>True when at least one visible neighbour contributed.</returns>
        public static bool TrySampleOverlayBilinear
            (
            byte[] OverlayRgba,
            int Width,
            int Height,
            double ColF,
            double RowF,
            out WaterOverlayPixel Pixel
            )
        {
            Pixel = WaterOverlayPixel.Transparent;
            if ((Width <= 0) || (Height <= 0) || (OverlayRgba.Length != Width * Height * RGBA_BYTES_PER_CELL))
                return false;

            ColF = Math.Clamp(ColF, 0.0, Width - 1);
            RowF = Math.Clamp(RowF, 0.0, Height - 1);
            int Col0 = (int)Math.Floor(ColF);
            int Row0 = (int)Math.Floor(RowF);
            int Col1 = Math.Min(Col0 + 1, Width - 1);
            int Row1 = Math.Min(Row0 + 1, Height - 1);
            double DeltaCol = ColF - Col0;
            double DeltaRow = RowF - Row0;

            double WeightSum = 0.0;
            double RedSum = 0.0;
            double GreenSum = 0.0;
            double BlueSum = 0.0;
            double AlphaSum = 0.0;
            AccumulateOverlayCorner(OverlayRgba, Width, Row0, Col0, (1.0 - DeltaCol) * (1.0 - DeltaRow), ref WeightSum, ref RedSum, ref GreenSum, ref BlueSum, ref AlphaSum);
            AccumulateOverlayCorner(OverlayRgba, Width, Row0, Col1, DeltaCol * (1.0 - DeltaRow), ref WeightSum, ref RedSum, ref GreenSum, ref BlueSum, ref AlphaSum);
            AccumulateOverlayCorner(OverlayRgba, Width, Row1, Col0, (1.0 - DeltaCol) * DeltaRow, ref WeightSum, ref RedSum, ref GreenSum, ref BlueSum, ref AlphaSum);
            AccumulateOverlayCorner(OverlayRgba, Width, Row1, Col1, DeltaCol * DeltaRow, ref WeightSum, ref RedSum, ref GreenSum, ref BlueSum, ref AlphaSum);
            if (WeightSum <= 0.0)
                return false;

            Pixel = new WaterOverlayPixel
            {
                R = (byte)Math.Clamp(RedSum / WeightSum, 0, 255),
                G = (byte)Math.Clamp(GreenSum / WeightSum, 0, 255),
                B = (byte)Math.Clamp(BlueSum / WeightSum, 0, 255),
                A = (byte)Math.Clamp(AlphaSum / WeightSum, 0, 255),
            };
            return Pixel.IsVisible;
        }

        /// <summary>
        /// Adds one bilinear overlay corner sample when the source pixel is visible.
        /// </summary>
        private static void AccumulateOverlayCorner
            (
            byte[] OverlayRgba,
            int Width,
            int Row,
            int Col,
            double Weight,
            ref double WeightSum,
            ref double RedSum,
            ref double GreenSum,
            ref double BlueSum,
            ref double AlphaSum
            )
        {
            if (Weight <= 0.0)
                return;

            int Offset = ((Row * Width) + Col) * RGBA_BYTES_PER_CELL;
            byte Alpha = OverlayRgba[Offset + 3];
            if (Alpha == 0)
                return;

            WeightSum += Weight;
            RedSum += OverlayRgba[Offset + 0] * Weight;
            GreenSum += OverlayRgba[Offset + 1] * Weight;
            BlueSum += OverlayRgba[Offset + 2] * Weight;
            AlphaSum += Alpha * Weight;
        }

        /// <summary>
        /// Builds RGBA overlay bytes from pond depth samples (deeper water is more opaque blue).
        /// </summary>
        /// <param name="PondDepthFlat">Row-major pond depths in metres.</param>
        /// <param name="Cols">Grid column count.</param>
        /// <param name="Rows">Grid row count.</param>
        /// <returns>RGBA byte buffer with four bytes per cell using default display settings.</returns>
        public static byte[] BuildOverlayRgba
            (
            float[] PondDepthFlat,
            int Cols,
            int Rows
            )
        {
            return BuildOverlayRgba(PondDepthFlat, Cols, Rows, new WaterSettings());
        }

        /// <summary>
        /// Builds RGBA overlay bytes from pond depth samples using absolute depth scaling.
        /// </summary>
        /// <param name="PondDepthFlat">Row-major pond depths in metres.</param>
        /// <param name="Cols">Grid column count.</param>
        /// <param name="Rows">Grid row count.</param>
        /// <param name="MinimumDepthMm">Cells below this depth are hidden.</param>
        /// <param name="OverlayMaxDepthMm">Depth mapped to full overlay opacity.</param>
        /// <returns>RGBA byte buffer with four bytes per cell.</returns>
        public static byte[] BuildOverlayRgba
            (
            float[] PondDepthFlat,
            int Cols,
            int Rows,
            uint MinimumDepthMm,
            uint OverlayMaxDepthMm
            )
        {
            int CellCount = Cols * Rows;
            byte[] Overlay = new byte[CellCount * RGBA_BYTES_PER_CELL];
            double OverlayMaxDepthM = OverlayMaxDepthMm / 1000.0;

            for (Int32 Index = 0; Index < CellCount; Index++)
            {
                int Offset = Index * RGBA_BYTES_PER_CELL;
                float Depth = PondDepthFlat[Index];
                if (float.IsNaN(Depth) || (OverlayMaxDepthM <= 0.0))
                {
                    Overlay[Offset + 0] = 0;
                    Overlay[Offset + 1] = 0;
                    Overlay[Offset + 2] = 0;
                    Overlay[Offset + 3] = 0;
                    continue;
                }

                WaterOverlayPixel Pixel = ComputeOverlayPixel(Depth, MinimumDepthMm, OverlayMaxDepthMm);
                if (!Pixel.IsVisible)
                {
                    Overlay[Offset + 0] = 0;
                    Overlay[Offset + 1] = 0;
                    Overlay[Offset + 2] = 0;
                    Overlay[Offset + 3] = 0;
                    continue;
                }

                Overlay[Offset + 0] = Pixel.R;
                Overlay[Offset + 1] = Pixel.G;
                Overlay[Offset + 2] = Pixel.B;
                Overlay[Offset + 3] = Pixel.A;
            }

            return Overlay;
        }

        /// <summary>
        /// Runs the SCS runoff, D8 routing, and depression fill/spill ponding model.
        /// </summary>
        private static void RunPondingModel
            (
            float[,] Dem,
            int Rows,
            int Cols,
            double CellSizeM,
            float NoDataValue,
            WaterSettings Settings,
            IProgress<int>? Progress,
            out float[] PondDepthFlat
            )
        {
            uint RainfallMm = Settings.PondingRainfallMm;
            uint CurveNumber = Settings.PondingCurveNumber;
            int Size = Rows * Cols;
            bool[] Valid = new bool[Size];
            double[] DemFlat = new double[Size];
            for (Int32 Row = 0; Row < Rows; Row++)
            {
                for (Int32 Col = 0; Col < Cols; Col++)
                {
                    int Index = (Row * Cols) + Col;
                    float Value = Dem[Row, Col];
                    Valid[Index] = (Value != NoDataValue) && (!float.IsNaN(Value)) && (!float.IsInfinity(Value));
                    DemFlat[Index] = Valid[Index] ? Value : 0.0;
                }
            }

            Progress?.Report(WaterConstants.PROGRESS_DEM_READY_PERCENT);
            double CellArea = CellSizeM * CellSizeM;
            double[] DepthInSink = ComputeDepthInSinkFromDem(DemFlat, Valid, Rows, Cols);
            D8Result D8 = ComputeD8Receiver(DemFlat, Valid, Rows, Cols, CellSizeM, CellSizeM);

            double Curve = Math.Clamp(CurveNumber, WaterConstants.MIN_SCS_CURVE_NUMBER, WaterConstants.MAX_SCS_CURVE_NUMBER);
            double RetentionMm = (WaterConstants.SCS_RETENTION_NUMERATOR_MM / Curve) - WaterConstants.SCS_RETENTION_DENOMINATOR_OFFSET_MM;
            double InitialAbstractionMm = WaterConstants.SCS_INITIAL_ABSTRACTION_FACTOR * RetentionMm;
            double Rainfall = RainfallMm;
            double RunoffMm = Rainfall <= InitialAbstractionMm
                ? 0.0
                : ((Rainfall - InitialAbstractionMm) * (Rainfall - InitialAbstractionMm)) / (Rainfall - InitialAbstractionMm + RetentionMm);
            double RunoffDepthM = RunoffMm / 1000.0;
            double[] RunoffVolumePerCell = new double[Size];
            for (Int32 Index = 0; Index < Size; Index++)
                RunoffVolumePerCell[Index] = Valid[Index] ? RunoffDepthM * CellArea : 0.0;

            RouteRunoffToTerminals(RunoffVolumePerCell, D8, out double[] Accumulated, out long[] TerminalOfCell);
            Progress?.Report(WaterConstants.PROGRESS_ROUTING_COMPLETE_PERCENT);
            int[] Labels = LabelSinkRegions(DepthInSink, Valid, Rows, Cols);

            int[] TerminalSinkId = new int[Size];
            for (Int32 Index = 0; Index < Size; Index++)
            {
                long Terminal = TerminalOfCell[Index];
                TerminalSinkId[Index] = (Terminal >= 0) && (Terminal < Size) ? Labels[Terminal] : 0;
            }

            Dictionary<int, double> IncomingVolume = new Dictionary<int, double>();
            for (Int32 Index = 0; Index < Size; Index++)
            {
                if (!Valid[Index])
                    continue;

                int SinkId = TerminalSinkId[Index];
                if (SinkId > 0)
                {
                    if (!IncomingVolume.ContainsKey(SinkId))
                        IncomingVolume[SinkId] = 0.0;

                    IncomingVolume[SinkId] += RunoffVolumePerCell[Index];
                }
            }

            Dictionary<int, SinkInfo> SinkInfos = BuildSinkInfos(
                DemFlat,
                DepthInSink,
                Labels,
                Valid,
                TerminalOfCell,
                Rows,
                Cols,
                CellArea);
            List<int> OrderedSinkIds = SinkInfos.Keys.OrderBy(SinkId => SinkInfos[SinkId].SpillElev).ToList();
            Dictionary<int, double> StoredVolume = new Dictionary<int, double>();
            Dictionary<int, double> OverflowVolume = new Dictionary<int, double>();
            foreach (int SinkId in SinkInfos.Keys)
            {
                StoredVolume[SinkId] = 0.0;
                OverflowVolume[SinkId] = 0.0;
            }

            foreach (int SinkId in OrderedSinkIds)
            {
                SinkInfo Info = SinkInfos[SinkId];
                double VolumeIn = (IncomingVolume.TryGetValue(SinkId, out double Incoming) ? Incoming : 0.0) + OverflowVolume[SinkId];
                double Capacity = Info.CapacityVolume;
                if (VolumeIn <= Capacity)
                {
                    StoredVolume[SinkId] = VolumeIn;
                }
                else
                {
                    StoredVolume[SinkId] = Capacity;
                    double Excess = VolumeIn - Capacity;
                    int? Downstream = Info.DownstreamSinkId;
                    if (Downstream.HasValue && SinkInfos.ContainsKey(Downstream.Value))
                        OverflowVolume[Downstream.Value] += Excess;
                }
            }

            PondDepthFlat = new float[Size];
            for (Int32 Index = 0; Index < Size; Index++)
                PondDepthFlat[Index] = float.NaN;

            foreach (KeyValuePair<int, SinkInfo> Pair in SinkInfos)
            {
                int SinkId = Pair.Key;
                SinkInfo Info = Pair.Value;
                double Volume = StoredVolume[SinkId];
                double[] CapacityDepth = new double[Info.CellsFlat.Length];
                for (Int32 CellIndex = 0; CellIndex < CapacityDepth.Length; CellIndex++)
                    CapacityDepth[CellIndex] = DepthInSink[Info.CellsFlat[CellIndex]];

                double[] Realized = VolumeToDepths(CapacityDepth, Volume, CellArea);
                for (Int32 CellIndex = 0; CellIndex < Info.CellsFlat.Length; CellIndex++)
                {
                    int Flat = Info.CellsFlat[CellIndex];
                    PondDepthFlat[Flat] = (float)Realized[CellIndex];
                }
            }

            Progress?.Report(WaterConstants.PROGRESS_OVERLAY_START_PERCENT);
        }

        /// <summary>
        /// Returns true when a row/column lies inside the grid bounds.
        /// </summary>
        private static bool InBounds
            (
            int Row,
            int Col,
            int Rows,
            int Cols
            )
        {
            return (Row >= 0) && (Row < Rows) && (Col >= 0) && (Col < Cols);
        }

        /// <summary>
        /// Computes depression depth below the priority-flood surface for each valid cell.
        /// </summary>
        private static double[] ComputeDepthInSinkFromDem
            (
            double[] Dem,
            bool[] Valid,
            int Rows,
            int Cols
            )
        {
            double[] Filled = PriorityFloodFill(Dem, Valid, Rows, Cols);
            double[] Depth = new double[Dem.Length];
            for (Int32 Index = 0; Index < Depth.Length; Index++)
                Depth[Index] = Valid[Index] ? Math.Max(Filled[Index] - Dem[Index], 0.0) : 0.0;

            return Depth;
        }

        /// <summary>
        /// Priority-flood fill used to identify sink capacity depths.
        /// </summary>
        private static double[] PriorityFloodFill
            (
            double[] Dem,
            bool[] Valid,
            int Rows,
            int Cols
            )
        {
            double[] Filled = (double[])Dem.Clone();
            bool[] Visited = new bool[Dem.Length];
            PriorityQueue<(int Row, int Col), double> Queue = new PriorityQueue<(int Row, int Col), double>();

            for (Int32 Row = 0; Row < Rows; Row++)
            {
                foreach (int Col in new[] { 0, Cols - 1 })
                {
                    int Index = (Row * Cols) + Col;
                    if (Valid[Index] && !Visited[Index])
                    {
                        Queue.Enqueue((Row, Col), Filled[Index]);
                        Visited[Index] = true;
                    }
                }
            }

            for (Int32 Col = 0; Col < Cols; Col++)
            {
                foreach (int Row in new[] { 0, Rows - 1 })
                {
                    int Index = (Row * Cols) + Col;
                    if (Valid[Index] && !Visited[Index])
                    {
                        Queue.Enqueue((Row, Col), Filled[Index]);
                        Visited[Index] = true;
                    }
                }
            }

            while (Queue.Count > 0)
            {
                (int Row, int Col) = Queue.Dequeue();
                double Elevation = Filled[(Row * Cols) + Col];
                foreach ((int Dr, int Dc) in Neigh8)
                {
                    int NeighbourRow = Row + Dr;
                    int NeighbourCol = Col + Dc;
                    if (!InBounds(NeighbourRow, NeighbourCol, Rows, Cols))
                        continue;

                    int Index = (NeighbourRow * Cols) + NeighbourCol;
                    if ((!Valid[Index]) || Visited[Index])
                        continue;

                    Visited[Index] = true;
                    double NeighbourElevation = Filled[Index];
                    if (NeighbourElevation < Elevation)
                    {
                        Filled[Index] = Elevation;
                        NeighbourElevation = Elevation;
                    }

                    Queue.Enqueue((NeighbourRow, NeighbourCol), NeighbourElevation);
                }
            }

            return Filled;
        }

        /// <summary>D8 flow receiver indices and topological order.</summary>
        private sealed class D8Result
        {
            /// <summary>Downslope receiver flat index per cell, or -1 at sinks.</summary>
            public long[] Receiver = Array.Empty<long>();

            /// <summary>Count of upstream donors per cell.</summary>
            public int[] DonorCount = Array.Empty<int>();

            /// <summary>Cells in upstream-to-downstream processing order.</summary>
            public List<long> Order = new List<long>();
        }

        /// <summary>
        /// Computes D8 steepest-descent receivers for each valid cell.
        /// </summary>
        private static D8Result ComputeD8Receiver
            (
            double[] Dem,
            bool[] Valid,
            int Rows,
            int Cols,
            double Dx,
            double Dy
            )
        {
            int Size = Rows * Cols;
            long[] Receiver = new long[Size];
            int[] DonorCount = new int[Size];
            for (Int32 Index = 0; Index < Size; Index++)
                Receiver[Index] = -1;

            double DiagonalDistance = Math.Sqrt((Dx * Dx) + (Dy * Dy));
            Dictionary<(int, int), double> Distance = new Dictionary<(int, int), double>
            {
                [(-1, -1)] = DiagonalDistance,
                [(-1, 0)] = Dy,
                [(-1, 1)] = DiagonalDistance,
                [(0, -1)] = Dx,
                [(0, 1)] = Dx,
                [(1, -1)] = DiagonalDistance,
                [(1, 0)] = Dy,
                [(1, 1)] = DiagonalDistance,
            };

            for (Int32 Row = 0; Row < Rows; Row++)
            {
                for (Int32 Col = 0; Col < Cols; Col++)
                {
                    int Current = (Row * Cols) + Col;
                    if (!Valid[Current])
                        continue;

                    double Elevation = Dem[Current];
                    double BestSlope = 0.0;
                    long BestIndex = -1;
                    foreach ((int Dr, int Dc) in Neigh8)
                    {
                        int NeighbourRow = Row + Dr;
                        int NeighbourCol = Col + Dc;
                        if ((!InBounds(NeighbourRow, NeighbourCol, Rows, Cols)) || (!Valid[(NeighbourRow * Cols) + NeighbourCol]))
                            continue;

                        long Index = (NeighbourRow * Cols) + NeighbourCol;
                        double Delta = Elevation - Dem[Index];
                        double Slope = Delta / Distance[(Dr, Dc)];
                        if (Slope > BestSlope)
                        {
                            BestSlope = Slope;
                            BestIndex = Index;
                        }
                    }

                    Receiver[Current] = BestIndex;
                    if (BestIndex >= 0)
                        DonorCount[BestIndex]++;
                }
            }

            int[] DonorWork = (int[])DonorCount.Clone();
            List<long> Order = new List<long>();
            Queue<long> Queue = new Queue<long>();
            for (long Cell = 0; Cell < Size; Cell++)
            {
                if (Valid[Cell] && (DonorWork[Cell] == 0))
                    Queue.Enqueue(Cell);
            }

            while (Queue.Count > 0)
            {
                long Cell = Queue.Dequeue();
                Order.Add(Cell);
                long Downstream = Receiver[Cell];
                if ((Downstream >= 0) && (--DonorWork[Downstream] == 0))
                    Queue.Enqueue(Downstream);
            }

            return new D8Result { Receiver = Receiver, DonorCount = DonorCount, Order = Order };
        }

        /// <summary>
        /// Routes per-cell runoff volumes to terminal sink cells along D8 receivers.
        /// </summary>
        private static void RouteRunoffToTerminals
            (
            double[] RunoffVolumePerCell,
            D8Result D8,
            out double[] Accumulated,
            out long[] TerminalOfCell
            )
        {
            int Size = RunoffVolumePerCell.Length;
            Accumulated = (double[])RunoffVolumePerCell.Clone();
            TerminalOfCell = new long[Size];
            for (Int32 Index = 0; Index < Size; Index++)
                TerminalOfCell[Index] = -1;

            foreach (long Cell in D8.Order)
            {
                long Downstream = D8.Receiver[Cell];
                if (Downstream >= 0)
                    Accumulated[Downstream] += Accumulated[Cell];
            }

            for (long Cell = 0; Cell < Size; Cell++)
            {
                if (TerminalOfCell[Cell] >= 0)
                    continue;

                long Current = Cell;
                List<long> Path = new List<long>();
                long Terminal = -1;
                while ((Current >= 0) && (TerminalOfCell[Current] < 0))
                {
                    Path.Add(Current);
                    long Next = D8.Receiver[Current];
                    if (Next < 0)
                    {
                        Terminal = Current;
                        break;
                    }

                    Current = Next;
                }

                if ((Terminal < 0) && (Current >= 0))
                    Terminal = TerminalOfCell[Current];

                foreach (long PathCell in Path)
                    TerminalOfCell[PathCell] = Terminal;
            }
        }

        /// <summary>
        /// Labels eight-connected sink regions where depth-in-sink is positive.
        /// </summary>
        private static int[] LabelSinkRegions
            (
            double[] DepthInSink,
            bool[] Valid,
            int Rows,
            int Cols
            )
        {
            int Size = Rows * Cols;
            int[] Labels = new int[Size];
            int NextLabel = 1;
            Stack<int> Stack = new Stack<int>();
            for (Int32 Start = 0; Start < Size; Start++)
            {
                if ((Labels[Start] != 0) || (!Valid[Start]) || (DepthInSink[Start] <= 0.0))
                    continue;

                int Label = NextLabel++;
                Labels[Start] = Label;
                Stack.Push(Start);
                while (Stack.Count > 0)
                {
                    int Index = Stack.Pop();
                    int Row = Index / Cols;
                    int Col = Index % Cols;
                    foreach ((int Dr, int Dc) in Neigh8)
                    {
                        int NeighbourRow = Row + Dr;
                        int NeighbourCol = Col + Dc;
                        if (!InBounds(NeighbourRow, NeighbourCol, Rows, Cols))
                            continue;

                        int NeighbourIndex = (NeighbourRow * Cols) + NeighbourCol;
                        if ((Labels[NeighbourIndex] != 0) || (!Valid[NeighbourIndex]) || (DepthInSink[NeighbourIndex] <= 0.0))
                            continue;

                        Labels[NeighbourIndex] = Label;
                        Stack.Push(NeighbourIndex);
                    }
                }
            }

            return Labels;
        }

        /// <summary>Metadata for one labelled sink region.</summary>
        private sealed class SinkInfo
        {
            /// <summary>Sink label identifier.</summary>
            public int SinkId;

            /// <summary>Flat indices of cells belonging to this sink.</summary>
            public int[] CellsFlat = Array.Empty<int>();

            /// <summary>Elevation at which this sink spills to neighbours.</summary>
            public double SpillElev;

            /// <summary>Maximum storable volume in cubic metres.</summary>
            public double CapacityVolume;

            /// <summary>Downstream sink label receiving overflow, if any.</summary>
            public int? DownstreamSinkId;
        }

        /// <summary>
        /// Builds sink region metadata used for fill-spill routing.
        /// </summary>
        private static Dictionary<int, SinkInfo> BuildSinkInfos
            (
            double[] Dem,
            double[] DepthInSink,
            int[] Labels,
            bool[] Valid,
            long[] TerminalOfCell,
            int Rows,
            int Cols,
            double CellArea
            )
        {
            Dictionary<int, SinkInfo> Infos = new Dictionary<int, SinkInfo>();
            List<int> UniqueIds = Labels.Where(Label => Label > 0).Distinct().OrderBy(Label => Label).ToList();
            foreach (int SinkId in UniqueIds)
            {
                List<int> CellsFlat = new List<int>();
                double CapacitySum = 0.0;
                for (Int32 Index = 0; Index < Labels.Length; Index++)
                {
                    if (Labels[Index] != SinkId)
                        continue;

                    CellsFlat.Add(Index);
                    CapacitySum += DepthInSink[Index] * CellArea;
                }

                double SpillElev = ComputeSinkSpillElevation(Dem, Labels, SinkId, Valid, Rows, Cols);
                Infos[SinkId] = new SinkInfo
                {
                    SinkId = SinkId,
                    CellsFlat = CellsFlat.ToArray(),
                    SpillElev = SpillElev,
                    CapacityVolume = CapacitySum,
                    DownstreamSinkId = null,
                };
            }

            foreach (int SinkId in UniqueIds)
            {
                double BestEscape = double.PositiveInfinity;
                int? BestPair = null;
                for (Int32 Row = 0; Row < Rows; Row++)
                {
                    for (Int32 Col = 0; Col < Cols; Col++)
                    {
                        int Index = (Row * Cols) + Col;
                        if (Labels[Index] != SinkId)
                            continue;

                        foreach ((int Dr, int Dc) in Neigh8)
                        {
                            int NeighbourRow = Row + Dr;
                            int NeighbourCol = Col + Dc;
                            if (!InBounds(NeighbourRow, NeighbourCol, Rows, Cols))
                            {
                                if (Dem[Index] < BestEscape)
                                {
                                    BestEscape = Dem[Index];
                                    BestPair = null;
                                }

                                continue;
                            }

                            int NeighbourIndex = (NeighbourRow * Cols) + NeighbourCol;
                            if (!Valid[NeighbourIndex])
                            {
                                if (Dem[Index] < BestEscape)
                                {
                                    BestEscape = Dem[Index];
                                    BestPair = null;
                                }

                                continue;
                            }

                            int Other = Labels[NeighbourIndex];
                            if (Other != SinkId)
                            {
                                double Escape = Math.Max(Dem[Index], Dem[NeighbourIndex]);
                                if (Escape < BestEscape)
                                {
                                    BestEscape = Escape;
                                    BestPair = Other > 0 ? Other : (int?)null;
                                }
                            }
                        }
                    }
                }

                Infos[SinkId].DownstreamSinkId = BestPair;
            }

            return Infos;
        }

        /// <summary>
        /// Computes the spill elevation for one sink region.
        /// </summary>
        private static double ComputeSinkSpillElevation
            (
            double[] Dem,
            int[] Labels,
            int SinkId,
            bool[] Valid,
            int Rows,
            int Cols
            )
        {
            double Spill = double.PositiveInfinity;
            for (Int32 Row = 0; Row < Rows; Row++)
            {
                for (Int32 Col = 0; Col < Cols; Col++)
                {
                    int Index = (Row * Cols) + Col;
                    if (Labels[Index] != SinkId)
                        continue;

                    foreach ((int Dr, int Dc) in Neigh8)
                    {
                        int NeighbourRow = Row + Dr;
                        int NeighbourCol = Col + Dc;
                        if (!InBounds(NeighbourRow, NeighbourCol, Rows, Cols))
                        {
                            Spill = Math.Min(Spill, Dem[Index]);
                            continue;
                        }

                        int NeighbourIndex = (NeighbourRow * Cols) + NeighbourCol;
                        if (!Valid[NeighbourIndex])
                        {
                            Spill = Math.Min(Spill, Dem[Index]);
                            continue;
                        }

                        if (Labels[NeighbourIndex] != SinkId)
                            Spill = Math.Min(Spill, Math.Max(Dem[Index], Dem[NeighbourIndex]));
                    }
                }
            }

            return double.IsInfinity(Spill) ? 0.0 : Spill;
        }

        /// <summary>
        /// Maps a target runoff volume to realized pond depths across sink cells.
        /// </summary>
        private static double[] VolumeToDepths
            (
            double[] DepthCapacity,
            double TargetVolume,
            double CellArea
            )
        {
            if (TargetVolume <= 0.0)
                return new double[DepthCapacity.Length];

            double TotalCapacity = DepthCapacity.Sum() * CellArea;
            if (TargetVolume >= TotalCapacity)
                return (double[])DepthCapacity.Clone();

            double Low = 0.0;
            double High = DepthCapacity.Max();
            for (Int32 Iteration = 0; Iteration < WaterConstants.VOLUME_TO_DEPTH_MAX_ITERATIONS; Iteration++)
            {
                double Mid = (Low + High) * BINARY_SEARCH_MIDPOINT_FACTOR;
                double Volume = DepthCapacity.Sum(Depth => Math.Max(Depth - Mid, 0.0)) * CellArea;
                if (Volume > TargetVolume)
                    Low = Mid;
                else
                    High = Mid;
            }

            double Level = High;
            return DepthCapacity.Select(Depth => Math.Max(Depth - Level, 0.0)).ToArray();
        }
    }
}
