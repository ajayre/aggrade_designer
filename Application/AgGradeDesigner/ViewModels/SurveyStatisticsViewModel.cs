using System.Globalization;

using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Survey;
using AgGradeDesigner.Core.Units;

using CommunityToolkit.Mvvm.ComponentModel;

namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// Read-only survey statistics for the survey statistics dialog (§3.5).
    /// </summary>
    public sealed partial class SurveyStatisticsViewModel : ViewModelBase
    {
        /// <summary>
        /// Whether survey statistics can be shown for the loaded project.
        /// </summary>
        [ObservableProperty]
        private bool _HasSurvey;

        /// <summary>
        /// Empty-state or informational message when no survey is loaded.
        /// </summary>
        [ObservableProperty]
        private string _Message = "No survey data is loaded.";

        /// <summary>
        /// Total count of interior, boundary, and benchmark points.
        /// </summary>
        [ObservableProperty]
        private int _TotalPoints;

        /// <summary>
        /// Count of interior grid topology points.
        /// </summary>
        [ObservableProperty]
        private int _InteriorCount;

        /// <summary>
        /// Count of boundary perimeter topology points.
        /// </summary>
        [ObservableProperty]
        private int _BoundaryCount;

        /// <summary>
        /// Count of benchmark control points.
        /// </summary>
        [ObservableProperty]
        private int _BenchmarkCount;

        /// <summary>
        /// Formatted field area in acres derived from the boundary polygon.
        /// </summary>
        [ObservableProperty]
        private string _AreaAcres = "";

        /// <summary>
        /// Formatted minimum existing elevation across all points.
        /// </summary>
        [ObservableProperty]
        private string _ElevationMin = "";

        /// <summary>
        /// Formatted maximum existing elevation across all points.
        /// </summary>
        [ObservableProperty]
        private string _ElevationMax = "";

        /// <summary>
        /// Formatted elevation span from minimum to maximum.
        /// </summary>
        [ObservableProperty]
        private string _ElevationRange = "";

        /// <summary>
        /// Label for the master benchmark, or a none placeholder.
        /// </summary>
        [ObservableProperty]
        private string _MasterBenchmark = "None";

        /// <summary>
        /// Populates read-only statistics from the loaded survey model.
        /// </summary>
        /// <param name="Survey">Survey to summarize, or null when none is loaded.</param>
        /// <param name="DisplayUnits">Display unit system for formatted labels.</param>
        public void LoadFrom
            (
            SurveyModel? Survey,
            DisplayUnitSystem DisplayUnits = DisplayUnitSystem.Imperial
            )
        {
            if (Survey == null ||
                (Survey.InteriorPoints.Count == 0 &&
                 Survey.BoundaryPoints.Count == 0 &&
                 Survey.Benchmarks.Count == 0))
            {
                HasSurvey = false;
                Message = "No survey data is loaded.";
                return;
            }

            HasSurvey = true;
            Message = "";

            InteriorCount = Survey.InteriorPoints.Count;
            BoundaryCount = Survey.BoundaryPoints.Count;
            BenchmarkCount = Survey.Benchmarks.Count;
            TotalPoints = InteriorCount + BoundaryCount + BenchmarkCount;

            if (Survey.BoundaryPoints.Count >= GeoConstants.MIN_POLYGON_VERTICES)
            {
                List<(double Lat, double Lon)> Ring = new List<(double Lat, double Lon)>();
                foreach (TopologyPoint Pt in Survey.BoundaryPoints)
                    Ring.Add((Pt.Latitude, Pt.Longitude));
                double Acres = PolygonArea.ComputeAreaAcresFromLatLonRing(Ring);
                AreaAcres = DisplayUnitFormatter.FormatAreaFromAcres(Acres, DisplayUnits);
            }
            else
            {
                AreaAcres = "Area: —";
            }

            double Min = double.MaxValue;
            double Max = double.MinValue;

            // Track extrema across all point categories in one pass per collection.
            /// <summary>
            /// Updates running min and max elevation while iterating survey points.
            /// </summary>
            /// <param name="Elevation">Candidate elevation in metres.</param>
            void Consider
                (
                double Elevation
                )
            {
                if (Elevation < Min) Min = Elevation;
                if (Elevation > Max) Max = Elevation;
            }

            foreach (TopologyPoint Pt in Survey.InteriorPoints)
                Consider(Pt.ExistingElevation);
            foreach (TopologyPoint Pt in Survey.BoundaryPoints)
                Consider(Pt.ExistingElevation);
            foreach (Benchmark Bm in Survey.Benchmarks)
                Consider(Bm.Elevation);

            if (Min <= Max)
            {
                ElevationMin = DisplayUnitFormatter.FormatElevation(Min, DisplayUnits);
                ElevationMax = DisplayUnitFormatter.FormatElevation(Max, DisplayUnits);
                ElevationRange = DisplayUnitFormatter.FormatElevation(Max - Min, DisplayUnits);
            }

            // Prefer a benchmark tagged as master; otherwise show the first listed benchmark.
            Benchmark? Master = Survey.Benchmarks.Find(B =>
                B.Name.Contains("mb", StringComparison.OrdinalIgnoreCase));
            if (Master != null)
            {
                MasterBenchmark = string.Format(CultureInfo.InvariantCulture,
                    "{0} ({1})", Master.Name, DisplayUnitFormatter.FormatElevation(Master.Elevation, DisplayUnits));
            }
            else if (Survey.Benchmarks.Count > 0)
            {
                Benchmark First = Survey.Benchmarks[0];
                MasterBenchmark = string.Format(CultureInfo.InvariantCulture,
                    "{0} ({1})", First.Name, DisplayUnitFormatter.FormatElevation(First.Elevation, DisplayUnits));
            }
            else
            {
                MasterBenchmark = "None";
            }
        }
    }
}
