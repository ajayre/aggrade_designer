namespace AgGradeDesigner.Core.Rendering
{
    /// <summary>
    /// Fast k-nearest weighted elevation lookup for survey point clouds (§2.4.14).
    /// </summary>
    internal sealed class SurveySpatialInterpolator
    {
        private const double CELL_SIZE_M = 25.0;
        private const int NEIGHBOUR_COUNT = 48;
        private const double SMOOTH_FRACTION = 0.05;

        private readonly double[] _Easting;
        private readonly double[] _Northing;
        private readonly double[] _Elevation;
        private readonly Dictionary<(int Ix, int Iy), List<int>> _Buckets;
        private readonly int _MaxRing;
        private readonly double[] _DistSqBuffer;
        private readonly int[] _IndexBuffer;

        /// <summary>
        /// Builds a spatial hash of survey points for k-nearest inverse-distance weighting.
        /// </summary>
        /// <param name="Points">Survey points as UTM easting, northing, and elevation tuples.</param>
        public SurveySpatialInterpolator
            (
            IReadOnlyList<(double E, double N, double Z)> Points
            )
        {
            int Count = Points.Count;
            _Easting = new double[Count];
            _Northing = new double[Count];
            _Elevation = new double[Count];
            _Buckets = new Dictionary<(int Ix, int Iy), List<int>>();

            double MinE = double.MaxValue;
            double MaxE = double.MinValue;
            double MinN = double.MaxValue;
            double MaxN = double.MinValue;

            for (int I = 0; I < Count; I++)
            {
                _Easting[I] = Points[I].E;
                _Northing[I] = Points[I].N;
                _Elevation[I] = Points[I].Z;

                MinE = Math.Min(MinE, Points[I].E);
                MaxE = Math.Max(MaxE, Points[I].E);
                MinN = Math.Min(MinN, Points[I].N);
                MaxN = Math.Max(MaxN, Points[I].N);

                int Ix = (int)Math.Floor(Points[I].E / CELL_SIZE_M);
                int Iy = (int)Math.Floor(Points[I].N / CELL_SIZE_M);
                (int Ix, int Iy) Key = (Ix, Iy);
                if (!_Buckets.TryGetValue(Key, out List<int>? List))
                {
                    List = new List<int>();
                    _Buckets[Key] = List;
                }

                List.Add(I);
            }

            // Expand search rings until the farthest neighbour exceeds the current ring radius.
            double SpanM = Math.Max(MaxE - MinE, MaxN - MinN);
            _MaxRing = (int)Math.Ceiling(SpanM / CELL_SIZE_M) + 3;
            _DistSqBuffer = new double[NEIGHBOUR_COUNT];
            _IndexBuffer = new int[NEIGHBOUR_COUNT];
        }

        /// <summary>
        /// Interpolates elevation at a UTM location using inverse-distance weighting of nearest neighbours.
        /// </summary>
        /// <param name="Easting">Query easting in metres.</param>
        /// <param name="Northing">Query northing in metres.</param>
        /// <returns>Weighted elevation in metres, or NaN when no neighbours are found.</returns>
        public double Interpolate
            (
            double Easting,
            double Northing
            )
        {
            GatherNearest(Easting, Northing);

            // Exact hit on a survey point avoids division by zero in weighting.
            if (_DistSqBuffer[0] < 1e-8)
                return _Elevation[_IndexBuffer[0]];

            int Count = 0;
            for (int I = 0; I < NEIGHBOUR_COUNT; I++)
            {
                if (_DistSqBuffer[I] >= double.PositiveInfinity)
                    break;
                Count++;
            }

            if (Count == 0)
                return double.NaN;

            double MaxDistSq = Math.Max(_DistSqBuffer[Count - 1], 1e-8);
            double Smooth = SMOOTH_FRACTION * SMOOTH_FRACTION * MaxDistSq;
            double WeightSum = 0;
            double ValueSum = 0;

            for (int I = 0; I < Count; I++)
            {
                double W = 1.0 / (_DistSqBuffer[I] + Smooth);
                WeightSum += W;
                ValueSum += W * _Elevation[_IndexBuffer[I]];
            }

            return WeightSum > 0 ? ValueSum / WeightSum : double.NaN;
        }

        /// <summary>
        /// Collects the k nearest survey points into reusable distance buffers.
        /// </summary>
        /// <param name="Easting">Query easting in metres.</param>
        /// <param name="Northing">Query northing in metres.</param>
        private void GatherNearest
            (
            double Easting,
            double Northing
            )
        {
            for (int I = 0; I < NEIGHBOUR_COUNT; I++)
                _DistSqBuffer[I] = double.PositiveInfinity;

            int QueryX = (int)Math.Floor(Easting / CELL_SIZE_M);
            int QueryY = (int)Math.Floor(Northing / CELL_SIZE_M);

            for (int Ring = 0; Ring < _MaxRing; Ring++)
            {
                for (int Dx = -Ring; Dx <= Ring; Dx++)
                {
                    for (int Dy = -Ring; Dy <= Ring; Dy++)
                    {
                        if (Ring > 0 && Math.Abs(Dx) < Ring && Math.Abs(Dy) < Ring)
                            continue;

                        if (!_Buckets.TryGetValue((QueryX + Dx, QueryY + Dy), out List<int>? List))
                            continue;

                        foreach (int Idx in List)
                            ConsiderCandidate(Easting, Northing, Idx);
                    }
                }

                if (FilledCount() >= NEIGHBOUR_COUNT)
                {
                    double WorstSq = WorstDistanceSq();
                    if ((Ring + 1) * CELL_SIZE_M > Math.Sqrt(WorstSq) + 1e-6)
                        break;
                }
            }

            // Fallback scan when the spatial hash ring search under-fills the neighbour buffer.
            if (FilledCount() < NEIGHBOUR_COUNT)
            {
                for (int I = 0; I < _Easting.Length; I++)
                    ConsiderCandidate(Easting, Northing, I);
            }
        }

        /// <summary>
        /// Inserts a candidate point into the k-nearest distance buffer if it qualifies.
        /// </summary>
        /// <param name="Easting">Query easting in metres.</param>
        /// <param name="Northing">Query northing in metres.</param>
        /// <param name="Idx">Index of the candidate survey point.</param>
        private void ConsiderCandidate
            (
            double Easting,
            double Northing,
            int Idx
            )
        {
            double Dx = _Easting[Idx] - Easting;
            double Dy = _Northing[Idx] - Northing;
            double DistSq = Dx * Dx + Dy * Dy;

            int Slot = -1;
            for (int I = 0; I < NEIGHBOUR_COUNT; I++)
            {
                if (_DistSqBuffer[I] >= double.PositiveInfinity)
                {
                    Slot = I;
                    break;
                }
            }

            if (Slot >= 0)
            {
                _DistSqBuffer[Slot] = DistSq;
                _IndexBuffer[Slot] = Idx;
                return;
            }

            int Worst = 0;
            double WorstDistSq = _DistSqBuffer[0];
            for (int I = 1; I < NEIGHBOUR_COUNT; I++)
            {
                if (_DistSqBuffer[I] > WorstDistSq)
                {
                    WorstDistSq = _DistSqBuffer[I];
                    Worst = I;
                }
            }

            if (DistSq < WorstDistSq)
            {
                _DistSqBuffer[Worst] = DistSq;
                _IndexBuffer[Worst] = Idx;
            }
        }

        /// <summary>
        /// Returns how many slots in the k-nearest buffer are filled.
        /// </summary>
        /// <returns>Count of finite distance entries.</returns>
        private int FilledCount
            (
            )
        {
            int Count = 0;
            for (int I = 0; I < NEIGHBOUR_COUNT; I++)
            {
                if (_DistSqBuffer[I] < double.PositiveInfinity)
                    Count++;
            }

            return Count;
        }

        /// <summary>
        /// Returns the largest squared distance among filled k-nearest slots.
        /// </summary>
        /// <returns>Worst-case squared distance in the current neighbour set.</returns>
        private double WorstDistanceSq
            (
            )
        {
            double Worst = 0;
            for (int I = 0; I < NEIGHBOUR_COUNT; I++)
            {
                if (_DistSqBuffer[I] < double.PositiveInfinity && _DistSqBuffer[I] > Worst)
                    Worst = _DistSqBuffer[I];
            }

            return Worst;
        }
    }
}
