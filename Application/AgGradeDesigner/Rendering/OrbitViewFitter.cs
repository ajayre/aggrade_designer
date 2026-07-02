using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Survey;

using SkiaSharp;

namespace AgGradeDesigner.Rendering
{
    /// <summary>
    /// Computes 3D orbit distance and pivot so the full field fits and is centred in the viewport.
    /// </summary>
    static internal class OrbitViewFitter
    {
        /// <summary>Default orbit heading: north-east oblique view.</summary>
        public const double InitialHeadingDeg = 45.0;

        /// <summary>Default orbit pitch for the initial 3D presentation.</summary>
        public const double InitialPitchDeg = 45.0;

        /// <summary>Viewport margin fraction required on all sides when testing field fit.</summary>
        private const double FIT_MARGIN_FRACTION = 0.20;
        /// <summary>Minimum orbit distance floor used during binary search in metres.</summary>
        private const double MIN_DISTANCE_M = 80.0;
        /// <summary>Number of binary-search iterations when fitting orbit distance.</summary>
        private const int DISTANCE_SEARCH_ITERATIONS = 22;
        /// <summary>Minimum distance as a fraction of field horizontal extent.</summary>
        private const double MIN_DISTANCE_EXTENT_FRACTION = 0.35;
        /// <summary>Maximum distance as a multiple of field horizontal extent.</summary>
        private const double MAX_DISTANCE_EXTENT_MULTIPLIER = 4.0;
        /// <summary>Upper cap on max distance as a multiple of the computed minimum distance.</summary>
        private const double MAX_DISTANCE_MIN_DISTANCE_MULTIPLIER = 2.0;
        /// <summary>Midpoint fraction for each binary-search distance trial.</summary>
        private const double BINARY_SEARCH_MIDPOINT_FRACTION = 0.5;

        /// <summary>
        /// Sets the standard north-east oblique orbit orientation used when 3D is first opened.
        /// </summary>
        static public void ApplyInitialOrientation
            (
            Camera3DState Camera
            )
        {
            Camera.OrbitHeadingDeg = InitialHeadingDeg;
            Camera.OrbitPitchDeg = InitialPitchDeg;
        }

        /// <summary>
        /// Updates 3D camera state so the project field fills the viewport with margin.
        /// </summary>
        static public void FitToField
            (
            ProjectDocument Project,
            FieldSurfaceSampler Surface,
            Camera3DState Camera,
            double VerticalExaggeration,
            int ViewportWidth,
            int ViewportHeight
            )
        {
            if ((ViewportWidth <= 0) || (ViewportHeight <= 0) || !Project.HasSurvey || (Surface.SampleWidth <= 0))
                return;

            if (!FieldBounds.TryFromProject(Project, out FieldBounds Bounds))
                return;

            SKRect Viewport = SKRect.Create(0, 0, ViewportWidth, ViewportHeight);
            List<(double Lat, double Lon, double Elev)> SamplePoints = CollectSamplePoints(Project, Bounds, Surface);
            if (SamplePoints.Count == 0)
                return;

            double OrbitLat = Bounds.CentreLat;
            double OrbitLon = Bounds.CentreLon;
            double HeadingDeg = Camera.OrbitHeadingDeg;
            double PitchDeg = Camera.OrbitPitchDeg;

            UTM.UTMCoordinate SouthWest = UTM.FromLatLon(Bounds.MinLat, Bounds.MinLon);
            UTM.UTMCoordinate NorthEast = UTM.FromLatLon(Bounds.MaxLat, Bounds.MaxLon);
            double ExtentM = Math.Max(
                Math.Abs(NorthEast.Easting - SouthWest.Easting),
                Math.Abs(NorthEast.Northing - SouthWest.Northing));

            double MinDistance = Math.Max(MIN_DISTANCE_M, ExtentM * MIN_DISTANCE_EXTENT_FRACTION);
            double MaxDistance = Math.Min(
                RenderingConstants.MAX_ORBIT_DISTANCE_M,
                Math.Max(ExtentM * MAX_DISTANCE_EXTENT_MULTIPLIER, MinDistance * MAX_DISTANCE_MIN_DISTANCE_MULTIPLIER));
            double DistanceM = MaxDistance;
            FieldContentView ReliefView = ResolveReliefView(Project);

            for (int Iteration = 0; Iteration < DISTANCE_SEARCH_ITERATIONS; Iteration++)
            {
                double Trial = (MinDistance + MaxDistance) * BINARY_SEARCH_MIDPOINT_FRACTION;
                if (FitsInViewport(SamplePoints, Surface, Viewport, OrbitLat, OrbitLon, HeadingDeg, PitchDeg, Trial, VerticalExaggeration, ReliefView))
                {
                    DistanceM = Trial;
                    MaxDistance = Trial;
                }
                else
                {
                    MinDistance = Trial;
                }
            }

            // Keep the orbit pivot at the field geometric centre so drag rotation feels natural.
            Camera.OrbitCentreLat = OrbitLat;
            Camera.OrbitCentreLon = OrbitLon;
            Camera.DistanceM = Math.Clamp(DistanceM, RenderingConstants.MIN_ORBIT_DISTANCE_M, RenderingConstants.MAX_ORBIT_DISTANCE_M);
        }

        /// <summary>
        /// Collects geographic samples used to bound the fitted 3D view.
        /// </summary>
        static private List<(double Lat, double Lon, double Elev)> CollectSamplePoints
            (
            ProjectDocument Project,
            FieldBounds Bounds,
            FieldSurfaceSampler Surface
            )
        {
            List<(double Lat, double Lon, double Elev)> Points = new List<(double Lat, double Lon, double Elev)>();

            if (Project.Survey != null)
            {
                foreach (TopologyPoint Point in Project.Survey.BoundaryPoints)
                    Points.Add((Point.Latitude, Point.Longitude, Point.ExistingElevation));

                foreach (TopologyPoint Point in Project.Survey.InteriorPoints)
                    Points.Add((Point.Latitude, Point.Longitude, Point.ExistingElevation));
            }

            if (Points.Count == 0)
            {
                (double Lat, double Lon)[] Corners =
                {
                    (Bounds.MinLat, Bounds.MinLon),
                    (Bounds.MinLat, Bounds.MaxLon),
                    (Bounds.MaxLat, Bounds.MinLon),
                    (Bounds.MaxLat, Bounds.MaxLon)
                };

                foreach ((double Lat, double Lon) Corner in Corners)
                {
                    double Elev = Surface.SampleScalar(FieldContentView.Existing, Corner.Lat, Corner.Lon);
                    if (double.IsNaN(Elev))
                        Elev = Surface.MinElevation;
                    Points.Add((Corner.Lat, Corner.Lon, Elev));
                }
            }

            return Points;
        }

        /// <summary>
        /// Returns whether all sample points project inside the padded viewport at the given distance.
        /// </summary>
        static private bool FitsInViewport
            (
            List<(double Lat, double Lon, double Elev)> SamplePoints,
            FieldSurfaceSampler Surface,
            SKRect Viewport,
            double OrbitLat,
            double OrbitLon,
            double HeadingDeg,
            double PitchDeg,
            double DistanceM,
            double VerticalExaggeration,
            FieldContentView ReliefView
            )
        {
            FieldCamera3D Camera = new FieldCamera3D(
                Surface,
                HeadingDeg,
                PitchDeg,
                DistanceM,
                VerticalExaggeration,
                OrbitLat,
                OrbitLon,
                ReliefView);

            if (!TryGetProjectedBounds(SamplePoints, Camera, Viewport, out float MinX, out float MaxX, out float MinY, out float MaxY))
                return false;

            float MarginX = Viewport.Width * (float)FIT_MARGIN_FRACTION;
            float MarginY = Viewport.Height * (float)FIT_MARGIN_FRACTION;
            return (MinX >= Viewport.Left + MarginX) &&
                (MaxX <= Viewport.Right - MarginX) &&
                (MinY >= Viewport.Top + MarginY) &&
                (MaxY <= Viewport.Bottom - MarginY);
        }

        /// <summary>
        /// Projects sample points and returns their screen-space bounding box.
        /// </summary>
        static private bool TryGetProjectedBounds
            (
            List<(double Lat, double Lon, double Elev)> SamplePoints,
            FieldCamera3D Camera,
            SKRect Viewport,
            out float MinX,
            out float MaxX,
            out float MinY,
            out float MaxY
            )
        {
            MinX = float.MaxValue;
            MaxX = float.MinValue;
            MinY = float.MaxValue;
            MaxY = float.MinValue;

            foreach ((double Lat, double Lon, double Elev) Point in SamplePoints)
            {
                Camera.Project(Point.Lat, Point.Lon, Point.Elev, Viewport, FlatPlane: false, out float X, out float Y, out _);
                MinX = Math.Min(MinX, X);
                MaxX = Math.Max(MaxX, X);
                MinY = Math.Min(MinY, Y);
                MaxY = Math.Max(MaxY, Y);
            }

            return (MinX <= MaxX) && (MinY <= MaxY);
        }

        /// <summary>
        /// Resolves the active field content view used for 3D relief anchoring.
        /// </summary>
        static private FieldContentView ResolveReliefView
            (
            ProjectDocument Project
            )
        {
            FieldContentView View = Project.ViewerState.FieldContentView;
            if (View != FieldContentView.None)
                return View;
            if (Project.HasDesign)
                return FieldContentView.Proposed;
            return FieldContentView.Existing;
        }
    }
}
