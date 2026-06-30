using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Survey;

namespace AgGradeDesigner.Core.Geo
{
    /// <summary>
    /// Geographic bounding box of a field derived from survey points or design grid bins.
    /// </summary>
    public readonly struct FieldBounds
    {
        /// <summary>Southern edge latitude in decimal degrees.</summary>
        public double MinLat { get; init; }

        /// <summary>Northern edge latitude in decimal degrees.</summary>
        public double MaxLat { get; init; }

        /// <summary>Western edge longitude in decimal degrees.</summary>
        public double MinLon { get; init; }

        /// <summary>Eastern edge longitude in decimal degrees.</summary>
        public double MaxLon { get; init; }

        /// <summary>Centre latitude of the bounding box.</summary>
        public double CentreLat => (MinLat + MaxLat) * 0.5;

        /// <summary>Centre longitude of the bounding box.</summary>
        public double CentreLon => (MinLon + MaxLon) * 0.5;

        /// <summary>
        /// Attempts to derive field bounds from a project document.
        /// </summary>
        /// <param name="Project">Project containing survey or design data.</param>
        /// <param name="Bounds">Output bounds when successful.</param>
        /// <returns>True when valid bounds could be computed.</returns>
        static public bool TryFromProject
            (
            ProjectDocument? Project,
            out FieldBounds Bounds
            )
        {
            Bounds = default;
            if ((Project == null) || !Project.HasSurvey)
                return false;

            // Prefer design grid centroids when a design exists for tighter framing.
            if (Project.HasDesign && (Project.DesignGrid != null))
                return TryFromGrid(Project.DesignGrid, out Bounds);

            return TryFromSurvey(Project.Survey!, out Bounds);
        }

        /// <summary>
        /// Derives bounds from valid design grid bin centroids.
        /// </summary>
        /// <param name="Grid">Design grid with bin centroids.</param>
        /// <param name="Bounds">Output bounds when successful.</param>
        /// <returns>True when at least one valid bin was found.</returns>
        static public bool TryFromGrid
            (
            DesignGrid Grid,
            out FieldBounds Bounds
            )
        {
            Bounds = default;
            double MinLat = double.MaxValue;
            double MaxLat = double.MinValue;
            double MinLon = double.MaxValue;
            double MaxLon = double.MinValue;
            bool Found = false;

            foreach (GridBinData Bin in Grid.Bins)
            {
                if (!Bin.Valid)
                    continue;

                MinLat = Math.Min(MinLat, Bin.CentroidLat);
                MaxLat = Math.Max(MaxLat, Bin.CentroidLat);
                MinLon = Math.Min(MinLon, Bin.CentroidLon);
                MaxLon = Math.Max(MaxLon, Bin.CentroidLon);
                Found = true;
            }

            if (!Found || (MinLat >= MaxLat))
                return false;

            Bounds = new FieldBounds
            {
                MinLat = MinLat,
                MaxLat = MaxLat,
                MinLon = MinLon,
                MaxLon = MaxLon
            };
            return true;
        }

        /// <summary>
        /// Derives bounds from all survey interior and boundary points.
        /// </summary>
        /// <param name="Survey">Survey model with topology points.</param>
        /// <param name="Bounds">Output bounds when successful.</param>
        /// <returns>True when at least one point was found.</returns>
        static public bool TryFromSurvey
            (
            SurveyModel Survey,
            out FieldBounds Bounds
            )
        {
            Bounds = default;
            List<(double Lat, double Lon)> Points = new List<(double Lat, double Lon)>();
            foreach (TopologyPoint Pt in Survey.InteriorPoints)
                Points.Add((Pt.Latitude, Pt.Longitude));
            foreach (TopologyPoint Pt in Survey.BoundaryPoints)
                Points.Add((Pt.Latitude, Pt.Longitude));

            if (Points.Count == 0)
                return false;

            Bounds = new FieldBounds
            {
                MinLat = Points.Min(P => P.Lat),
                MaxLat = Points.Max(P => P.Lat),
                MinLon = Points.Min(P => P.Lon),
                MaxLon = Points.Max(P => P.Lon)
            };
            return true;
        }
    }
}
