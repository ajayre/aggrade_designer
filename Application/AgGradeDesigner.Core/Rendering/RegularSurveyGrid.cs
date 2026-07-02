using AgGradeDesigner.Core.Models;

using AgGradeDesigner.Core.Survey;



namespace AgGradeDesigner.Core.Rendering

{

    /// <summary>

    /// Detects regular lat/lon survey grids and samples them with bilinear interpolation.

    /// </summary>

    internal sealed class RegularSurveyGrid

    {

        /// <summary>Fixed-point scale factor for quantizing lat/lon grid keys.</summary>
        private const long QUANT_SCALE = 100_000_000L;

        /// <summary>Minimum interior point count required to detect a regular grid.</summary>
        private const int MIN_INTERIOR_POINTS = 4;



        /// <summary>Ascending latitude axis coordinates for the detected grid.</summary>
        private readonly double[] _Lats;

        /// <summary>Ascending longitude axis coordinates for the detected grid.</summary>
        private readonly double[] _Lons;

        /// <summary>Flattened elevation values indexed by lat then lon.</summary>
        private readonly double[] _Values;



        /// <summary>

        /// Creates a grid from pre-sorted axis coordinates and flattened cell values.

        /// </summary>

        /// <param name="Lats">Ascending latitude axis coordinates.</param>

        /// <param name="Lons">Ascending longitude axis coordinates.</param>

        /// <param name="Values">Flattened elevation values indexed by lat then lon.</param>

        private RegularSurveyGrid

            (

            double[] Lats,

            double[] Lons,

            double[] Values

            )

        {

            // Snapshot axis coordinates and flattened values for bilinear sampling.
            _Lats = Lats;

            _Lons = Lons;

            _Values = Values;

        }



        /// <summary>

        /// Attempts to detect a complete rectangular lat/lon grid in survey interior points.

        /// </summary>

        /// <param name="Survey">Survey model with interior topology points.</param>

        /// <param name="Grid">Output grid when detection succeeds.</param>

        /// <returns>True when interior points form a regular grid without duplicates or gaps.</returns>

        static public bool TryBuild

            (

            SurveyModel Survey,

            out RegularSurveyGrid? Grid

            )

        {

            Grid = null;

            if (Survey.InteriorPoints.Count < MIN_INTERIOR_POINTS)

                return false;



            // Quantize lat/lon keys so duplicate and missing grid cells are detectable.
            SortedSet<long> LatKeys = new SortedSet<long>();

            SortedSet<long> LonKeys = new SortedSet<long>();

            Dictionary<(long LatKey, long LonKey), double> ValuesByCell = new Dictionary<(long LatKey, long LonKey), double>();



            foreach (TopologyPoint Pt in Survey.InteriorPoints)

            {

                long LatKey = Quantize(Pt.Latitude);

                long LonKey = Quantize(Pt.Longitude);

                LatKeys.Add(LatKey);

                LonKeys.Add(LonKey);



                (long LatKey, long LonKey) Cell = (LatKey, LonKey);

                if (ValuesByCell.ContainsKey(Cell))

                    return false;



                ValuesByCell[Cell] = Pt.ExistingElevation;

            }



            if (ValuesByCell.Count != Survey.InteriorPoints.Count)

                return false;



            int LatCount = LatKeys.Count;

            int LonCount = LonKeys.Count;

            if (LatCount * LonCount != ValuesByCell.Count)

                return false;



            double[] Lats = LatKeys.Select(Dequantize).ToArray();

            double[] Lons = LonKeys.Select(Dequantize).ToArray();

            double[] Values = new double[LatCount * LonCount];



            for (int LatIndex = 0; LatIndex < LatCount; LatIndex++)

            {

                for (int LonIndex = 0; LonIndex < LonCount; LonIndex++)

                {

                    if (!ValuesByCell.TryGetValue((Quantize(Lats[LatIndex]), Quantize(Lons[LonIndex])), out double Value))

                        return false;



                    Values[LatIndex * LonCount + LonIndex] = Value;

                }

            }



            Grid = new RegularSurveyGrid(Lats, Lons, Values);

            return true;

        }



        /// <summary>

        /// Bilinearly interpolates elevation at a lat/lon location within the grid extent.

        /// </summary>

        /// <param name="Lat">Sample latitude in decimal degrees.</param>

        /// <param name="Lon">Sample longitude in decimal degrees.</param>

        /// <returns>Interpolated elevation in metres, or NaN when outside the grid.</returns>

        public double Sample

            (

            double Lat,

            double Lon

            )

        {

            if (_Lats.Length == 0 || _Lons.Length == 0)

                return double.NaN;



            // Reject samples outside the rectangular grid extent.
            if (Lat < _Lats[0] || Lat > _Lats[^1] || Lon < _Lons[0] || Lon > _Lons[^1])

                return double.NaN;



            int LatIndex = FindBracket(_Lats, Lat);

            int LonIndex = FindBracket(_Lons, Lon);

            if (LatIndex < 0 || LonIndex < 0)

                return double.NaN;



            int Lat1 = Math.Min(LatIndex + 1, _Lats.Length - 1);

            int Lon1 = Math.Min(LonIndex + 1, _Lons.Length - 1);

            double LatT = _Lats[Lat1] > _Lats[LatIndex]

                ? (Lat - _Lats[LatIndex]) / (_Lats[Lat1] - _Lats[LatIndex])

                : 0.0;

            double LonT = _Lons[Lon1] > _Lons[LonIndex]

                ? (Lon - _Lons[LonIndex]) / (_Lons[Lon1] - _Lons[LonIndex])

                : 0.0;



            double V00 = _Values[LatIndex * _Lons.Length + LonIndex];

            double V01 = _Values[LatIndex * _Lons.Length + Lon1];

            double V10 = _Values[Lat1 * _Lons.Length + LonIndex];

            double V11 = _Values[Lat1 * _Lons.Length + Lon1];



            double Top = V00 + (V01 - V00) * LonT;

            double Bottom = V10 + (V11 - V10) * LonT;

            return Top + (Bottom - Top) * LatT;

        }



        /// <summary>

        /// Finds the lower bracket index in a sorted axis for bilinear interpolation.

        /// </summary>

        /// <param name="Sorted">Ascending sorted axis coordinates.</param>

        /// <param name="Value">Sample coordinate to bracket.</param>

        /// <returns>Lower index, or -1 when outside the axis range.</returns>

        static private int FindBracket

            (

            double[] Sorted,

            double Value

            )

        {

            if (Value < Sorted[0] || Value > Sorted[^1])

                return -1;



            // Binary search for the lower bracket index on an ascending axis.
            int Low = 0;

            int High = Sorted.Length - 1;

            while (Low < High)

            {

                int Mid = (Low + High + 1) >> 1;

                if (Sorted[Mid] <= Value)

                    Low = Mid;

                else

                    High = Mid - 1;

            }



            return Low;

        }



        /// <summary>

        /// Quantizes a coordinate to a fixed-point key for grid cell lookup.

        /// </summary>

        /// <param name="Value">Coordinate in decimal degrees.</param>

        /// <returns>Fixed-point key.</returns>

        static private long Quantize

            (

            double Value

            )

        {

            // Fixed-point keys avoid floating-point hash collisions in grid lookup.
            return (long)Math.Round(Value * QUANT_SCALE);

        }



        /// <summary>

        /// Restores a decimal-degree coordinate from a fixed-point key.

        /// </summary>

        /// <param name="Key">Fixed-point key.</param>

        /// <returns>Coordinate in decimal degrees.</returns>

        static private double Dequantize

            (

            long Key

            )

        {

            return Key / (double)QUANT_SCALE;

        }

    }

}


