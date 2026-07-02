using AgGradeDesigner.Core;
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

        /// <summary>Default sample grid resolution per axis when none is specified.</summary>
        private const int DEFAULT_RESOLUTION = 256;

        /// <summary>Box-blur passes applied after the scalar field is built.</summary>
        private const int SMOOTH_PASSES = 2;

        /// <summary>Passes that pull sparse-expansion fringe elevations toward interior neighbours.</summary>
        private const int SPARSE_FRINGE_SMOOTH_PASSES = 6;



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



        /// <summary>Field boundary vertices in geographic coordinates for contour clipping.</summary>

        public IReadOnlyList<(double Lat, double Lon)>? BoundaryLatLon { get; private set; }



        /// <summary>Inpaint passes applied while building the high-resolution contour grid.</summary>
        private const int CONTOUR_INPAINT_PASSES = 8;

        /// <summary>Smoothing passes applied to the fine contour sampling grid.</summary>
        private const int CONTOUR_FINE_SMOOTH_PASSES = 2;

        /// <summary>Progress value reported when a pipeline stage completes.</summary>
        private const int PROGRESS_COMPLETE_PERCENT = 100;

        /// <summary>Blur radius in cells for cached 3D mesh hillshade factors.</summary>
        private const int MESH_HILLSHADE_BLUR_RADIUS = 2;

        /// <summary>Vertical exaggeration fraction used when computing mesh hillshade.</summary>
        private const double HILLSHADE_Z_SCALE_FRACTION = 0.25;

        /// <summary>Minimum elevation span treated as non-degenerate for display scaling.</summary>
        private const double MIN_ELEVATION_RANGE_M = 1e-6;

        /// <summary>Fractional pad added around design-grid geographic extent.</summary>
        private const double GRID_EXTENT_PAD_FRACTION = 0.01;

        /// <summary>Fractional pad added around survey-only geographic extent.</summary>
        private const double SURVEY_EXTENT_PAD_FRACTION = 0.02;

        /// <summary>Minimum lat/lon pad in degrees when expanding sample extent.</summary>
        private const double MIN_EXTENT_PAD_DEGREES = 0.00001;

        /// <summary>Valid-cell ratio below which sparse expansion inpainting is enabled.</summary>
        private const double SPARSE_FILL_RATIO_THRESHOLD = 0.5;

        /// <summary>Design bin count above which survey points are rasterized instead of IDW-sampled.</summary>
        private const int LARGE_GRID_BIN_THRESHOLD = 1000;

        /// <summary>Neighbour-expansion passes for filling sparse survey rasters.</summary>
        private const int SPARSE_EXPANSION_PASSES = 12;

        /// <summary>Weight applied when averaging duplicate samples at the same raster cell.</summary>
        private const double DUPLICATE_SAMPLE_AVERAGE_FACTOR = 0.5;

        /// <summary>Closing morphology passes applied to the observed boundary mask.</summary>
        private const int MORPHOLOGY_CLOSE_PASSES = 2;

        /// <summary>Maximum propagation passes when expanding the observed boundary mask.</summary>
        private const int OBSERVED_MASK_PROPAGATION_MAX_PASSES = 256;

        /// <summary>Maximum inpaint passes when filling interior holes in the valid mask.</summary>
        private const int INTERIOR_HOLE_FILL_MAX_PASSES = 64;

        /// <summary>Minimum valid neighbours required before sparse expansion accepts a cell.</summary>
        private const int MIN_EXPAND_NEIGHBOUR_COUNT = 2;

        /// <summary>Minimum boundary vertex count required to build a clipping ring.</summary>
        private const int MIN_BOUNDARY_VERTEX_COUNT = 3;

        /// <summary>Minimum grid width and height required for quad validity tests.</summary>
        private const int MIN_VALID_QUAD_GRID_SIZE = 2;

        /// <summary>Number of neighbours considered in eight-connected morphology and expansion.</summary>
        private const int EIGHT_CONNECTED_NEIGHBOR_COUNT = 8;

        /// <summary>Divisor mapping completed work units to coarse progress percentages.</summary>
        private const int PROGRESS_REPORT_DIVISOR = 100;

        /// <summary>Divisor for progress updates during large survey rasterization.</summary>
        private const int SURVEY_RASTERIZE_REPORT_DIVISOR = 10;

        /// <summary>Progress percent reached when plan-view colour map generation finishes.</summary>
        private const int PROGRESS_COLORMAP_END_PERCENT = 40;

        /// <summary>Progress percent reached when contour extraction finishes.</summary>
        private const int PROGRESS_CONTOUR_END_PERCENT = 95;

        /// <summary>Progress percent reached when design-grid sampling finishes.</summary>
        private const int PROGRESS_GRID_SAMPLE_END_PERCENT = 90;

        /// <summary>Progress percent reached when survey IDW sampling finishes.</summary>
        private const int PROGRESS_SURVEY_SAMPLE_END_PERCENT = 85;

        /// <summary>Progress percent reached when survey rasterization finishes.</summary>
        private const int PROGRESS_SURVEY_RASTERIZE_END_PERCENT = 70;

        /// <summary>Progress percent reached when contour-grid inpainting finishes.</summary>
        private const int PROGRESS_CONTOUR_INPAINT_END_PERCENT = 60;

        /// <summary>Progress percent at which contour fine-grid smoothing begins.</summary>
        private const int PROGRESS_CONTOUR_SMOOTH_START_PERCENT = 80;

        /// <summary>Progress percent at which post-build scalar smoothing begins.</summary>
        private const int PROGRESS_SMOOTH_START_PERCENT = 90;

        /// <summary>Per-content-view plan-view and mesh hillshade caches.</summary>
        private readonly ViewDisplayCacheStore _ViewDisplayCaches = new ViewDisplayCacheStore();



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

        /// <param name="Progress">Optional progress reporter for colour-map generation.</param>

        /// <param name="SupersampleFactor">Supersample factor forwarded to <see cref="PlanViewColorMapBuilder"/>.</param>

        /// <returns>RGBA bitmap for the scalar field.</returns>

        public SKBitmap GetPlanViewColorMap

            (

            FieldContentView ContentView,

            double VMin,

            double VMax,

            bool ApplyHillshade,

            IProgress<int>? Progress = null,

            int SupersampleFactor = PlanViewColorMapBuilder.FullSupersampleFactor

            )

        {

            EnsurePlanViewBaseCache(ContentView, VMin, VMax, Progress, SupersampleFactor);

            ViewDisplayCache Cache = _ViewDisplayCaches.Get(ContentView);

            if (!ApplyHillshade)

                return Cache.ColorMapBase!;



            EnsurePlanViewHillshadeCache(ContentView);

            return Cache.ColorMapHillshadeCache!;

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

            ViewDisplayCache Cache = _ViewDisplayCaches.Get(ContentView);
            if (Cache.MeshHillshadeCache == null || GridIndex < 0 || GridIndex >= Cache.MeshHillshadeCache.Length)
                return double.NaN;

            return Cache.MeshHillshadeCache[GridIndex];

        }



        /// <summary>

        /// Builds or returns the base (unshaded) plan-view colour map cache.

        /// </summary>

        private void EnsurePlanViewBaseCache

            (

            FieldContentView ContentView,

            double VMin,

            double VMax,

            IProgress<int>? Progress = null,

            int SupersampleFactor = PlanViewColorMapBuilder.FullSupersampleFactor

            )

        {

            ViewDisplayCache Cache = _ViewDisplayCaches.Get(ContentView);

            if (Cache.ColorMapBase != null

                && Cache.ColorMapVMin == VMin

                && Cache.ColorMapVMax == VMax

                && Cache.ColorMapSupersample >= SupersampleFactor)

            {

                Progress?.Report(PROGRESS_COMPLETE_PERCENT);

                return;

            }



            Cache.InvalidateColorMaps();

            Cache.ColorMapBase = PlanViewColorMapBuilder.Build(

                this, ContentView, VMin, VMax, ApplyHillshade: false, Progress, SupersampleFactor);

            Cache.ColorMapVMin = VMin;

            Cache.ColorMapVMax = VMax;

            Cache.ColorMapSupersample = SupersampleFactor;

        }



        /// <summary>

        /// Composes the hillshade-modulated colour map from cached base colours and shade grid.

        /// </summary>

        private void EnsurePlanViewHillshadeCache

            (

            FieldContentView ContentView

            )

        {

            ViewDisplayCache Cache = _ViewDisplayCaches.Get(ContentView);

            if (Cache.ColorMapHillshadeCache != null)

                return;



            if (Cache.PlanViewShadeGrid == null)

            {

                (Cache.PlanViewShadeGrid, _, _) = PlanViewColorMapBuilder.BuildShadeGrid(

                    this, ContentView, Cache.ColorMapSupersample);

            }



            Cache.ColorMapHillshadeCache?.Dispose();

            Cache.ColorMapHillshadeCache = PlanViewColorMapBuilder.ApplyHillshadeGrid(Cache.ColorMapBase!, Cache.PlanViewShadeGrid);

        }



        /// <summary>

        /// Precomputes per-vertex hillshade factors on the sample grid for 3D rendering.

        /// </summary>

        private void EnsureMeshHillshadeCache

            (

            FieldContentView ContentView

            )

        {

            ViewDisplayCache Cache = _ViewDisplayCaches.Get(ContentView);

            if (Cache.MeshHillshadeCache != null)

                return;



            int Count = SampleWidth * SampleHeight;

            Cache.MeshHillshadeCache = new double[Count];

            double CentreLat = (MinLat + MaxLat) * FieldConstants.EXTENT_MIDPOINT_FRACTION;

            double ZScale = ComputeHillshadeZScale();



            for (int Sy = 0; Sy < SampleHeight; Sy++)

            {

                for (int Sx = 0; Sx < SampleWidth; Sx++)

                {

                    int Idx = Sy * SampleWidth + Sx;

                    if (!Valid[Idx])

                    {

                        Cache.MeshHillshadeCache[Idx] = double.NaN;

                        continue;

                    }



                    double Lon = MinLon + (MaxLon - MinLon) * Sx / Math.Max(1, SampleWidth - 1);

                    double Lat = MaxLat - (MaxLat - MinLat) * Sy / Math.Max(1, SampleHeight - 1);

                    Cache.MeshHillshadeCache[Idx] = HillshadeRenderer.ComputeFactorAtLocation(

                        this, ContentView, CentreLat, Lat, Lon, ZScale);

                }

            }

            HillshadeRenderer.BoxBlurShadeGrid(Cache.MeshHillshadeCache, SampleWidth, SampleHeight, Radius: MESH_HILLSHADE_BLUR_RADIUS);

        }



        /// <summary>

        /// Computes vertical exaggeration for 2D hillshade so sub-metre relief is visible.

        /// </summary>

        /// <returns>Z scale factor relating horizontal extent to elevation range.</returns>

        public double ComputeHillshadeZScale

            (

            )

        {

            double CentreLat = (MinLat + MaxLat) * FieldConstants.EXTENT_MIDPOINT_FRACTION;

            double SpanMetresLat = (MaxLat - MinLat) * FieldConstants.METRES_PER_DEGREE_LAT;

            double SpanMetresLon = (MaxLon - MinLon) * FieldConstants.METRES_PER_DEGREE_LAT * Math.Cos(CentreLat * Math.PI / FieldConstants.DEGREES_PER_RADIAN);

            double HorizontalExtentM = Math.Max(SpanMetresLat, SpanMetresLon);

            double ElevationRangeM = Math.Max(MaxElevation - MinElevation, MIN_ELEVATION_RANGE_M);

            // Scale Z relative to horizontal span so sub-metre relief is visible in hillshade.
            return HorizontalExtentM * HILLSHADE_Z_SCALE_FRACTION / ElevationRangeM;

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

            double VMax,

            IProgress<int>? Progress = null

            )

        {

            ViewDisplayCache Cache = _ViewDisplayCaches.Get(ContentView);

            if (Cache.ContourCacheByLevel != null

                && Cache.ContourCacheVMin == VMin

                && Cache.ContourCacheVMax == VMax)

            {

                Progress?.Report(PROGRESS_COMPLETE_PERCENT);

                return Cache.ContourCacheByLevel;

            }



            // Contour extraction is expensive; cache by view and display range.
            Cache.ContourCacheByLevel = ContourRenderer.ExtractPolylinesByLevel(this, ContentView, VMin, VMax, Progress);

            Cache.ContourCacheVMin = VMin;

            Cache.ContourCacheVMax = VMax;

            return Cache.ContourCacheByLevel;

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

        /// Builds a supersampled, inpainted scalar height grid for contour extraction.

        /// </summary>

        /// <param name="ContentView">Scalar channel to contour.</param>

        /// <param name="ResolutionMultiplier">Upsample factor relative to the surface raster.</param>

        /// <param name="Heights">Output height grid indexed [row, column].</param>

        /// <param name="InsideField">Output mask of cells inside the field boundary.</param>

        public void BuildContourFineGrid

            (

            FieldContentView ContentView,

            int ResolutionMultiplier,

            double[,] Heights,

            bool[,] InsideField,

            IProgress<int>? Progress = null

            )

        {

            int FineHeight = Heights.GetLength(0);

            int FineWidth = Heights.GetLength(1);

            double[] Field = CopyScalarField(ContentView);

            List<(double X, double Y)>? UtmRing = BoundaryLatLon != null

                ? BoundaryMask.BuildUtmRing(BoundaryLatLon)

                : null;



            Parallel.For(0, FineHeight, Y =>
            {
                for (int X = 0; X < FineWidth; X++)
                {
                    double Lon = MinLon + (MaxLon - MinLon) * X / Math.Max(1, FineWidth - 1);
                    double Lat = MaxLat - (MaxLat - MinLat) * Y / Math.Max(1, FineHeight - 1);
                    bool Inside = IsContourCellInside(Lat, Lon, UtmRing);
                    InsideField[Y, X] = Inside;
                    Heights[Y, X] = Inside ? SampleScalarFieldBilinear(Field, Lat, Lon) : double.NaN;
                }

                ProgressMapper.ReportSegment(Progress, 0, PROGRESS_CONTOUR_INPAINT_END_PERCENT, (Y + 1) * PROGRESS_COMPLETE_PERCENT / FineHeight);
            });



            for (int Pass = 0; Pass < CONTOUR_INPAINT_PASSES; Pass++)

            {

                InpaintContourGridPass(Heights, InsideField, FineWidth, FineHeight);

                ProgressMapper.ReportSegment(Progress, PROGRESS_CONTOUR_INPAINT_END_PERCENT, PROGRESS_CONTOUR_SMOOTH_START_PERCENT, (Pass + 1) * PROGRESS_COMPLETE_PERCENT / CONTOUR_INPAINT_PASSES);

            }



            for (int Pass = 0; Pass < CONTOUR_FINE_SMOOTH_PASSES; Pass++)

            {

                SmoothContourGridPass(Heights, InsideField, FineWidth, FineHeight);

                ProgressMapper.ReportSegment(Progress, PROGRESS_CONTOUR_SMOOTH_START_PERCENT, PROGRESS_COMPLETE_PERCENT, (Pass + 1) * PROGRESS_COMPLETE_PERCENT / CONTOUR_FINE_SMOOTH_PASSES);

            }

            Progress?.Report(PROGRESS_COMPLETE_PERCENT);

        }



        /// <summary>

        /// Rebuilds the scalar field from a project document.

        /// </summary>

        /// <param name="Project">Project containing survey and optional design data.</param>

        /// <param name="Resolution">Sample grid resolution per axis.</param>

        /// <param name="Progress">Optional 0–100 progress reporter for surface construction.</param>

        public void BuildFromProject

            (

            ProjectDocument? Project,

            int Resolution = DEFAULT_RESOLUTION,

            IProgress<int>? Progress = null

            )

        {

            if (Project == null || !Project.HasSurvey)

            {

                Clear();

                Progress?.Report(PROGRESS_COMPLETE_PERCENT);

                return;

            }



            // Prefer design grid sampling when a solved design is present.
            if (Project.HasDesign && Project.DesignGrid != null)

            {

                BuildFromGrid(Project.DesignGrid, Project.Survey, Resolution, Progress);

                return;

            }



            BuildFromSurvey(Project.Survey!, Resolution, Progress);

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

            BoundaryLatLon = null;

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

            _ViewDisplayCaches.InvalidateAllContours();

        }

        /// <summary>
        /// Precomputes colour maps, contour polylines, and mesh hillshade before frame rendering.
        /// </summary>
        /// <param name="ContentView">Active scalar field view.</param>
        /// <param name="Display">Display toggles controlling which caches to warm.</param>
        /// <param name="Progress">Optional 0–100 progress reporter.</param>
        public void PrewarmDisplayCaches
            (
            FieldContentView ContentView,
            DisplaySettings Display,
            IProgress<int>? Progress = null,
            int SupersampleFactor = PlanViewColorMapBuilder.FullSupersampleFactor,
            bool IncludeContours = true
            )
        {
            Progress?.Report(0);
            if (SampleWidth <= 0)
            {
                Progress?.Report(PROGRESS_COMPLETE_PERCENT);
                return;
            }

            if (AreDisplayCachesWarm(ContentView, Display, SupersampleFactor))
            {
                Progress?.Report(PROGRESS_COMPLETE_PERCENT);
                return;
            }

            ComputeScalarRange(ContentView, out double VMin, out double VMax);
            bool ApplyHillshade = Display.Hillshade && ContentView != FieldContentView.CutFill;
            GetPlanViewColorMap(
                ContentView,
                VMin,
                VMax,
                ApplyHillshade,
                ProgressMapper.Subrange(Progress, 0, PROGRESS_COLORMAP_END_PERCENT),
                SupersampleFactor);

            if (IncludeContours && Display.Contours && ContentView != FieldContentView.CutFill)
                GetContourPolylinesByLevel(ContentView, VMin, VMax, ProgressMapper.Subrange(Progress, PROGRESS_COLORMAP_END_PERCENT, PROGRESS_CONTOUR_END_PERCENT));

            if (ApplyHillshade)
                EnsureMeshHillshadeCache(ContentView);

            Progress?.Report(PROGRESS_COMPLETE_PERCENT);
        }

        /// <summary>
        /// Returns true when colour maps, contours, and hillshade caches are ready for the view.
        /// </summary>
        public bool AreDisplayCachesWarm
            (
            FieldContentView ContentView,
            DisplaySettings Display,
            int SupersampleFactor = PlanViewColorMapBuilder.FullSupersampleFactor
            )
        {
            if (SampleWidth <= 0)
                return false;

            ViewDisplayCache Cache = _ViewDisplayCaches.Get(ContentView);
            if (Cache.ColorMapBase == null || Cache.ColorMapSupersample < SupersampleFactor)
                return false;

            ComputeScalarRange(ContentView, out double VMin, out double VMax);
            if (Cache.ColorMapVMin != VMin || Cache.ColorMapVMax != VMax)
                return false;

            bool ApplyHillshade = Display.Hillshade && ContentView != FieldContentView.CutFill;
            if (ApplyHillshade && Cache.ColorMapHillshadeCache == null)
                return false;

            if (Display.Contours && ContentView != FieldContentView.CutFill)
            {
                if (Cache.ContourCacheByLevel == null
                    || Cache.ContourCacheVMin != VMin
                    || Cache.ContourCacheVMax != VMax)
                {
                    return false;
                }
            }

            if (ApplyHillshade && Cache.MeshHillshadeCache == null)
                return false;

            return true;
        }

        /// <summary>
        /// Rebuilds display caches at full quality when a preview-quality cache is already warm.
        /// </summary>
        public void UpgradeDisplayCacheQuality
            (
            FieldContentView ContentView,
            DisplaySettings Display,
            IProgress<int>? Progress = null
            )
        {
            ViewDisplayCache Cache = _ViewDisplayCaches.Get(ContentView);
            if (Cache.ColorMapSupersample >= PlanViewColorMapBuilder.FullSupersampleFactor)
            {
                Progress?.Report(PROGRESS_COMPLETE_PERCENT);
                return;
            }

            PrewarmDisplayCaches(
                ContentView,
                Display,
                Progress,
                PlanViewColorMapBuilder.FullSupersampleFactor,
                IncludeContours: true);
        }

        /// <summary>
        /// Precomputes display caches for existing, proposed, and cut/fill views.
        /// </summary>
        /// <param name="Display">Display toggles controlling which caches to warm.</param>
        /// <param name="HasDesign">When false, proposed and cut/fill caches are skipped.</param>
        /// <param name="Progress">Optional 0–100 progress reporter.</param>
        public void PrewarmAllFieldContentViews
            (
            DisplaySettings Display,
            bool HasDesign,
            IProgress<int>? Progress = null
            )
        {
            if (SampleWidth <= 0)
            {
                Progress?.Report(PROGRESS_COMPLETE_PERCENT);
                return;
            }

            FieldContentView[] Views = HasDesign
                ? new[] { FieldContentView.Existing, FieldContentView.Proposed, FieldContentView.CutFill }
                : new[] { FieldContentView.Existing };

            for (int I = 0; I < Views.Length; I++)
            {
                int Start = (I * PROGRESS_COMPLETE_PERCENT) / Views.Length;
                int End = ((I + 1) * PROGRESS_COMPLETE_PERCENT) / Views.Length;
                PrewarmDisplayCaches(Views[I], Display, ProgressMapper.Subrange(Progress, Start, End));
            }

            Progress?.Report(PROGRESS_COMPLETE_PERCENT);
        }

        /// <summary>

        /// Disposes and clears the cached colour map bitmap.

        /// </summary>

        private void InvalidateColorMapCache

            (

            )

        {

            _ViewDisplayCaches.InvalidateAll();

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

            int Resolution,

            IProgress<int>? Progress = null

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



            BoundaryLatLon = BuildBoundaryLatLon(Survey);

            // Design grids define their own footprint; perimeter codes from AGD imports may not enclose all bins.

            ComputeExtent(null, Grid, null, out double ExtMinLat, out double ExtMaxLat, out double ExtMinLon, out double ExtMaxLon);

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

            double LatPad = (MaxLat - MinLat) * GRID_EXTENT_PAD_FRACTION;

            double LonPad = (MaxLon - MinLon) * GRID_EXTENT_PAD_FRACTION;

            MinLat -= LatPad;

            MaxLat += LatPad;

            MinLon -= LonPad;

            MaxLon += LonPad;



            int ValidBinCount = Grid.Bins.Count(B => B.Valid);

            double FillRatio = ValidBinCount / (double)Math.Max(1, Grid.Width * Grid.Height);

            // Large AGD grids and sparse footprints both rasterize faster than per-pixel bin scans.
            bool UsedSparseExpansion = (FillRatio < SPARSE_FILL_RATIO_THRESHOLD) || (ValidBinCount >= LARGE_GRID_BIN_THRESHOLD);
            if (UsedSparseExpansion)
            {
                RasterizeGridBinCentroids(Grid, Progress);

                if (BoundaryLatLon != null)
                {
                    // Explicit boundary supplied (2PER points): expand freely then clip to it.
                    ExpandValidSurface(Passes: SPARSE_EXPANSION_PASSES);
                }
                else
                {
                    // No explicit boundary: derive the valid region tightly from the data
                    // distribution using the row/col-fill + morphological close algorithm
                    // (mirrors AgGrade's BuildValidMaskFromObservedPoints).
                    ApplyObservedBoundaryMask();
                }
            }
            else
                SampleDesignGridInBinSpace(Grid, Progress);

            if (BoundaryLatLon != null)
                ClipValidToBoundaryLatLon();

            // After clipping, sparse expansion may leave interior holes (gaps > 12 cells from any
            // survey point).  Fill them so the colormap has no transparent holes inside the field.
            if (UsedSparseExpansion)
                FillInteriorHoles();

            // Sparse inpainting can leave extrapolated fringe elevations at the boundary.
            if (UsedSparseExpansion && ValidBinCount >= LARGE_GRID_BIN_THRESHOLD)
                SmoothSparseExpansionFringe(SPARSE_FRINGE_SMOOTH_PASSES);



            for (int Pass = 0; Pass < SMOOTH_PASSES; Pass++)

            {

                SmoothPass(Existing);

                SmoothPass(Proposed);

                ProgressMapper.ReportSegment(Progress, PROGRESS_SMOOTH_START_PERCENT, PROGRESS_COMPLETE_PERCENT, (Pass + 1) * PROGRESS_COMPLETE_PERCENT / SMOOTH_PASSES);

            }

            Progress?.Report(PROGRESS_COMPLETE_PERCENT);

            RecomputeElevationExtentsFromValid();

        }



        /// <summary>

        /// Merges one bin elevation into a sample cell, averaging when multiple bins map to the same pixel.

        /// </summary>

        private void MergeSampleCell

            (

            int Idx,

            double ExistingM,

            double ProposedM

            )

        {

            if (Valid[Idx])

            {

                Existing[Idx] = (Existing[Idx] + ExistingM) * DUPLICATE_SAMPLE_AVERAGE_FACTOR;

                Proposed[Idx] = (Proposed[Idx] + ProposedM) * DUPLICATE_SAMPLE_AVERAGE_FACTOR;

                return;

            }



            Existing[Idx] = ExistingM;

            Proposed[Idx] = ProposedM;

            Valid[Idx] = true;

        }



        /// <summary>

        /// Recomputes <see cref="MinElevation"/> and <see cref="MaxElevation"/> from valid existing samples.

        /// </summary>

        private void RecomputeElevationExtentsFromValid

            (

            )

        {

            MinElevation = double.MaxValue;

            MaxElevation = double.MinValue;

            for (int Idx = 0; Idx < Valid.Length; Idx++)

            {

                if (!Valid[Idx])

                    continue;

                MinElevation = Math.Min(MinElevation, Existing[Idx]);

                MaxElevation = Math.Max(MaxElevation, Existing[Idx]);

            }



            if (MinElevation > MaxElevation)

            {

                MinElevation = 0;

                MaxElevation = 0;

            }

        }



        /// <summary>

        /// Clears surface samples outside the survey boundary polygon.

        /// </summary>

        private void ClipValidToBoundaryLatLon

            (

            )

        {

            if ((BoundaryLatLon == null) || (BoundaryLatLon.Count < MIN_BOUNDARY_VERTEX_COUNT))

                return;



            double LatSpan = MaxLat - MinLat;

            double LonSpan = MaxLon - MinLon;

            if ((LatSpan <= 0) || (LonSpan <= 0))

                return;



            int InsideCount = 0;

            for (int Sy = 0; Sy < SampleHeight; Sy++)

            {

                double Lat = MaxLat - LatSpan * Sy / Math.Max(1, SampleHeight - 1);

                for (int Sx = 0; Sx < SampleWidth; Sx++)

                {

                    int Idx = Sy * SampleWidth + Sx;

                    if (!Valid[Idx])

                        continue;



                    double Lon = MinLon + LonSpan * Sx / Math.Max(1, SampleWidth - 1);

                    if (BoundaryMask.IsInsideBoundaryLatLon(Lat, Lon, BoundaryLatLon))

                        InsideCount++;

                }

            }



            if (InsideCount == 0)

                return;



            for (int Sy = 0; Sy < SampleHeight; Sy++)

            {

                double Lat = MaxLat - LatSpan * Sy / Math.Max(1, SampleHeight - 1);

                for (int Sx = 0; Sx < SampleWidth; Sx++)

                {

                    int Idx = Sy * SampleWidth + Sx;

                    if (!Valid[Idx])

                        continue;



                    double Lon = MinLon + LonSpan * Sx / Math.Max(1, SampleWidth - 1);

                    if (!BoundaryMask.IsInsideBoundaryLatLon(Lat, Lon, BoundaryLatLon))

                        Valid[Idx] = false;

                }

            }

        }



        /// <summary>

        /// Samples a dense design grid by interpolating across UTM bin index space.

        /// </summary>

        private void SampleDesignGridInBinSpace

            (

            DesignGrid Grid,

            IProgress<int>? Progress

            )

        {

            double MaxBinX = Math.Max(1, Grid.Width - 1);

            double MaxBinY = Math.Max(1, Grid.Height - 1);



            for (int Sy = 0; Sy < SampleHeight; Sy++)

            {

                double Ey = (SampleHeight - 1 - Sy) * MaxBinY / (SampleHeight - 1);

                for (int Sx = 0; Sx < SampleWidth; Sx++)

                {

                    double Ex = Sx * MaxBinX / (SampleWidth - 1);



                    if (!TrySampleGridBilinear(Grid, Ex, Ey, out double ExistingM, out double ProposedM) &&

                        !TrySampleGridNearest(Grid, Ex, Ey, out ExistingM, out ProposedM))

                        continue;



                    int Idx = Sy * SampleWidth + Sx;

                    Existing[Idx] = ExistingM;

                    Proposed[Idx] = ProposedM;

                    Valid[Idx] = true;

                    MinElevation = Math.Min(MinElevation, ExistingM);

                    MaxElevation = Math.Max(MaxElevation, ExistingM);

                }

                ProgressMapper.ReportSegment(Progress, 0, PROGRESS_GRID_SAMPLE_END_PERCENT, (Sy + 1) * PROGRESS_COMPLETE_PERCENT / SampleHeight);

            }

        }



        /// <summary>

        /// Rasterizes sparse design grids by placing each valid bin on the nearest sample cell.

        /// </summary>

        private void RasterizeGridBinCentroids

            (

            DesignGrid Grid,

            IProgress<int>? Progress

            )

        {

            double LatSpan = MaxLat - MinLat;

            double LonSpan = MaxLon - MinLon;

            if ((LatSpan <= 0) || (LonSpan <= 0))

                return;



            int ReportStep = Math.Max(1, Grid.Bins.Count / PROGRESS_REPORT_DIVISOR);

            ProgressMapper.ReportSegment(Progress, 0, PROGRESS_GRID_SAMPLE_END_PERCENT, 0);

            for (int BinIndex = 0; BinIndex < Grid.Bins.Count; BinIndex++)

            {

                GridBinData Bin = Grid.Bins[BinIndex];

                if (!Bin.Valid)

                    continue;



                int Sx = (int)Math.Round((Bin.CentroidLon - MinLon) / LonSpan * (SampleWidth - 1));

                int Sy = (int)Math.Round((MaxLat - Bin.CentroidLat) / LatSpan * (SampleHeight - 1));

                Sx = Math.Clamp(Sx, 0, SampleWidth - 1);

                Sy = Math.Clamp(Sy, 0, SampleHeight - 1);

                int Idx = Sy * SampleWidth + Sx;

                MergeSampleCell(Idx, Bin.ExistingM, Bin.ProposedM);

                MinElevation = Math.Min(MinElevation, Bin.ExistingM);

                MaxElevation = Math.Max(MaxElevation, Bin.ExistingM);



                if ((BinIndex % ReportStep) == 0)

                    ProgressMapper.ReportSegment(Progress, 0, PROGRESS_GRID_SAMPLE_END_PERCENT, (BinIndex + 1) * PROGRESS_COMPLETE_PERCENT / Grid.Bins.Count);

            }

            ProgressMapper.ReportSegment(Progress, 0, PROGRESS_GRID_SAMPLE_END_PERCENT, PROGRESS_COMPLETE_PERCENT);

        }



        /// <summary>
        /// Computes a valid-cell mask from observed (rasterized) cells using row/column fill,
        /// morphological closing, hole filling, and largest-component selection — ported from
        /// the AgGrade reference application's BuildValidMaskFromObservedPoints algorithm.
        /// The returned mask tightly follows the data extent without requiring explicit boundary points.
        /// </summary>
        private bool[] ComputeObservedBoundaryMask()
        {
            int W = SampleWidth;
            int H = SampleHeight;
            int N = W * H;

            // Row-fill: for each row fill between leftmost and rightmost observed cell.
            bool[] RowFill = (bool[])Valid.Clone();
            for (int Y = 0; Y < H; Y++)
            {
                int Row = Y * W;
                int MinX = int.MaxValue, MaxX = int.MinValue;
                for (int X = 0; X < W; X++)
                {
                    if (!Valid[Row + X]) continue;
                    if (X < MinX) MinX = X;
                    if (X > MaxX) MaxX = X;
                }
                if (MinX <= MaxX)
                    for (int X = MinX; X <= MaxX; X++)
                        RowFill[Row + X] = true;
            }

            // Col-fill: for each column fill between topmost and bottommost observed cell.
            bool[] ColFill = (bool[])Valid.Clone();
            for (int X = 0; X < W; X++)
            {
                int MinY = int.MaxValue, MaxY = int.MinValue;
                for (int Y = 0; Y < H; Y++)
                {
                    if (!Valid[Y * W + X]) continue;
                    if (Y < MinY) MinY = Y;
                    if (Y > MaxY) MaxY = Y;
                }
                if (MinY <= MaxY)
                    for (int Y = MinY; Y <= MaxY; Y++)
                        ColFill[Y * W + X] = true;
            }

            // Envelope = row OR col fill.
            bool[] Envelope = new bool[N];
            for (int I = 0; I < N; I++)
                Envelope[I] = RowFill[I] || ColFill[I];

            // Morphological close: dilate 2 passes then erode 2 passes to smooth the boundary.
            bool[] Closed = MorphErode(MorphDilate(Envelope, W, H, MORPHOLOGY_CLOSE_PASSES), W, H, MORPHOLOGY_CLOSE_PASSES);

            // Fill holes: flood-fill from border tags exterior; remaining interior invalid cells become valid.
            bool[] Filled = MorphFillHoles(Closed, W, H);

            // Keep largest connected component only (removes stray isolated blobs).
            bool[] Main = MorphLargestComponent(Filled, W, H);

            // Always include all originally observed cells (union with data).
            for (int I = 0; I < N; I++)
                Main[I] = Main[I] || Valid[I];

            return Main;
        }

        /// <summary>
        /// Applies the computed boundary mask: fills elevation values for newly-valid cells by
        /// propagating from observed neighbours (constrained to the mask), then clips cells
        /// outside the mask back to invalid.
        /// </summary>
        private void ApplyObservedBoundaryMask()
        {
            bool[] Mask = ComputeObservedBoundaryMask();

            // Propagate elevations into mask cells that don't yet have data.
            // Constrained to the mask so we never expand beyond the data boundary.
            for (int Pass = 0; Pass < OBSERVED_MASK_PROPAGATION_MAX_PASSES; Pass++)
            {
                bool AnyFilled = false;
                bool[] Expanded = (bool[])Valid.Clone();

                for (int Y = 0; Y < SampleHeight; Y++)
                {
                    for (int X = 0; X < SampleWidth; X++)
                    {
                        int Idx = Y * SampleWidth + X;
                        if (Valid[Idx] || !Mask[Idx]) continue;

                        double SumE = 0, SumP = 0;
                        int Count = 0;
                        for (int Dy = -1; Dy <= 1; Dy++)
                        {
                            for (int Dx = -1; Dx <= 1; Dx++)
                            {
                                if (Dx == 0 && Dy == 0) continue;
                                int NX = X + Dx, NY = Y + Dy;
                                if (NX < 0 || NX >= SampleWidth || NY < 0 || NY >= SampleHeight) continue;
                                int NIdx = NY * SampleWidth + NX;
                                if (!Valid[NIdx]) continue;
                                SumE += Existing[NIdx];
                                SumP += Proposed[NIdx];
                                Count++;
                            }
                        }
                        if (Count < 1) continue;

                        Expanded[Idx] = true;
                        Existing[Idx] = SumE / Count;
                        Proposed[Idx] = SumP / Count;
                        AnyFilled = true;
                    }
                }

                Valid = Expanded;
                if (!AnyFilled) break;
            }

            // Clip: any cell outside the computed mask must not remain valid.
            for (int I = 0; I < Valid.Length; I++)
                if (!Mask[I]) Valid[I] = false;
        }

        /// <summary>
        /// Expands true cells in a boolean mask using eight-connected dilation.
        /// </summary>
        /// <param name="Mask">Source validity mask.</param>
        /// <param name="W">Grid width in cells.</param>
        /// <param name="H">Grid height in cells.</param>
        /// <param name="Passes">Number of dilation iterations to apply.</param>
        /// <returns>Dilated mask copy.</returns>
        private static bool[] MorphDilate(bool[] Mask, int W, int H, int Passes)
        {
            bool[] Out = (bool[])Mask.Clone();
            for (int P = 0; P < Passes; P++)
            {
                bool[] Next = new bool[Out.Length];
                for (int Y = 0; Y < H; Y++)
                {
                    for (int X = 0; X < W; X++)
                    {
                        bool Any = false;
                        for (int Dy = -1; Dy <= 1 && !Any; Dy++)
                        {
                            int YY = Y + Dy;
                            if (YY < 0 || YY >= H) continue;
                            for (int Dx = -1; Dx <= 1; Dx++)
                            {
                                int XX = X + Dx;
                                if (XX < 0 || XX >= W) continue;
                                if (Out[YY * W + XX]) { Any = true; break; }
                            }
                        }
                        Next[Y * W + X] = Any;
                    }
                }
                Out = Next;
            }
            return Out;
        }

        /// <summary>
        /// Shrinks true cells in a boolean mask using eight-connected erosion.
        /// </summary>
        /// <param name="Mask">Source validity mask.</param>
        /// <param name="W">Grid width in cells.</param>
        /// <param name="H">Grid height in cells.</param>
        /// <param name="Passes">Number of erosion iterations to apply.</param>
        /// <returns>Eroded mask copy.</returns>
        private static bool[] MorphErode(bool[] Mask, int W, int H, int Passes)
        {
            bool[] Out = (bool[])Mask.Clone();
            for (int P = 0; P < Passes; P++)
            {
                bool[] Next = new bool[Out.Length];
                for (int Y = 0; Y < H; Y++)
                {
                    for (int X = 0; X < W; X++)
                    {
                        bool All = true;
                        for (int Dy = -1; Dy <= 1 && All; Dy++)
                        {
                            int YY = Y + Dy;
                            if (YY < 0 || YY >= H) { All = false; break; }
                            for (int Dx = -1; Dx <= 1; Dx++)
                            {
                                int XX = X + Dx;
                                if (XX < 0 || XX >= W || !Out[YY * W + XX]) { All = false; break; }
                            }
                        }
                        Next[Y * W + X] = All;
                    }
                }
                Out = Next;
            }
            return Out;
        }

        /// <summary>
        /// Fills enclosed false regions in a boolean mask by flood-filling from the grid border.
        /// </summary>
        /// <param name="Mask">Source validity mask.</param>
        /// <param name="W">Grid width in cells.</param>
        /// <param name="H">Grid height in cells.</param>
        /// <returns>Mask with interior holes filled to true.</returns>
        private static bool[] MorphFillHoles(bool[] Mask, int W, int H)
        {
            bool[] Inv = new bool[Mask.Length];
            for (int I = 0; I < Mask.Length; I++) Inv[I] = !Mask[I];

            bool[] Outside = new bool[Mask.Length];
            Queue<int> Q = new Queue<int>();
            void TryEnqueue(int X, int Y)
            {
                if (X < 0 || X >= W || Y < 0 || Y >= H) return;
                int I = Y * W + X;
                if (!Inv[I] || Outside[I]) return;
                Outside[I] = true;
                Q.Enqueue(I);
            }
            for (int X = 0; X < W; X++) { TryEnqueue(X, 0); TryEnqueue(X, H - 1); }
            for (int Y = 0; Y < H; Y++) { TryEnqueue(0, Y); TryEnqueue(W - 1, Y); }
            while (Q.Count > 0)
            {
                int Cur = Q.Dequeue();
                int CY = Cur / W, CX = Cur % W;
                TryEnqueue(CX - 1, CY); TryEnqueue(CX + 1, CY);
                TryEnqueue(CX, CY - 1); TryEnqueue(CX, CY + 1);
            }

            bool[] Out = new bool[Mask.Length];
            for (int I = 0; I < Mask.Length; I++)
                Out[I] = Mask[I] || (Inv[I] && !Outside[I]);
            return Out;
        }

        /// <summary>
        /// Retains only the largest eight-connected true component in a boolean mask.
        /// </summary>
        /// <param name="Mask">Source validity mask.</param>
        /// <param name="W">Grid width in cells.</param>
        /// <param name="H">Grid height in cells.</param>
        /// <returns>Mask containing only the largest connected component.</returns>
        private static bool[] MorphLargestComponent(bool[] Mask, int W, int H)
        {
            bool[] Seen = new bool[Mask.Length];
            List<int> Best = new List<int>();
            int[] NbX = { -1, 0, 1, -1, 1, -1, 0, 1 };
            int[] NbY = { -1, -1, -1, 0, 0, 1, 1, 1 };

            for (int Y0 = 0; Y0 < H; Y0++)
            {
                for (int X0 = 0; X0 < W; X0++)
                {
                    int I0 = Y0 * W + X0;
                    if (Seen[I0] || !Mask[I0]) continue;
                    var Comp = new List<int>();
                    var St = new Stack<(int X, int Y)>();
                    Seen[I0] = true;
                    St.Push((X0, Y0));
                    while (St.Count > 0)
                    {
                        (int X, int Y) = St.Pop();
                        Comp.Add(Y * W + X);
                        for (int K = 0; K < EIGHT_CONNECTED_NEIGHBOR_COUNT; K++)
                        {
                            int XX = X + NbX[K], YY = Y + NbY[K];
                            if (XX < 0 || XX >= W || YY < 0 || YY >= H) continue;
                            int II = YY * W + XX;
                            if (Seen[II] || !Mask[II]) continue;
                            Seen[II] = true;
                            St.Push((XX, YY));
                        }
                    }
                    if (Comp.Count > Best.Count) Best = Comp;
                }
            }

            bool[] Out = new bool[Mask.Length];
            foreach (int I in Best) Out[I] = true;
            return Out;
        }

        /// <summary>

        /// Grows the valid sample mask outward so sparse bin footprints tessellate without gaps.

        /// </summary>

        private void ExpandValidSurface

            (

            int Passes

            )

        {

            for (int Pass = 0; Pass < Passes; Pass++)

            {

                bool[] Expanded = (bool[])Valid.Clone();

                for (int Y = 0; Y < SampleHeight; Y++)

                {

                    for (int X = 0; X < SampleWidth; X++)

                    {

                        int Idx = Y * SampleWidth + X;

                        if (Valid[Idx])

                            continue;



                        double SumExisting = 0;

                        double SumProposed = 0;

                        int Count = 0;

                        for (int Dy = -1; Dy <= 1; Dy++)

                        {

                            for (int Dx = -1; Dx <= 1; Dx++)

                            {

                                if ((Dx == 0) && (Dy == 0))

                                    continue;

                                int NeighbourIdx = (Y + Dy) * SampleWidth + (X + Dx);

                                if ((NeighbourIdx < 0) || (NeighbourIdx >= Valid.Length) || !Valid[NeighbourIdx])

                                    continue;

                                SumExisting += Existing[NeighbourIdx];

                                SumProposed += Proposed[NeighbourIdx];

                                Count++;

                            }

                        }



                        if (Count < MIN_EXPAND_NEIGHBOUR_COUNT)

                            continue;

                        Expanded[Idx] = true;

                        Existing[Idx] = SumExisting / Count;

                        Proposed[Idx] = SumProposed / Count;

                    }

                }

                Valid = Expanded;

            }

        }



        /// <summary>
        /// Blends boundary-adjacent fringe elevations toward valid neighbours without clearing samples.
        /// </summary>
        private void SmoothSparseExpansionFringe
            (
            int Passes
            )
        {
            for (int Pass = 0; Pass < Passes; Pass++)
            {
                double[] ExistingCopy = (double[])Existing.Clone();
                double[] ProposedCopy = (double[])Proposed.Clone();

                for (int Y = 0; Y < SampleHeight; Y++)
                {
                    for (int X = 0; X < SampleWidth; X++)
                    {
                        int Idx = (Y * SampleWidth) + X;
                        if (!Valid[Idx] || !IsFringeSampleCell(X, Y))
                            continue;

                        double SumExisting = 0;
                        double SumProposed = 0;
                        int Count = 0;
                        for (int Dy = -1; Dy <= 1; Dy++)
                        {
                            for (int Dx = -1; Dx <= 1; Dx++)
                            {
                                if ((Dx == 0) && (Dy == 0))
                                    continue;

                                int Ny = Y + Dy;
                                int Nx = X + Dx;
                                if ((Ny < 0) || (Ny >= SampleHeight) || (Nx < 0) || (Nx >= SampleWidth))
                                    continue;

                                int NeighbourIdx = (Ny * SampleWidth) + Nx;
                                if (!Valid[NeighbourIdx])
                                    continue;

                                SumExisting += ExistingCopy[NeighbourIdx];
                                SumProposed += ProposedCopy[NeighbourIdx];
                                Count++;
                            }
                        }

                        if (Count == 0)
                            continue;

                        Existing[Idx] = SumExisting / Count;
                        Proposed[Idx] = SumProposed / Count;
                    }
                }
            }
        }

        /// <summary>
        /// Fills any invalid cells that are enclosed by valid cells (interior holes).
        /// Uses a border-seeded flood fill to identify exterior invalid cells, then
        /// iteratively fills the remaining interior holes from valid neighbours.
        /// </summary>
        private void FillInteriorHoles()
        {
            // Step 1: flood-fill from the raster border to tag all exterior invalid cells.
            bool[] Exterior = new bool[Valid.Length];
            Queue<int> FloodQ = new Queue<int>();

            for (int Y = 0; Y < SampleHeight; Y++)
            {
                for (int X = 0; X < SampleWidth; X++)
                {
                    bool IsBorder = (X == 0) || (X == SampleWidth - 1) || (Y == 0) || (Y == SampleHeight - 1);
                    int Idx = Y * SampleWidth + X;
                    if (IsBorder && !Valid[Idx] && !Exterior[Idx])
                    {
                        Exterior[Idx] = true;
                        FloodQ.Enqueue(Idx);
                    }
                }
            }

            int[] FDx = { -1, 0, 1, -1, 1, -1, 0, 1 };
            int[] FDy = { -1, -1, -1, 0, 0, 1, 1, 1 };
            while (FloodQ.Count > 0)
            {
                int Cur = FloodQ.Dequeue();
                int CY = Cur / SampleWidth;
                int CX = Cur % SampleWidth;
                for (int D = 0; D < EIGHT_CONNECTED_NEIGHBOR_COUNT; D++)
                {
                    int NX = CX + FDx[D];
                    int NY = CY + FDy[D];
                    if ((NX < 0) || (NX >= SampleWidth) || (NY < 0) || (NY >= SampleHeight))
                        continue;
                    int NIdx = NY * SampleWidth + NX;
                    if (!Valid[NIdx] && !Exterior[NIdx])
                    {
                        Exterior[NIdx] = true;
                        FloodQ.Enqueue(NIdx);
                    }
                }
            }

            // Step 2: iteratively fill interior holes (not exterior, not already valid)
            // using averages of valid neighbours.  Repeat until stable.
            for (int Pass = 0; Pass < INTERIOR_HOLE_FILL_MAX_PASSES; Pass++)
            {
                bool AnyFilled = false;
                for (int Y = 0; Y < SampleHeight; Y++)
                {
                    for (int X = 0; X < SampleWidth; X++)
                    {
                        int Idx = Y * SampleWidth + X;
                        if (Valid[Idx] || Exterior[Idx])
                            continue;

                        double SumE = 0, SumP = 0;
                        int Count = 0;
                        for (int Dy = -1; Dy <= 1; Dy++)
                        {
                            for (int Dx = -1; Dx <= 1; Dx++)
                            {
                                if ((Dx == 0) && (Dy == 0))
                                    continue;
                                int NX = X + Dx;
                                int NY = Y + Dy;
                                if ((NX < 0) || (NX >= SampleWidth) || (NY < 0) || (NY >= SampleHeight))
                                    continue;
                                int NIdx = NY * SampleWidth + NX;
                                if (!Valid[NIdx])
                                    continue;
                                SumE += Existing[NIdx];
                                SumP += Proposed[NIdx];
                                Count++;
                            }
                        }
                        if (Count == 0)
                            continue;

                        Valid[Idx] = true;
                        Existing[Idx] = SumE / Count;
                        Proposed[Idx] = SumP / Count;
                        AnyFilled = true;
                    }
                }
                if (!AnyFilled)
                    break;
            }
        }

        /// <summary>
        /// True when a valid sample lies on the edge of the valid region or the raster.
        /// </summary>
        private bool IsFringeSampleCell
            (
            int X,
            int Y
            )
        {
            for (int Dy = -1; Dy <= 1; Dy++)
            {
                for (int Dx = -1; Dx <= 1; Dx++)
                {
                    if ((Dx == 0) && (Dy == 0))
                        continue;

                    int Ny = Y + Dy;
                    int Nx = X + Dx;
                    if ((Ny < 0) || (Ny >= SampleHeight) || (Nx < 0) || (Nx >= SampleWidth))
                        return true;

                    if (!Valid[(Ny * SampleWidth) + Nx])
                        return true;
                }
            }

            return false;
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

            return Grid.TryGetValidBin(X, Y, out Bin);

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
        /// Minimum survey point count before switching from IDW to direct rasterization.
        /// </summary>
        private const int SURVEY_RASTERIZE_THRESHOLD = 500;

        /// <summary>
        /// Inpaint passes applied after rasterizing large survey point clouds.
        /// </summary>
        private const int SURVEY_RASTERIZE_EXPAND_PASSES = 8;

        /// <summary>

        /// Builds the scalar field from survey points using regular-grid or IDW interpolation.

        /// </summary>

        /// <param name="Survey">Survey model with topology points.</param>

        /// <param name="Resolution">Sample grid resolution per axis.</param>

        private void BuildFromSurvey

            (

            SurveyModel Survey,

            int Resolution,

            IProgress<int>? Progress = null

            )

        {

            if (Survey.InteriorPoints.Count + Survey.BoundaryPoints.Count == 0)

            {

                Clear();

                return;

            }



            InvalidateContourCache();

            InvalidateColorMapCache();

            BoundaryLatLon = BuildBoundaryLatLon(Survey);

            List<(double X, double Y)>? BoundaryRing = BuildBoundaryRing(Survey);

            ComputeExtent(BoundaryRing, null, Survey, out double ExtMinLat, out double ExtMaxLat, out double ExtMinLon, out double ExtMaxLon);

            MinLat = ExtMinLat;

            MaxLat = ExtMaxLat;

            MinLon = ExtMinLon;

            MaxLon = ExtMaxLon;



            double LatPad = Math.Max(MIN_EXTENT_PAD_DEGREES, (MaxLat - MinLat) * SURVEY_EXTENT_PAD_FRACTION);

            double LonPad = Math.Max(MIN_EXTENT_PAD_DEGREES, (MaxLon - MinLon) * SURVEY_EXTENT_PAD_FRACTION);

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



            if (RegularSurveyGrid.TryBuild(Survey, out RegularSurveyGrid? RegularGrid))

            {

                SampleRegularSurveyGrid(RegularGrid, BoundaryLatLon, Progress);

                return;

            }



            int PointCount = Survey.InteriorPoints.Count + Survey.BoundaryPoints.Count;

            if (PointCount >= SURVEY_RASTERIZE_THRESHOLD)

            {

                RasterizeSurveyPoints(Survey, BoundaryLatLon, Progress);

                return;

            }



            List<(double E, double N, double Z)> Points = new List<(double E, double N, double Z)>(PointCount);

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



            SurveySpatialInterpolator Interpolator = new SurveySpatialInterpolator(Points);

            SampleSurveyWithInterpolator(Interpolator, BoundaryLatLon, Progress);

        }



        /// <summary>

        /// Samples a detected regular survey grid into the surface raster.

        /// </summary>

        private void SampleRegularSurveyGrid

            (

            RegularSurveyGrid RegularGrid,

            IReadOnlyList<(double Lat, double Lon)>? BoundaryLatLonRing,

            IProgress<int>? Progress

            )

        {

            for (int Sy = 0; Sy < SampleHeight; Sy++)

            {

                double Lat = MaxLat - (MaxLat - MinLat) * Sy / (SampleHeight - 1);

                for (int Sx = 0; Sx < SampleWidth; Sx++)

                {

                    double Lon = MinLon + (MaxLon - MinLon) * Sx / (SampleWidth - 1);



                    if (BoundaryLatLonRing != null &&

                        !BoundaryMask.IsInsideBoundaryLatLon(Lat, Lon, BoundaryLatLonRing))

                        continue;



                    double Elev = RegularGrid.Sample(Lat, Lon);

                    if (double.IsNaN(Elev))

                        continue;



                    int Idx = Sy * SampleWidth + Sx;

                    Existing[Idx] = Elev;

                    Proposed[Idx] = Elev;

                    Valid[Idx] = true;

                    MinElevation = Math.Min(MinElevation, Elev);

                    MaxElevation = Math.Max(MaxElevation, Elev);

                }

                ProgressMapper.ReportSegment(Progress, 0, PROGRESS_SURVEY_SAMPLE_END_PERCENT, (Sy + 1) * PROGRESS_COMPLETE_PERCENT / SampleHeight);

            }



            FinishSurveySurfaceSmoothing(Progress, PROGRESS_SURVEY_SAMPLE_END_PERCENT);

        }



        /// <summary>

        /// Interpolates a small survey point cloud onto the surface raster.

        /// </summary>

        private void SampleSurveyWithInterpolator

            (

            SurveySpatialInterpolator Interpolator,

            IReadOnlyList<(double Lat, double Lon)>? BoundaryLatLonRing,

            IProgress<int>? Progress

            )

        {

            for (int Sy = 0; Sy < SampleHeight; Sy++)

            {

                double Lat = MaxLat - (MaxLat - MinLat) * Sy / (SampleHeight - 1);

                for (int Sx = 0; Sx < SampleWidth; Sx++)

                {

                    double Lon = MinLon + (MaxLon - MinLon) * Sx / (SampleWidth - 1);



                    if (BoundaryLatLonRing != null &&

                        !BoundaryMask.IsInsideBoundaryLatLon(Lat, Lon, BoundaryLatLonRing))

                        continue;



                    UTM.UTMCoordinate Utm = UTM.FromLatLon(Lat, Lon);

                    double Elev = Interpolator.Interpolate(Utm.Easting, Utm.Northing);

                    if (double.IsNaN(Elev))

                        continue;



                    int Idx = Sy * SampleWidth + Sx;

                    Existing[Idx] = Elev;

                    Proposed[Idx] = Elev;

                    Valid[Idx] = true;

                    MinElevation = Math.Min(MinElevation, Elev);

                    MaxElevation = Math.Max(MaxElevation, Elev);

                }

                ProgressMapper.ReportSegment(Progress, 0, PROGRESS_SURVEY_SAMPLE_END_PERCENT, (Sy + 1) * PROGRESS_COMPLETE_PERCENT / SampleHeight);

            }



            FinishSurveySurfaceSmoothing(Progress, PROGRESS_SURVEY_SAMPLE_END_PERCENT);

        }



        /// <summary>

        /// Rasterizes large survey point clouds directly onto the sample grid.

        /// </summary>

        private void RasterizeSurveyPoints

            (

            SurveyModel Survey,

            IReadOnlyList<(double Lat, double Lon)>? BoundaryLatLonRing,

            IProgress<int>? Progress

            )

        {

            double LatSpan = MaxLat - MinLat;

            double LonSpan = MaxLon - MinLon;

            if ((LatSpan <= 0) || (LonSpan <= 0))

                return;



            int TotalPoints = Survey.InteriorPoints.Count + Survey.BoundaryPoints.Count;

            int ReportStep = Math.Max(1, TotalPoints / SURVEY_RASTERIZE_REPORT_DIVISOR);

            int Processed = 0;



            void PlacePoint

                (

                double Lat,

                double Lon,

                double Elev

                )

            {

                if (BoundaryLatLonRing != null &&

                    !BoundaryMask.IsInsideBoundaryLatLon(Lat, Lon, BoundaryLatLonRing))

                    return;



                int Sx = (int)Math.Round((Lon - MinLon) / LonSpan * (SampleWidth - 1));

                int Sy = (int)Math.Round((MaxLat - Lat) / LatSpan * (SampleHeight - 1));

                Sx = Math.Clamp(Sx, 0, SampleWidth - 1);

                Sy = Math.Clamp(Sy, 0, SampleHeight - 1);

                int Idx = Sy * SampleWidth + Sx;

                if (Valid[Idx])

                {

                    Existing[Idx] = (Existing[Idx] + Elev) * DUPLICATE_SAMPLE_AVERAGE_FACTOR;

                    Proposed[Idx] = (Proposed[Idx] + Elev) * DUPLICATE_SAMPLE_AVERAGE_FACTOR;

                }

                else

                {

                    Existing[Idx] = Elev;

                    Proposed[Idx] = Elev;

                    Valid[Idx] = true;

                }

                MinElevation = Math.Min(MinElevation, Elev);

                MaxElevation = Math.Max(MaxElevation, Elev);

            }



            foreach (TopologyPoint Pt in Survey.InteriorPoints)

            {

                PlacePoint(Pt.Latitude, Pt.Longitude, Pt.ExistingElevation);

                Processed++;

                if ((Processed % ReportStep) == 0)

                    ProgressMapper.ReportSegment(Progress, 0, PROGRESS_SURVEY_RASTERIZE_END_PERCENT, Processed * PROGRESS_COMPLETE_PERCENT / TotalPoints);

            }



            foreach (TopologyPoint Pt in Survey.BoundaryPoints)

            {

                PlacePoint(Pt.Latitude, Pt.Longitude, Pt.ExistingElevation);

                Processed++;

                if ((Processed % ReportStep) == 0)

                    ProgressMapper.ReportSegment(Progress, 0, PROGRESS_SURVEY_RASTERIZE_END_PERCENT, Processed * PROGRESS_COMPLETE_PERCENT / TotalPoints);

            }



            ProgressMapper.ReportSegment(Progress, PROGRESS_SURVEY_RASTERIZE_END_PERCENT, PROGRESS_SURVEY_SAMPLE_END_PERCENT, PROGRESS_COMPLETE_PERCENT);

            ExpandValidSurface(Passes: SURVEY_RASTERIZE_EXPAND_PASSES);

            ClipValidToBoundaryLatLon();

            FinishSurveySurfaceSmoothing(Progress, PROGRESS_SURVEY_SAMPLE_END_PERCENT);

        }



        /// <summary>

        /// Applies final smoothing passes to a survey-built surface raster.

        /// </summary>

        private void FinishSurveySurfaceSmoothing

            (

            IProgress<int>? Progress,

            int SmoothStartPercent

            )

        {

            for (int Pass = 0; Pass < SMOOTH_PASSES; Pass++)

            {

                SmoothPass(Existing);

                ProgressMapper.ReportSegment(Progress, SmoothStartPercent, PROGRESS_COMPLETE_PERCENT, (Pass + 1) * PROGRESS_COMPLETE_PERCENT / SMOOTH_PASSES);

            }

            Progress?.Report(PROGRESS_COMPLETE_PERCENT);

            RecomputeElevationExtentsFromValid();

        }

        /// <summary>
        /// Copies the active scalar channel into a parallel array with NaN outside the valid mask.
        /// </summary>

        /// <param name="ContentView">Scalar channel to copy.</param>

        /// <returns>Scalar samples parallel to the surface raster.</returns>

        private double[] CopyScalarField

            (

            FieldContentView ContentView

            )

        {

            double[] Field = new double[Existing.Length];

            for (int Idx = 0; Idx < Existing.Length; Idx++)

                Field[Idx] = Valid[Idx] ? GetScalarAtIndex(ContentView, Idx) : double.NaN;

            return Field;

        }



        /// <summary>

        /// Bilinearly upsamples a scalar field at a geographic location.

        /// </summary>

        /// <param name="Field">Flattened scalar samples.</param>

        /// <param name="Lat">Sample latitude in decimal degrees.</param>

        /// <param name="Lon">Sample longitude in decimal degrees.</param>

        /// <returns>Interpolated scalar, or NaN when outside the raster.</returns>

        private double SampleScalarFieldBilinear

            (

            double[] Field,

            double Lat,

            double Lon

            )

        {

            return SampleField(Field, Lat, Lon);

        }



        /// <summary>
        /// Returns true when a geographic point lies inside the rendered topo region.
        /// Uses the survey boundary ring when available, otherwise the valid mesh quads.
        /// </summary>
        public bool IsInsideValidRegion(double Lat, double Lon)
        {
            if (Lat < MinLat || Lat > MaxLat || Lon < MinLon || Lon > MaxLon)
                return false;

            if (BoundaryLatLon != null && BoundaryLatLon.Count >= MIN_BOUNDARY_VERTEX_COUNT)
                return BoundaryMask.IsInsideBoundaryLatLon(Lat, Lon, BoundaryLatLon);

            int W = SampleWidth;
            int H = SampleHeight;
            if (W < MIN_VALID_QUAD_GRID_SIZE || H < MIN_VALID_QUAD_GRID_SIZE)
                return false;

            double LonSpan = MaxLon - MinLon;
            double LatSpan = MaxLat - MinLat;
            if (LonSpan <= 0 || LatSpan <= 0)
                return false;

            double Fx = (Lon - MinLon) / LonSpan * (W - 1);
            double Fy = (MaxLat - Lat) / LatSpan * (H - 1);
            int Sx = (int)Math.Floor(Fx);
            int Sy = (int)Math.Floor(Fy);
            if (Sx < 0 || Sy < 0 || Sx >= W - 1 || Sy >= H - 1)
                return false;

            if (!IsAllValidQuad(Sx, Sy))
                return false;

            double Lon0 = MinLon + LonSpan * Sx / (W - 1);
            double Lon1 = MinLon + LonSpan * (Sx + 1) / (W - 1);
            double LatNorth = MaxLat - LatSpan * Sy / (H - 1);
            double LatSouth = MaxLat - LatSpan * (Sy + 1) / (H - 1);
            return Lat <= LatNorth && Lat >= LatSouth && Lon >= Lon0 && Lon <= Lon1;
        }

        /// <summary>
        /// Returns true when all four corners of a sample grid quad are marked valid.
        /// </summary>
        /// <param name="Sx">Lower-left column index of the quad.</param>
        /// <param name="Sy">Lower-left row index of the quad.</param>
        /// <returns>True when every corner cell in the quad is valid.</returns>
        private bool IsAllValidQuad(int Sx, int Sy)
        {
            int W = SampleWidth;
            int Idx00 = Sy * W + Sx;
            int Idx10 = Sy * W + Sx + 1;
            int Idx11 = (Sy + 1) * W + Sx + 1;
            int Idx01 = (Sy + 1) * W + Sx;
            return Valid[Idx00] && Valid[Idx10] && Valid[Idx11] && Valid[Idx01];
        }



        /// <summary>

        /// Returns whether a geographic point lies inside the field for contour extraction.

        /// </summary>

        /// <param name="Lat">Point latitude in decimal degrees.</param>

        /// <param name="Lon">Point longitude in decimal degrees.</param>

        /// <param name="UtmRing">Optional UTM boundary ring.</param>

        /// <returns>True when the point is inside the field boundary or no boundary exists.</returns>

        private bool IsContourCellInside

            (

            double Lat,

            double Lon,

            List<(double X, double Y)>? UtmRing

            )

        {

            if ((Lat < MinLat) || (Lat > MaxLat) || (Lon < MinLon) || (Lon > MaxLon))

                return false;



            return IsInsideValidRegion(Lat, Lon);

        }



        /// <summary>

        /// Fills missing height samples from valid neighbours inside the field mask.

        /// </summary>

        static private void InpaintContourGridPass

            (

            double[,] Heights,

            bool[,] InsideField,

            int Width,

            int Height

            )

        {

            double[,] Copy = (double[,])Heights.Clone();

            for (int Y = 0; Y < Height; Y++)

            {

                for (int X = 0; X < Width; X++)

                {

                    if (!InsideField[Y, X] || double.IsFinite(Copy[Y, X]))

                        continue;



                    double Sum = 0;

                    int Count = 0;

                    for (int Dy = -1; Dy <= 1; Dy++)

                    {

                        for (int Dx = -1; Dx <= 1; Dx++)

                        {

                            if ((Dx == 0) && (Dy == 0))

                                continue;



                            int Nx = X + Dx;

                            int Ny = Y + Dy;

                            if ((Nx < 0) || (Ny < 0) || (Nx >= Width) || (Ny >= Height))

                                continue;



                            if (!InsideField[Ny, Nx] || !double.IsFinite(Copy[Ny, Nx]))

                                continue;



                            Sum += Copy[Ny, Nx];

                            Count++;

                        }

                    }



                    if (Count > 0)

                        Heights[Y, X] = Sum / Count;

                }

            }

        }



        /// <summary>

        /// Applies one box-smooth pass to a filled contour height grid.

        /// </summary>

        static private void SmoothContourGridPass

            (

            double[,] Heights,

            bool[,] InsideField,

            int Width,

            int Height

            )

        {

            double[,] Copy = (double[,])Heights.Clone();

            for (int Y = 0; Y < Height; Y++)

            {

                for (int X = 0; X < Width; X++)

                {

                    if (!InsideField[Y, X] || !double.IsFinite(Copy[Y, X]))

                        continue;



                    double Sum = 0;

                    int Count = 0;

                    for (int Dy = -1; Dy <= 1; Dy++)

                    {

                        for (int Dx = -1; Dx <= 1; Dx++)

                        {

                            int Nx = X + Dx;

                            int Ny = Y + Dy;

                            if ((Nx < 0) || (Ny < 0) || (Nx >= Width) || (Ny >= Height))

                                continue;



                            if (!InsideField[Ny, Nx] || !double.IsFinite(Copy[Ny, Nx]))

                                continue;



                            Sum += Copy[Ny, Nx];

                            Count++;

                        }

                    }



                    if (Count > 0)

                        Heights[Y, X] = Sum / Count;

                }

            }

        }



        /// <summary>

        /// Converts survey boundary points to a lat/lon ring for contour clipping.

        /// </summary>

        /// <param name="Survey">Survey model with boundary points.</param>

        /// <returns>Geographic ring, or null when fewer than three boundary points exist.</returns>

        static private List<(double Lat, double Lon)>? BuildBoundaryLatLon

            (

            SurveyModel? Survey

            )

        {

            if (Survey == null || Survey.BoundaryPoints.Count < MIN_BOUNDARY_VERTEX_COUNT)

                return null;



            List<(double Lat, double Lon)> Ring = new List<(double Lat, double Lon)>();

            foreach (TopologyPoint Pt in Survey.BoundaryPoints)

                Ring.Add((Pt.Latitude, Pt.Longitude));

            return BoundaryMask.OrderBoundaryLatLon(Ring);

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

            if (Survey == null || Survey.BoundaryPoints.Count < MIN_BOUNDARY_VERTEX_COUNT)

                return null;



            List<(double Lat, double Lon)> LatLon = new List<(double Lat, double Lon)>();

            foreach (TopologyPoint Pt in Survey.BoundaryPoints)

                LatLon.Add((Pt.Latitude, Pt.Longitude));

            // Convert geographic boundary vertices to a UTM ring for clipping tests.
            return BoundaryMask.BuildUtmRing(BoundaryMask.OrderBoundaryLatLon(LatLon));

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



            // Union extent from boundary, design bins, and interior survey points.
            if (Survey != null && Survey.BoundaryPoints.Count >= MIN_BOUNDARY_VERTEX_COUNT)

            {

                foreach (TopologyPoint Pt in Survey.BoundaryPoints)

                {

                    MinLat = Math.Min(MinLat, Pt.Latitude);

                    MaxLat = Math.Max(MaxLat, Pt.Latitude);

                    MinLon = Math.Min(MinLon, Pt.Longitude);

                    MaxLon = Math.Max(MaxLon, Pt.Longitude);

                }

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

        /// Samples the nearest valid bin when bilinear interpolation is undefined.

        /// </summary>

        static private bool TrySampleGridNearest

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

            int Nx = (int)Math.Round(Ex);

            int Ny = (int)Math.Round(Ey);

            if (!TryGetBin(Grid, Nx, Ny, out GridBinData Bin))

                return false;

            ExistingM = Bin.ExistingM;

            ProposedM = Bin.ProposedM;

            return true;

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


