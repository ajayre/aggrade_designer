using AgGradeDesigner.Core.Geo;
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
        /// Minimal AGS survey text used across import and serialization tests.
        /// </summary>
        public const string MINIMAL_AGS = """
            40.00361425,-93.49528194,100.000,0MB
            40.00000000,-93.50000000,99.195,2PER
            40.00000000,-93.49900000,99.195,2PER
            40.00100000,-93.49950000,99.500,2PER
            40.00100000,-93.49900000,99.500,3GRD
            40.00150000,-93.49900000,99.550,3GRD
            40.00200000,-93.49900000,99.600,3GRD
            40.00100000,-93.49850000,99.500,3GRD
            40.00150000,-93.49850000,99.550,3GRD
            40.00200000,-93.49850000,99.600,3GRD
            """;

        /// <summary>
        /// Minimal AGD design export text used across import and round-trip tests.
        /// </summary>
        public const string MINIMAL_AGD = """
            Latitude (deg), Longitude (deg), Elevation Existing(m), Elevation Proposed(m), CutFill(m), Code, Comments
            40.00361425,-93.49528194,100.000,,,0MB,
            40.00100000,-93.49900000,99.500,99.400,-0.100,3GRD,
            40.00150000,-93.49900000,99.550,99.450,-0.100,3GRD,
            40.00200000,-93.49900000,99.600,99.500,-0.100,3GRD,
            40.00100000,-93.49850000,99.500,99.400,-0.100,3GRD,
            40.00150000,-93.49850000,99.550,99.450,-0.100,3GRD,
            40.00200000,-93.49850000,99.600,99.500,-0.100,3GRD,
            40.00000000,-93.50000000,99.195,99.100,-0.095,2PER,
            40.00000000,-93.49900000,99.195,99.100,-0.095,2PER,
            40.00100000,-93.49950000,99.500,99.400,-0.100,2PER,
            """;

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
            Survey.Benchmarks.Add(new Benchmark(OriginLat, OriginLon, "0MB", 100.0));

            for (int Row = 0; Row < Rows; Row++)
            {
                for (int Col = 0; Col < Cols; Col++)
                {
                    double Lat = OriginLat + (Row * SpacingDeg);
                    double Lon = OriginLon + (Col * SpacingDeg);
                    double Elev = 100.0 + (Row * 0.01) + (Col * 0.005);
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
    }
}
