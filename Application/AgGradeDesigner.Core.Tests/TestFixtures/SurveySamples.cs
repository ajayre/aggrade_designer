using System.Globalization;

using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Survey;

namespace AgGradeDesigner.Core.Tests.TestFixtures
{
    /// <summary>
    /// Minimal survey and design text fixtures shared across Core unit tests.
    /// </summary>

    static internal class SurveySamples
    {
        /// <summary>
        /// Mb lat used by this component.
        /// </summary>

        private const double MB_LAT = 40.00361425;
        /// <summary>
        /// Mb lon used by this component.
        /// </summary>

        private const double MB_LON = -93.49528194;
        /// <summary>
        /// Mb elevation m used by this component.
        /// </summary>

        private const double MB_ELEVATION_M = 100.000;
        /// <summary>
        /// Boundary lat south used by this component.
        /// </summary>

        private const double BOUNDARY_LAT_SOUTH = 40.00000000;
        /// <summary>
        /// Boundary lon west used by this component.
        /// </summary>

        private const double BOUNDARY_LON_WEST = -93.50000000;
        /// <summary>
        /// Boundary lon east used by this component.
        /// </summary>

        private const double BOUNDARY_LON_EAST = -93.49900000;
        /// <summary>
        /// Boundary lat north used by this component.
        /// </summary>

        private const double BOUNDARY_LAT_NORTH = 40.00100000;
        /// <summary>
        /// Boundary lat mid used by this component.
        /// </summary>

        private const double BOUNDARY_LAT_MID = 40.00100000;
        /// <summary>
        /// Boundary lon mid used by this component.
        /// </summary>

        private const double BOUNDARY_LON_MID = -93.49950000;
        /// <summary>
        /// Boundary elevation m used by this component.
        /// </summary>

        private const double BOUNDARY_ELEVATION_M = 99.195;
        /// <summary>
        /// Boundary elevation mid m used by this component.
        /// </summary>

        private const double BOUNDARY_ELEVATION_MID_M = 99.500;
        /// <summary>
        /// Interior elevation base m used by this component.
        /// </summary>

        private const double INTERIOR_ELEVATION_BASE_M = 99.500;
        /// <summary>
        /// Interior elevation mid m used by this component.
        /// </summary>

        private const double INTERIOR_ELEVATION_MID_M = 99.550;
        /// <summary>
        /// Interior elevation high m used by this component.
        /// </summary>

        private const double INTERIOR_ELEVATION_HIGH_M = 99.600;
        /// <summary>
        /// Interior lat row a used by this component.
        /// </summary>

        private const double INTERIOR_LAT_ROW_A = 40.00100000;
        /// <summary>
        /// Interior lat row b used by this component.
        /// </summary>

        private const double INTERIOR_LAT_ROW_B = 40.00150000;
        /// <summary>
        /// Interior lat row c used by this component.
        /// </summary>

        private const double INTERIOR_LAT_ROW_C = 40.00200000;
        /// <summary>
        /// Interior lon col a used by this component.
        /// </summary>

        private const double INTERIOR_LON_COL_A = -93.49900000;
        /// <summary>
        /// Interior lon col b used by this component.
        /// </summary>

        private const double INTERIOR_LON_COL_B = -93.49850000;
        /// <summary>
        /// Agd cut fill delta m used by this component.
        /// </summary>

        private const double AGD_CUT_FILL_DELTA_M = -0.100;
        /// <summary>
        /// Agd boundary cut fill delta m used by this component.
        /// </summary>

        private const double AGD_BOUNDARY_CUT_FILL_DELTA_M = -0.095;
        /// <summary>
        /// Grid benchmark elevation m used by this component.
        /// </summary>

        private const double GRID_BENCHMARK_ELEVATION_M = 100.0;
        /// <summary>
        /// Grid row elevation increment m used by this component.
        /// </summary>

        private const double GRID_ROW_ELEVATION_INCREMENT_M = 0.01;
        /// <summary>
        /// Grid col elevation increment m used by this component.
        /// </summary>

        private const double GRID_COL_ELEVATION_INCREMENT_M = 0.005;

        /// <summary>
        /// Minimal AGS survey text used across import and serialization tests.
        /// </summary>
        public static string MINIMAL_AGS =>
            string.Join('\n', new[]
            {
                FormatAgsRow(MB_LAT, MB_LON, MB_ELEVATION_M, "0MB"),
                FormatAgsRow(BOUNDARY_LAT_SOUTH, BOUNDARY_LON_WEST, BOUNDARY_ELEVATION_M, "2PER"),
                FormatAgsRow(BOUNDARY_LAT_SOUTH, BOUNDARY_LON_EAST, BOUNDARY_ELEVATION_M, "2PER"),
                FormatAgsRow(BOUNDARY_LAT_MID, BOUNDARY_LON_MID, BOUNDARY_ELEVATION_MID_M, "2PER"),
                FormatAgsRow(INTERIOR_LAT_ROW_A, INTERIOR_LON_COL_A, INTERIOR_ELEVATION_BASE_M, "3GRD"),
                FormatAgsRow(INTERIOR_LAT_ROW_B, INTERIOR_LON_COL_A, INTERIOR_ELEVATION_MID_M, "3GRD"),
                FormatAgsRow(INTERIOR_LAT_ROW_C, INTERIOR_LON_COL_A, INTERIOR_ELEVATION_HIGH_M, "3GRD"),
                FormatAgsRow(INTERIOR_LAT_ROW_A, INTERIOR_LON_COL_B, INTERIOR_ELEVATION_BASE_M, "3GRD"),
                FormatAgsRow(INTERIOR_LAT_ROW_B, INTERIOR_LON_COL_B, INTERIOR_ELEVATION_MID_M, "3GRD"),
                FormatAgsRow(INTERIOR_LAT_ROW_C, INTERIOR_LON_COL_B, INTERIOR_ELEVATION_HIGH_M, "3GRD"),
            }) + "\n";

        /// <summary>
        /// Minimal AGD design export text used across import and round-trip tests.
        /// </summary>
        public static string MINIMAL_AGD =>
            string.Join('\n', new[]
            {
                "Latitude (deg), Longitude (deg), Elevation Existing(m), Elevation Proposed(m), CutFill(m), Code, Comments",
                FormatAgdRow(MB_LAT, MB_LON, MB_ELEVATION_M, null, null, "0MB"),
                FormatAgdRow(INTERIOR_LAT_ROW_A, INTERIOR_LON_COL_A, INTERIOR_ELEVATION_BASE_M, INTERIOR_ELEVATION_BASE_M + AGD_CUT_FILL_DELTA_M, AGD_CUT_FILL_DELTA_M, "3GRD"),
                FormatAgdRow(INTERIOR_LAT_ROW_B, INTERIOR_LON_COL_A, INTERIOR_ELEVATION_MID_M, INTERIOR_ELEVATION_MID_M + AGD_CUT_FILL_DELTA_M, AGD_CUT_FILL_DELTA_M, "3GRD"),
                FormatAgdRow(INTERIOR_LAT_ROW_C, INTERIOR_LON_COL_A, INTERIOR_ELEVATION_HIGH_M, INTERIOR_ELEVATION_HIGH_M + AGD_CUT_FILL_DELTA_M, AGD_CUT_FILL_DELTA_M, "3GRD"),
                FormatAgdRow(INTERIOR_LAT_ROW_A, INTERIOR_LON_COL_B, INTERIOR_ELEVATION_BASE_M, INTERIOR_ELEVATION_BASE_M + AGD_CUT_FILL_DELTA_M, AGD_CUT_FILL_DELTA_M, "3GRD"),
                FormatAgdRow(INTERIOR_LAT_ROW_B, INTERIOR_LON_COL_B, INTERIOR_ELEVATION_MID_M, INTERIOR_ELEVATION_MID_M + AGD_CUT_FILL_DELTA_M, AGD_CUT_FILL_DELTA_M, "3GRD"),
                FormatAgdRow(INTERIOR_LAT_ROW_C, INTERIOR_LON_COL_B, INTERIOR_ELEVATION_HIGH_M, INTERIOR_ELEVATION_HIGH_M + AGD_CUT_FILL_DELTA_M, AGD_CUT_FILL_DELTA_M, "3GRD"),
                FormatAgdRow(BOUNDARY_LAT_SOUTH, BOUNDARY_LON_WEST, BOUNDARY_ELEVATION_M, BOUNDARY_ELEVATION_M + AGD_BOUNDARY_CUT_FILL_DELTA_M, AGD_BOUNDARY_CUT_FILL_DELTA_M, "2PER"),
                FormatAgdRow(BOUNDARY_LAT_SOUTH, BOUNDARY_LON_EAST, BOUNDARY_ELEVATION_M, BOUNDARY_ELEVATION_M + AGD_BOUNDARY_CUT_FILL_DELTA_M, AGD_BOUNDARY_CUT_FILL_DELTA_M, "2PER"),
                FormatAgdRow(BOUNDARY_LAT_MID, BOUNDARY_LON_MID, BOUNDARY_ELEVATION_MID_M, BOUNDARY_ELEVATION_MID_M + AGD_CUT_FILL_DELTA_M, AGD_CUT_FILL_DELTA_M, "2PER"),
            }) + "\n";

        /// <summary>
        /// Builds a rectangular grid survey with boundary and interior topology points.
        /// </summary>
        /// <param name="Rows">Number of grid rows.</param>
        /// <param name="Cols">Number of grid columns.</param>
        /// <param name="OriginLat">South-west corner latitude in degrees.</param>
        /// <param name="OriginLon">South-west corner longitude in degrees.</param>
        /// <param name="SpacingDeg">Lat/lon spacing between adjacent points in degrees.</param>
        /// <returns>Populated survey model ready for design or serialization tests.</returns>
        static public SurveyModel CreateGridSurvey
            (
            int Rows,
            int Cols,
            double OriginLat,
            double OriginLon,
            double SpacingDeg
            )
        {
            SurveyModel Survey = new SurveyModel();
            Survey.Benchmarks.Add(new Benchmark(OriginLat, OriginLon, "0MB", GRID_BENCHMARK_ELEVATION_M));

            for (int Row = 0; Row < Rows; Row++)
            {
                for (int Col = 0; Col < Cols; Col++)
                {
                    double Lat = OriginLat + (Row * SpacingDeg);
                    double Lon = OriginLon + (Col * SpacingDeg);
                    double Elev = GRID_BENCHMARK_ELEVATION_M + (Row * GRID_ROW_ELEVATION_INCREMENT_M) + (Col * GRID_COL_ELEVATION_INCREMENT_M);
                    bool IsBoundary = (Row == 0) || (Col == 0) || (Row == (Rows - 1)) || (Col == (Cols - 1));
                    TopologyPoint Point = new TopologyPoint
                    {
                        Latitude = Lat,
                        Longitude = Lon,
                        ExistingElevation = Elev,
                        Code = IsBoundary ? "2PER" : "3GRD"
                    };
                    // Perimeter codes classify boundary rings separately from interior grids.
                    if (IsBoundary)
                        Survey.BoundaryPoints.Add(Point);
                    else
                        Survey.InteriorPoints.Add(Point);
                }
            }

            return Survey;
        }

        static private string FormatAgsRow
            (
            double Latitude,
            double Longitude,
            double ElevationM,
            string Code
            )
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0},{1},{2},{3}",
                Latitude,
                Longitude,
                ElevationM,
                Code);
        }

        static private string FormatAgdRow
            (
            double Latitude,
            double Longitude,
            double ExistingM,
            double? ProposedM,
            double? CutFillM,
            string Code
            )
        {
            string ProposedText = ProposedM.HasValue
                ? ProposedM.Value.ToString("F3", CultureInfo.InvariantCulture)
                : string.Empty;
            string CutFillText = CutFillM.HasValue
                ? CutFillM.Value.ToString("F3", CultureInfo.InvariantCulture)
                : string.Empty;
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0},{1},{2:F3},{3},{4},{5},",
                Latitude,
                Longitude,
                ExistingM,
                ProposedText,
                CutFillText,
                Code);
        }
    }
}
