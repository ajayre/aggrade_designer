using AgGradeDesigner.Core.Geo;

using AgGradeDesigner.Core.Models;

using AgGradeDesigner.Core.Survey;



using SkiaSharp;



namespace AgGradeDesigner.Core.Rendering

{

    /// <summary>

    /// Builds a high-resolution scalar field for smooth viewer rendering (§2.4.13).

    /// </summary>

    public sealed class FieldSurfaceSampler

    {

        private const int DEFAULT_RESOLUTION = 256;

        private const int SMOOTH_PASSES = 2;



        /// <summary>Raster width in sample cells.</summary>

        public int SampleWidth { get; private set; }



        /// <summary>Raster height in sample cells.</summary>

        public int SampleHeight { get; private set; }



        /// <summary>Southern edge latitude of the sample extent.</summary>

        public double MinLat { get; private set; }



        /// <summary>Northern edge latitude of the sample extent.</summary>

        public double MaxLat { get; private set; }



        /// <summary>Western edge longitude of the sample extent.</summary>

        public double MinLon { get; private set; }



        /// <summary>Eastern edge longitude of the sample extent.</summary>

        public double MaxLon { get; private set; }



        /// <summary>Minimum existing elevation across valid samples.</summary>

        public double MinElevation { get; private set; }



        /// <summary>Maximum existing elevation across valid samples.</summary>

        public double MaxElevation { get; private set; }



        /// <summary>Flattened existing elevation samples indexed row-major.</summary>

        public double[] Existing { get; private set; } = Array.Empty<double>();



        /// <summary>Flattened proposed elevation samples indexed row-major.</summary>

        public double[] Proposed { get; private set; } = Array.Empty<double>();



        /// <summary>Validity mask parallel to <see cref="Existing"/> and <see cref="Proposed"/>.</summary>

        public bool[] Valid { get; private set; } = Array.Empty<bool>();



        private IReadOnlyList<(double Level, IReadOnlyList<(double Lat, double Lon)> Points)>? _ContourCacheByLevel;

        private FieldContentView _ContourCacheView;

        private double _ContourCacheVMin;

        private double _ContourCacheVMax;



        private SKBitmap? _ColorMapCache;

        private SKBitmap? _ColorMapBaseCache;

        private SKBitmap? _ColorMapHillshadeCache;

        private double[]? _PlanViewShadeGrid;

        private FieldContentView _ColorMapView;

        private double _ColorMapVMin;

        private double _ColorMapVMax;

        private bool _ColorMapHillshade;

        private double[]? _MeshHillshadeCache;

        private FieldContentView _MeshHillshadeView;



        /// <summary>

        /// Returns the scalar value at a flattened sample index for the given content view.

        /// </summary>

        /// <param name="ContentView">Scalar channel to read.</param>

        /// <param name="Idx">Flattened row-major sample index.</param>

        /// <returns>Scalar value at the index.</returns>

        public double GetScalarAtIndex

            (

            FieldContentView ContentView,

            int Idx

            )

        {

            // Dispatch flattened index reads through the active scalar channel.
            return ContentView switch

            {

                FieldContentView.Proposed => Proposed[Idx],

                FieldContentView.CutFill => Existing[Idx] - Proposed[Idx],

                _ => Existing[Idx]

            };

        }



        /// <summary>

        /// Returns a cached or freshly built plan-view colour map bitmap.

        /// </summary>

        /// <param name="ContentView">Scalar channel to colour.</param>

        /// <param name="VMin">Minimum value of the display range.</param>

        /// <param name="VMax">Maximum value of the display range.</param>

        /// <param name="ApplyHillshade">When true, applies hillshade modulation.</param>

        /// <returns>RGBA bitmap for the scalar field.</returns>

        public SKBitmap GetPlanViewColorMap

            (

            FieldContentView ContentView,

            double VMin,

            double VMax,

            bool ApplyHillshade

            )

        {

            EnsurePlanViewBaseCache(ContentView, VMin, VMax);



            if (!ApplyHillshade)

                return _ColorMapBaseCache!;



            EnsurePlanViewHillshadeCache(ContentView);

            return _ColorMapHillshadeCache!;

        }



        /// <summary>

        /// Returns a cached hillshade factor for a surface grid vertex (3D mesh shading).

        /// </summary>

        /// <param name="ContentView">Scalar channel used for slope calculation.</param>

        /// <param name="GridIndex">Flattened row-major index into the sample grid.</param>

        /// <returns>Shade factor in [0, 1], or NaN when invalid.</returns>

        public double GetMeshHillshadeFactor

            (

            FieldContentView ContentView,

            int GridIndex

            )

        {

            EnsureMeshHillshadeCache(ContentView);

            if (_MeshHillshadeCache == null || GridIndex < 0 || GridIndex >= _MeshHillshadeCache.Length)

                return double.NaN;

            return _MeshHillshadeCache[GridIndex];

        }



        /// <summary>

        /// Builds or returns the base (unshaded) plan-view colour map cache.

        /// </summary>

        private void EnsurePlanViewBaseCache

            (

            FieldContentView ContentView,

            double VMin,

            double VMax

            )

        {

            if (_ColorMapBaseCache != null

                && _ColorMapView == ContentView

                && _ColorMapVMin == VMin

                && _ColorMapVMax == VMax)

            {

                return;

            }



            InvalidateColorMapCache();

            _ColorMapBaseCache = PlanViewColorMapBuilder.Build(this, ContentView, VMin, VMax, ApplyHillshade: false);

            _ColorMapView = ContentView;

            _ColorMapVMin = VMin;

            _ColorMapVMax = VMax;

        }



        /// <summary>

        /// Composes the hillshade-modulated colour map from cached base colours and shade grid.

        /// </summary>

        private void EnsurePlanViewHillshadeCache

            (

            FieldContentView ContentView

            )

        {

            if (_ColorMapHillshadeCache != null && _ColorMapView == ContentView)

                return;



            if (_PlanViewShadeGrid == null || _ColorMapView != ContentView)

            {

                (_PlanViewShadeGrid, _, _) = PlanViewColorMapBuilder.BuildShadeGrid(this, ContentView);

            }



            _ColorMapHillshadeCache?.Dispose();

            _ColorMapHillshadeCache = PlanViewColorMapBuilder.ApplyHillshadeGrid(_ColorMapBaseCache!, _PlanViewShadeGrid);

            _ColorMapHillshade = true;

        }



        /// <summary>

        /// Precomputes per-vertex hillshade factors on the sample grid for 3D rendering.

        /// </summary>

        private void EnsureMeshHillshadeCache

            (

            FieldContentView ContentView

            )

        {

            if (_MeshHillshadeCache != null && _MeshHillshadeView == ContentView)

                return;



            int Count = SampleWidth * SampleHeight;

            _MeshHillshadeCache = new double[Count];

            double CentreLat = (MinLat + MaxLat) * 0.5;

            double ZScale = ComputeHillshadeZScale();



            for (int Sy = 0; Sy < SampleHeight; Sy++)

            {

                for (int Sx = 0; Sx < SampleWidth; Sx++)

                {

                    int Idx = Sy * SampleWidth + Sx;

                    if (!Valid[Idx])

                    {

                        _MeshHillshadeCache[Idx] = double.NaN;

                        continue;

                    }



                    double Lon = MinLon + (MaxLon - MinLon) * Sx / Math.Max(1, SampleWidth - 1);

                    double Lat = MaxLat - (MaxLat - MinLat) * Sy / Math.Max(1, SampleHeight - 1);

                    _MeshHillshadeCache[Idx] = HillshadeRenderer.ComputeFactorAtLocation(

                        this, ContentView, CentreLat, Lat, Lon, ZScale);

                }

            }



            _MeshHillshadeView = ContentView;

        }



        /// <summary>

        /// Computes vertical exaggeration for 2D hillshade so sub-metre relief is visible.

        /// </summary>

        /// <returns>Z scale factor relating horizontal extent to elevation range.</returns>

        public double ComputeHillshadeZScale

            (

            )

        {

            double CentreLat = (MinLat + MaxLat) * 0.5;

            double SpanMetresLat = (MaxLat - MinLat) * 111320.0;

            double SpanMetresLon = (MaxLon - MinLon) * 111320.0 * Math.Cos(CentreLat * Math.PI / 180.0);

            double HorizontalExtentM = Math.Max(SpanMetresLat, SpanMetresLon);

            double ElevationRangeM = Math.Max(MaxElevation - MinElevation, 1e-6);

            // Scale Z relative to horizontal span so sub-metre relief is visible in hillshade.
            return HorizontalExtentM * 0.25 / ElevationRangeM;

        }



        /// <summary>

        /// Returns cached or freshly extracted contour polylines grouped by elevation level.

        /// </summary>

        /// <param name="ContentView">Scalar channel to contour.</param>

        /// <param name="VMin">Minimum value of the display range.</param>

        /// <param name="VMax">Maximum value of the display range.</param>

        /// <returns>Level and polyline pairs.</returns>

        public IReadOnlyList<(double Level, IReadOnlyList<(double Lat, double Lon)> Points)> GetContourPolylinesByLevel

            (

            FieldContentView ContentView,

            double VMin,

            double VMax

            )

        {

            if (_ContourCacheByLevel != null

                && _ContourCacheView == ContentView

                && _ContourCacheVMin == VMin

                && _ContourCacheVMax == VMax)

            {

                return _ContourCacheByLevel;

            }



            // Contour extraction is expensive; cache by view and display range.
            _ContourCacheByLevel = ContourRenderer.ExtractPolylinesByLevel(this, ContentView, VMin, VMax);

            _ContourCacheView = ContentView;

            _ContourCacheVMin = VMin;

            _ContourCacheVMax = VMax;

            return _ContourCacheByLevel;

        }



        /// <summary>

        /// Returns contour polylines flattened across all elevation levels.

        /// </summary>

        /// <param name="ContentView">Scalar channel to contour.</param>

        /// <param name="VMin">Minimum value of the display range.</param>

        /// <param name="VMax">Maximum value of the display range.</param>

        /// <returns>Flat list of contour polylines.</returns>

        public IReadOnlyList<IReadOnlyList<(double Lat, double Lon)>> GetContourPolylines

            (

            FieldContentView ContentView,

            double VMin,

            double VMax

            )

        {

            List<IReadOnlyList<(double Lat, double Lon)>> Flat = new List<IReadOnlyList<(double Lat, double Lon)>>();

            // Flatten level-grouped polylines for callers that ignore elevation bands.
            foreach ((double _, IReadOnlyList<(double Lat, double Lon)> Points) in GetContourPolylinesByLevel(ContentView, VMin, VMax))

                Flat.Add(Points);

            return Flat;

        }



        /// <summary>

        /// Rebuilds the scalar field from a project document.

        /// </summary>

        /// <param name="Project">Project containing survey and optional design data.</param>

        /// <param name="Resolution">Sample grid resolution per axis.</param>

        public void BuildFromProject

            (

            ProjectDocument? Project,

            int Resolution = DEFAULT_RESOLUTION

            )

        {

            if (Project == null || !Project.HasSurvey)

            {

                Clear();

                return;

            }



            // Prefer design grid sampling when a solved design is present.
            if (Project.HasDesign && Project.DesignGrid != null)

            {

                BuildFromGrid(Project.DesignGrid, Project.Survey, Resolution);

                return;

            }



            BuildFromSurvey(Project.Survey!, Resolution);

        }



        /// <summary>

        /// Clears all samples, caches, and extent metadata.

        /// </summary>

        public void Clear

            (

            )

        {

            SampleWidth = 0;

            SampleHeight = 0;

            Existing = Array.Empty<double>();

            Proposed = Array.Empty<double>();

            Valid = Array.Empty<bool>();

            InvalidateContourCache();

            InvalidateColorMapCache();

        }



        /// <summary>

        /// Discards cached contour polylines.

        /// </summary>

        private void InvalidateContourCache

            (

            )

        {

            _ContourCacheByLevel = null;

            _ContourCacheVMin = 0;

            _ContourCacheVMax = 0;

        }



        /// <summary>

        /// Disposes and clears the cached colour map bitmap.

        /// </summary>

        private void InvalidateColorMapCache

            (

            )

        {

            _ColorMapCache?.Dispose();

            _ColorMapCache = null;

            _ColorMapBaseCache?.Dispose();

            _ColorMapBaseCache = null;

            _ColorMapHillshadeCache?.Dispose();

            _ColorMapHillshadeCache = null;

            _PlanViewShadeGrid = null;

            _MeshHillshadeCache = null;

            _ColorMapVMin = 0;

            _ColorMapVMax = 0;

            _ColorMapHillshade = false;

        }



        /// <summary>

        /// Bilinearly samples existing elevation at a geographic location.

        /// </summary>

        /// <param name="Lat">Sample latitude in decimal degrees.</param>

        /// <param name="Lon">Sample longitude in decimal degrees.</param>

        /// <returns>Existing elevation in metres, or NaN when invalid.</returns>

        public double SampleExisting

            (

            double Lat,

            double Lon

            )

        {

            // Delegate to shared bilinear field sampling for existing elevations.
            return SampleField(Existing, Lat, Lon);

        }



        /// <summary>

        /// Bilinearly samples proposed elevation at a geographic location.

        /// </summary>

        /// <param name="Lat">Sample latitude in decimal degrees.</param>

        /// <param name="Lon">Sample longitude in decimal degrees.</param>

        /// <returns>Proposed elevation in metres, or NaN when invalid.</returns>

        public double SampleProposed

            (

            double Lat,

            double Lon

            )

        {

            // Delegate to shared bilinear field sampling for proposed elevations.
            return SampleField(Proposed, Lat, Lon);

        }



        /// <summary>

        /// Returns existing minus proposed elevation at a geographic location.

        /// </summary>

        /// <param name="Lat">Sample latitude in decimal degrees.</param>

        /// <param name="Lon">Sample longitude in decimal degrees.</param>

        /// <returns>Cut/fill depth in metres, or NaN when either sample is invalid.</returns>

        public double SampleCutFill

            (

            double Lat,

            double Lon

            )

        {

            double E = SampleExisting(Lat, Lon);

            double P = SampleProposed(Lat, Lon);

            if (double.IsNaN(E) || double.IsNaN(P))

                return double.NaN;

            // Cut/fill depth is existing minus proposed at the sample location.
            return E - P;

        }



        /// <summary>

        /// Samples the scalar field for the requested content view at a geographic location.

        /// </summary>

        /// <param name="View">Scalar channel to sample.</param>

        /// <param name="Lat">Sample latitude in decimal degrees.</param>

        /// <param name="Lon">Sample longitude in decimal degrees.</param>

        /// <returns>Scalar value in metres, or NaN when invalid.</returns>

        public double SampleScalar

            (

            FieldContentView View,

            double Lat,

            double Lon

            )

        {

            // Route geographic samples through the requested content view channel.
            return View switch

            {

                FieldContentView.Proposed => SampleProposed(Lat, Lon),

                FieldContentView.CutFill => SampleCutFill(Lat, Lon),

                _ => SampleExisting(Lat, Lon)

            };

        }



        /// <summary>

        /// Bilinearly interpolates one elevation field at a geographic location.

        /// </summary>

        /// <param name="Field">Flattened elevation samples.</param>

        /// <param name="Lat">Sample latitude in decimal degrees.</param>

        /// <param name="Lon">Sample longitude in decimal degrees.</param>

        /// <returns>Interpolated elevation, or NaN when outside or invalid.</returns>

        private double SampleField

            (

            double[] Field,

            double Lat,

            double Lon

            )

        {

            if (Field.Length == 0 || SampleWidth == 0 || SampleHeight == 0)

                return double.NaN;



            // Reject samples outside the raster geographic envelope.
            if ((Lat < MinLat) || (Lat > MaxLat) || (Lon < MinLon) || (Lon > MaxLon))

                return double.NaN;



            double U = (Lon - MinLon) / (MaxLon - MinLon);

            double V = (MaxLat - Lat) / (MaxLat - MinLat);

            double Fx = U * (SampleWidth - 1);

            double Fy = V * (SampleHeight - 1);

            int X0 = (int)Math.Floor(Fx);

            int Y0 = (int)Math.Floor(Fy);

            int X1 = Math.Min(X0 + 1, SampleWidth - 1);

            int Y1 = Math.Min(Y0 + 1, SampleHeight - 1);

            double Tx = Fx - X0;

            double Ty = Fy - Y0;



            int Idx00 = Y0 * SampleWidth + X0;

            int Idx10 = Y0 * SampleWidth + X1;

            int Idx01 = Y1 * SampleWidth + X0;

            int Idx11 = Y1 * SampleWidth + X1;



            if (!Valid[Idx00] && !Valid[Idx10] && !Valid[Idx01] && !Valid[Idx11])

                return double.NaN;



            double V00 = Valid[Idx00] ? Field[Idx00] : double.NaN;

            double V10 = Valid[Idx10] ? Field[Idx10] : double.NaN;

            double V01 = Valid[Idx01] ? Field[Idx01] : double.NaN;

            double V11 = Valid[Idx11] ? Field[Idx11] : double.NaN;



            return BilinearWithFallback(V00, V10, V01, V11, Tx, Ty);

        }



        /// <summary>

        /// Performs bilinear interpolation with nearest-neighbour fallback for partial corners.

        /// </summary>

        /// <param name="V00">Bottom-left corner value.</param>

        /// <param name="V10">Bottom-right corner value.</param>

        /// <param name="V01">Top-left corner value.</param>

        /// <param name="V11">Top-right corner value.</param>

        /// <param name="Tx">Horizontal interpolation parameter.</param>

        /// <param name="Ty">Vertical interpolation parameter.</param>

        /// <returns>Interpolated value, or NaN when no valid corners exist.</returns>

        static private double BilinearWithFallback

            (

            double V00,

            double V10,

            double V01,

            double V11,

            double Tx,

            double Ty

            )

        {

            if (!double.IsNaN(V00) && !double.IsNaN(V10) && !double.IsNaN(V01) && !double.IsNaN(V11))

            {

                double Top = V00 + (V10 - V00) * Tx;

                double Bottom = V01 + (V11 - V01) * Tx;

                return Top + (Bottom - Top) * Ty;

            }



            // Average whichever corners are valid when the cell straddles a data boundary.

            double Sum = 0;

            int Count = 0;

            foreach (double V in new[] { V00, V10, V01, V11 })

            {

                if (!double.IsNaN(V))

                {

                    Sum += V;

                    Count++;

                }

            }



            return Count > 0 ? Sum / Count : double.NaN;

        }



        /// <summary>

        /// Builds the scalar field from a design grid with optional survey boundary masking.

        /// </summary>

        /// <param name="Grid">Design grid with bin elevations.</param>

        /// <param name="Survey">Optional survey for boundary clipping.</param>

        /// <param name="Resolution">Sample grid resolution per axis.</param>

        private void BuildFromGrid

            (

            DesignGrid Grid,

            SurveyModel? Survey,

            int Resolution

            )

        {

            InvalidateContourCache();

            InvalidateColorMapCache();

            SampleWidth = Resolution;

            SampleHeight = Resolution;

            Existing = new double[SampleWidth * SampleHeight];

            Proposed = new double[SampleWidth * SampleHeight];

            Valid = new bool[SampleWidth * SampleHeight];

            MinElevation = double.MaxValue;

            MaxElevation = double.MinValue;



            List<(double X, double Y)>? BoundaryRing = BuildBoundaryRing(Survey);

            ComputeExtent(BoundaryRing, Grid, Survey, out double ExtMinLat, out double ExtMaxLat, out double ExtMinLon, out double ExtMaxLon);

            MinLat = ExtMinLat;

            MaxLat = ExtMaxLat;

            MinLon = ExtMinLon;

            MaxLon = ExtMaxLon;



            if (MinLat >= MaxLat)

            {

                Clear();

                return;

            }



            // Pad extent slightly so edge samples are not clipped at the boundary.

            double LatPad = (MaxLat - MinLat) * 0.01;

            double LonPad = (MaxLon - MinLon) * 0.01;

            MinLat -= LatPad;

            MaxLat += LatPad;

            MinLon -= LonPad;

            MaxLon += LonPad;



            for (int Sy = 0; Sy < SampleHeight; Sy++)

            {

                for (int Sx = 0; Sx < SampleWidth; Sx++)

                {

                    double Lon = MinLon + (MaxLon - MinLon) * Sx / (SampleWidth - 1);

                    double Lat = MaxLat - (MaxLat - MinLat) * Sy / (SampleHeight - 1);

                    UTM.UTMCoordinate Utm = UTM.FromLatLon(Lat, Lon);



                    if (BoundaryRing != null && !BoundaryMask.IsInsideBoundary(Utm.Easting, Utm.Northing, BoundaryRing))

                        continue;



                    double Ex = (Utm.Easting - Grid.OriginEastingM) / Grid.BinSizeM - 0.5;

                    double Ey = (Utm.Northing - Grid.OriginNorthingM) / Grid.BinSizeM - 0.5;

                    if (!TrySampleGridBilinear(Grid, Ex, Ey, out double ExistingM, out double ProposedM))

                        continue;



                    int Idx = Sy * SampleWidth + Sx;

                    Existing[Idx] = ExistingM;

                    Proposed[Idx] = ProposedM;

                    Valid[Idx] = true;

                    MinElevation = Math.Min(MinElevation, ExistingM);

                    MaxElevation = Math.Max(MaxElevation, ExistingM);

                }

            }



            for (int Pass = 0; Pass < SMOOTH_PASSES; Pass++)

            {

                SmoothPass(Existing);

                SmoothPass(Proposed);

            }

        }



        /// <summary>

        /// Bilinearly samples existing and proposed elevations from design grid bin coordinates.

        /// </summary>

        /// <param name="Grid">Design grid with bin data.</param>

        /// <param name="Ex">Fractional bin easting coordinate.</param>

        /// <param name="Ey">Fractional bin northing coordinate.</param>

        /// <param name="ExistingM">Output existing elevation in metres.</param>

        /// <param name="ProposedM">Output proposed elevation in metres.</param>

        /// <returns>True when all four corner bins are present and valid.</returns>

        static private bool TrySampleGridBilinear

            (

            DesignGrid Grid,

            double Ex,

            double Ey,

            out double ExistingM,

            out double ProposedM

            )

        {

            ExistingM = double.NaN;

            ProposedM = double.NaN;

            int X0 = (int)Math.Floor(Ex);

            int Y0 = (int)Math.Floor(Ey);

            int X1 = X0 + 1;

            int Y1 = Y0 + 1;

            double Tx = Ex - X0;

            double Ty = Ey - Y0;



            // Require all four corner bins so bilinear interpolation stays well defined.
            if (!TryGetBin(Grid, X0, Y0, out GridBinData B00) ||

                !TryGetBin(Grid, X1, Y0, out GridBinData B10) ||

                !TryGetBin(Grid, X0, Y1, out GridBinData B01) ||

                !TryGetBin(Grid, X1, Y1, out GridBinData B11))

                return false;



            ExistingM = Bilinear(B00.ExistingM, B10.ExistingM, B01.ExistingM, B11.ExistingM, Tx, Ty);

            ProposedM = Bilinear(B00.ProposedM, B10.ProposedM, B01.ProposedM, B11.ProposedM, Tx, Ty);

            return true;

        }



        /// <summary>

        /// Looks up one valid design grid bin by integer coordinates.

        /// </summary>

        /// <param name="Grid">Design grid with bin data.</param>

        /// <param name="X">Bin column index.</param>

        /// <param name="Y">Bin row index.</param>

        /// <param name="Bin">Output bin data when found.</param>

        /// <returns>True when a valid bin exists at the coordinates.</returns>

        static private bool TryGetBin

            (

            DesignGrid Grid,

            int X,

            int Y,

            out GridBinData Bin

            )

        {

            Bin = default!;

            // Linear scan matches sparse bin storage used by the design grid model.
            GridBinData? Found = Grid.Bins.Find(B => B.Valid && B.X == X && B.Y == Y);

            if (Found == null)

                return false;

            Bin = Found;

            return true;

        }



        /// <summary>

        /// Standard bilinear interpolation across four corner values.

        /// </summary>

        /// <param name="V00">Bottom-left corner value.</param>

        /// <param name="V10">Bottom-right corner value.</param>

        /// <param name="V01">Top-left corner value.</param>

        /// <param name="V11">Top-right corner value.</param>

        /// <param name="Tx">Horizontal interpolation parameter.</param>

        /// <param name="Ty">Vertical interpolation parameter.</param>

        /// <returns>Interpolated value.</returns>

        static private double Bilinear

            (

            double V00,

            double V10,

            double V01,

            double V11,

            double Tx,

            double Ty

            )

        {

            double Top = V00 + (V10 - V00) * Tx;

            double Bottom = V01 + (V11 - V01) * Tx;

            // Standard bilinear blend along X then Y.
            return Top + (Bottom - Top) * Ty;

        }



        /// <summary>

        /// Builds the scalar field from survey points using regular-grid or IDW interpolation.

        /// </summary>

        /// <param name="Survey">Survey model with topology points.</param>

        /// <param name="Resolution">Sample grid resolution per axis.</param>

        private void BuildFromSurvey

            (

            SurveyModel Survey,

            int Resolution

            )

        {

            List<(double E, double N, double Z)> Points = new List<(double E, double N, double Z)>();

            foreach (TopologyPoint Pt in Survey.InteriorPoints)

            {

                UTM.UTMCoordinate Utm = UTM.FromLatLon(Pt.Latitude, Pt.Longitude);

                Points.Add((Utm.Easting, Utm.Northing, Pt.ExistingElevation));

            }



            foreach (TopologyPoint Pt in Survey.BoundaryPoints)

            {

                UTM.UTMCoordinate Utm = UTM.FromLatLon(Pt.Latitude, Pt.Longitude);

                Points.Add((Utm.Easting, Utm.Northing, Pt.ExistingElevation));

            }



            if (Points.Count == 0)

            {

                Clear();

                return;

            }



            InvalidateContourCache();

            InvalidateColorMapCache();

            List<(double X, double Y)>? BoundaryRing = BuildBoundaryRing(Survey);

            ComputeExtent(BoundaryRing, null, Survey, out double ExtMinLat, out double ExtMaxLat, out double ExtMinLon, out double ExtMaxLon);

            MinLat = ExtMinLat;

            MaxLat = ExtMaxLat;

            MinLon = ExtMinLon;

            MaxLon = ExtMaxLon;



            double LatPad = Math.Max(0.00001, (MaxLat - MinLat) * 0.02);

            double LonPad = Math.Max(0.00001, (MaxLon - MinLon) * 0.02);

            MinLat -= LatPad;

            MaxLat += LatPad;

            MinLon -= LonPad;

            MaxLon += LonPad;



            SampleWidth = Resolution;

            SampleHeight = Resolution;

            Existing = new double[SampleWidth * SampleHeight];

            Proposed = new double[SampleWidth * SampleHeight];

            Valid = new bool[SampleWidth * SampleHeight];

            MinElevation = double.MaxValue;

            MaxElevation = double.MinValue;



            // Prefer fast regular-grid sampling when interior points form a complete grid.

            RegularSurveyGrid? RegularGrid = null;

            RegularSurveyGrid.TryBuild(Survey, out RegularGrid);

            SurveySpatialInterpolator? Interpolator = RegularGrid == null ? new SurveySpatialInterpolator(Points) : null;



            for (int Sy = 0; Sy < SampleHeight; Sy++)

            {

                for (int Sx = 0; Sx < SampleWidth; Sx++)

                {

                    double Lon = MinLon + (MaxLon - MinLon) * Sx / (SampleWidth - 1);

                    double Lat = MaxLat - (MaxLat - MinLat) * Sy / (SampleHeight - 1);



                    if (BoundaryRing != null)

                    {

                        UTM.UTMCoordinate Utm = UTM.FromLatLon(Lat, Lon);

                        if (!BoundaryMask.IsInsideBoundary(Utm.Easting, Utm.Northing, BoundaryRing))

                            continue;

                    }



                    double Elev;

                    if (RegularGrid != null)

                        Elev = RegularGrid.Sample(Lat, Lon);

                    else

                    {

                        UTM.UTMCoordinate Utm = UTM.FromLatLon(Lat, Lon);

                        Elev = Interpolator!.Interpolate(Utm.Easting, Utm.Northing);

                    }

                    if (double.IsNaN(Elev))

                        continue;



                    int Idx = Sy * SampleWidth + Sx;

                    Existing[Idx] = Elev;

                    Proposed[Idx] = Elev;

                    Valid[Idx] = true;

                    MinElevation = Math.Min(MinElevation, Elev);

                    MaxElevation = Math.Max(MaxElevation, Elev);

                }

            }



            for (int Pass = 0; Pass < SMOOTH_PASSES; Pass++)

                SmoothPass(Existing);

        }



        /// <summary>

        /// Converts survey boundary points to a UTM polygon ring for clipping.

        /// </summary>

        /// <param name="Survey">Survey model with boundary points.</param>

        /// <returns>UTM ring, or null when fewer than three boundary points exist.</returns>

        static private List<(double X, double Y)>? BuildBoundaryRing

            (

            SurveyModel? Survey

            )

        {

            if (Survey == null || Survey.BoundaryPoints.Count < 3)

                return null;



            List<(double Lat, double Lon)> LatLon = new List<(double Lat, double Lon)>();

            foreach (TopologyPoint Pt in Survey.BoundaryPoints)

                LatLon.Add((Pt.Latitude, Pt.Longitude));

            // Convert geographic boundary vertices to a UTM ring for clipping tests.
            return BoundaryMask.BuildUtmRing(LatLon);

        }



        /// <summary>

        /// Computes geographic extent from boundary, grid, or interior survey points.

        /// </summary>

        /// <param name="BoundaryRing">Optional UTM boundary ring (unused for extent).</param>

        /// <param name="Grid">Optional design grid.</param>

        /// <param name="Survey">Optional survey model.</param>

        /// <param name="MinLat">Output minimum latitude.</param>

        /// <param name="MaxLat">Output maximum latitude.</param>

        /// <param name="MinLon">Output minimum longitude.</param>

        /// <param name="MaxLon">Output maximum longitude.</param>

        static private void ComputeExtent

            (

            List<(double X, double Y)>? BoundaryRing,

            DesignGrid? Grid,

            SurveyModel? Survey,

            out double MinLat,

            out double MaxLat,

            out double MinLon,

            out double MaxLon

            )

        {

            MinLat = double.MaxValue;

            MaxLat = double.MinValue;

            MinLon = double.MaxValue;

            MaxLon = double.MinValue;



            // Prefer boundary extent, then design bins, then interior survey points.
            if (Survey != null && Survey.BoundaryPoints.Count >= 3)

            {

                foreach (TopologyPoint Pt in Survey.BoundaryPoints)

                {

                    MinLat = Math.Min(MinLat, Pt.Latitude);

                    MaxLat = Math.Max(MaxLat, Pt.Latitude);

                    MinLon = Math.Min(MinLon, Pt.Longitude);

                    MaxLon = Math.Max(MaxLon, Pt.Longitude);

                }

                return;

            }



            if (Grid != null)

            {

                foreach (GridBinData Bin in Grid.Bins)

                {

                    if (!Bin.Valid)

                        continue;

                    MinLat = Math.Min(MinLat, Bin.CentroidLat);

                    MaxLat = Math.Max(MaxLat, Bin.CentroidLat);

                    MinLon = Math.Min(MinLon, Bin.CentroidLon);

                    MaxLon = Math.Max(MaxLon, Bin.CentroidLon);

                }

                return;

            }



            if (Survey != null)

            {

                foreach (TopologyPoint Pt in Survey.InteriorPoints)

                {

                    MinLat = Math.Min(MinLat, Pt.Latitude);

                    MaxLat = Math.Max(MaxLat, Pt.Latitude);

                    MinLon = Math.Min(MinLon, Pt.Longitude);

                    MaxLon = Math.Max(MaxLon, Pt.Longitude);

                }

            }

        }



        /// <summary>

        /// Applies one 3×3 box smooth pass to a scalar field, respecting validity.

        /// </summary>

        /// <param name="Field">Flattened scalar samples to smooth in place.</param>

        private void SmoothPass

            (

            double[] Field

            )

        {

            double[] Copy = (double[])Field.Clone();

            // Box-smooth interior valid cells while preserving invalid boundary samples.
            for (int Y = 1; Y < SampleHeight - 1; Y++)

            {

                for (int X = 1; X < SampleWidth - 1; X++)

                {

                    int Idx = Y * SampleWidth + X;

                    if (!Valid[Idx] || double.IsNaN(Copy[Idx]))

                        continue;



                    double Sum = 0;

                    int Count = 0;

                    for (int Dy = -1; Dy <= 1; Dy++)

                    {

                        for (int Dx = -1; Dx <= 1; Dx++)

                        {

                            int NeighbourIdx = (Y + Dy) * SampleWidth + (X + Dx);

                            if (!Valid[NeighbourIdx] || double.IsNaN(Copy[NeighbourIdx]))

                                continue;

                            Sum += Copy[NeighbourIdx];

                            Count++;

                        }

                    }



                    if (Count > 0)

                        Field[Idx] = Sum / Count;

                }

            }

        }



        /// <summary>

        /// Computes min and max scalar values across valid samples for a content view.

        /// </summary>

        /// <param name="View">Scalar channel to measure.</param>

        /// <param name="VMin">Output minimum value.</param>

        /// <param name="VMax">Output maximum value.</param>

        public void ComputeScalarRange

            (

            FieldContentView View,

            out double VMin,

            out double VMax

            )

        {

            VMin = double.MaxValue;

            VMax = double.MinValue;



            // Scan flattened samples for the active scalar channel range.
            for (int I = 0; I < Existing.Length; I++)

            {

                if (!Valid[I])

                    continue;



                double Value = View switch

                {

                    FieldContentView.Proposed => Proposed[I],

                    FieldContentView.CutFill => Existing[I] - Proposed[I],

                    _ => Existing[I]

                };



                if (double.IsNaN(Value))

                    continue;



                VMin = Math.Min(VMin, Value);

                VMax = Math.Max(VMax, Value);

            }



            if (VMin > VMax)

            {

                VMin = 0;

                VMax = 0;

            }

        }



        /// <summary>

        /// Computes display range directly from project data without building the full raster.

        /// </summary>

        /// <param name="Project">Project containing survey or design data.</param>

        /// <param name="View">Scalar channel to measure.</param>

        /// <param name="VMin">Output minimum value.</param>

        /// <param name="VMax">Output maximum value.</param>

        static public void ComputeScalarRangeFromProject

            (

            ProjectDocument? Project,

            FieldContentView View,

            out double VMin,

            out double VMax

            )

        {

            VMin = double.MaxValue;

            VMax = double.MinValue;



            if (Project == null || !Project.HasSurvey)

            {

                VMin = 0;

                VMax = 0;

                return;

            }



            // Use bin aggregates for design views; fall back to raw survey elevations otherwise.
            if (Project.HasDesign && Project.DesignGrid != null &&

                (View == FieldContentView.Proposed || View == FieldContentView.CutFill))

            {

                foreach (GridBinData Bin in Project.DesignGrid.Bins)

                {

                    if (!Bin.Valid)

                        continue;



                    double Value = View switch

                    {

                        FieldContentView.Proposed => Bin.ProposedM,

                        FieldContentView.CutFill => Bin.ExistingM - Bin.ProposedM,

                        _ => Bin.ExistingM

                    };



                    if (double.IsNaN(Value))

                        continue;



                    VMin = Math.Min(VMin, Value);

                    VMax = Math.Max(VMax, Value);

                }

            }

            else

            {

                foreach (TopologyPoint Pt in Project.Survey!.InteriorPoints)

                    IncludeElevation(Pt.ExistingElevation, ref VMin, ref VMax);

                foreach (TopologyPoint Pt in Project.Survey.BoundaryPoints)

                    IncludeElevation(Pt.ExistingElevation, ref VMin, ref VMax);

            }



            if (VMin > VMax)

            {

                VMin = 0;

                VMax = 0;

            }

        }



        /// <summary>

        /// Expands a scalar range to include one elevation sample when finite.

        /// </summary>

        /// <param name="Value">Elevation sample in metres.</param>

        /// <param name="VMin">Current minimum, updated in place.</param>

        /// <param name="VMax">Current maximum, updated in place.</param>

        static private void IncludeElevation

            (

            double Value,

            ref double VMin,

            ref double VMax

            )

        {

            if (double.IsNaN(Value))

                return;



            // Expand running min/max only for finite elevation samples.
            VMin = Math.Min(VMin, Value);

            VMax = Math.Max(VMax, Value);

        }

    }

}


