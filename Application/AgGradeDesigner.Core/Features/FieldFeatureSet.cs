using AgGradeDesigner.Core;

namespace AgGradeDesigner.Core.Features
{
    /// <summary>
    /// Wells, roads, and reference points owned by one project.
    /// </summary>
    public sealed class FieldFeatureSet
    {
        /// <summary>
        /// Default pad diameter in feet. Twice the haul pad turn radius plus clearance
        /// (42 ft + 3 ft) until the user types a different diameter.
        /// </summary>
        public const double DEFAULT_PAD_DIAMETER_FT = 90.0;

        /// <summary>Minimum vertices that form a road with a start and an end.</summary>
        public const int MIN_ROAD_VERTICES = 2;

        /// <summary>Committed wells in creation order.</summary>
        private readonly List<WellFeature> _Wells = new List<WellFeature>();

        /// <summary>Committed roads in creation order.</summary>
        private readonly List<RoadFeature> _Roads = new List<RoadFeature>();

        /// <summary>Committed reference points in creation order.</summary>
        private readonly List<ReferencePointFeature> _ReferencePoints = new List<ReferencePointFeature>();

        /// <summary>Next numeric suffix for a new well id.</summary>
        private int _NextWellNumber = 1;

        /// <summary>Next numeric suffix for a new road id.</summary>
        private int _NextRoadNumber = 1;

        /// <summary>Next numeric suffix for a new reference point id.</summary>
        private int _NextReferenceNumber = 1;

        /// <summary>Default pad diameter in metres for a newly placed well.</summary>
        static public double DefaultPadDiameterM => DEFAULT_PAD_DIAMETER_FT * FieldConstants.METERS_PER_FOOT;

        /// <summary>Committed wells in creation order.</summary>
        public IReadOnlyList<WellFeature> Wells => _Wells;

        /// <summary>Committed roads in creation order.</summary>
        public IReadOnlyList<RoadFeature> Roads => _Roads;

        /// <summary>Committed reference points in creation order.</summary>
        public IReadOnlyList<ReferencePointFeature> ReferencePoints => _ReferencePoints;

        /// <summary>Next well id suffix that will be assigned.</summary>
        public int NextWellNumber => _NextWellNumber;

        /// <summary>Next road id suffix that will be assigned.</summary>
        public int NextRoadNumber => _NextRoadNumber;

        /// <summary>Next reference-point id suffix that will be assigned.</summary>
        public int NextReferenceNumber => _NextReferenceNumber;

        /// <summary>
        /// Removes every well, road, and reference point and restarts identifiers at 1.
        /// </summary>
        public void Clear
            (
            )
        {
            _Wells.Clear();
            _Roads.Clear();
            _ReferencePoints.Clear();
            _NextWellNumber = 1;
            _NextRoadNumber = 1;
            _NextReferenceNumber = 1;
        }

        /// <summary>
        /// Appends a new well and assigns the next well id.
        /// </summary>
        /// <param name="Latitude">Pad centre latitude in decimal degrees.</param>
        /// <param name="Longitude">Pad centre longitude in decimal degrees.</param>
        /// <param name="PadDiameterM">Pad diameter in metres.</param>
        /// <returns>The stored well.</returns>
        public WellFeature AddWell
            (
            double Latitude,
            double Longitude,
            double PadDiameterM
            )
        {
            WellFeature Well = new WellFeature($"well_{_NextWellNumber}", Latitude, Longitude, PadDiameterM);
            _NextWellNumber++;
            _Wells.Add(Well);
            return Well;
        }

        /// <summary>
        /// Appends a new road and assigns the next road id.
        /// </summary>
        /// <param name="Vertices">Centreline from start to end.</param>
        /// <param name="BedWidthM">Full road bed width in metres.</param>
        /// <param name="HeightM">Embankment height in metres.</param>
        /// <param name="SideSlope">Horizontal run per one vertical.</param>
        /// <returns>The stored road.</returns>
        public RoadFeature AddRoad
            (
            IReadOnlyList<FeatureVertex> Vertices,
            double BedWidthM,
            double HeightM,
            double SideSlope
            )
        {
            RoadFeature Road = new RoadFeature($"road_{_NextRoadNumber}", Vertices, BedWidthM, HeightM, SideSlope);
            _NextRoadNumber++;
            _Roads.Add(Road);
            return Road;
        }

        /// <summary>
        /// Appends a new reference point and assigns the next reference-point id.
        /// </summary>
        /// <param name="Latitude">Latitude in decimal degrees.</param>
        /// <param name="Longitude">Longitude in decimal degrees.</param>
        /// <param name="Name">Name drawn beside the marker.</param>
        /// <returns>The stored reference point.</returns>
        public ReferencePointFeature AddReferencePoint
            (
            double Latitude,
            double Longitude,
            string Name
            )
        {
            ReferencePointFeature Point = new ReferencePointFeature(
                $"reference_{_NextReferenceNumber}",
                Name,
                Latitude,
                Longitude);
            _NextReferenceNumber++;
            _ReferencePoints.Add(Point);
            return Point;
        }

        /// <summary>
        /// Finds a committed well by identifier.
        /// </summary>
        /// <param name="Id">Well identifier.</param>
        /// <returns>The well, or null when it is not in the set.</returns>
        public WellFeature? FindWell
            (
            string Id
            )
        {
            for (int Index = 0; Index < _Wells.Count; Index++)
            {
                if (string.Equals(_Wells[Index].Id, Id, StringComparison.Ordinal))
                    return _Wells[Index];
            }

            return null;
        }

        /// <summary>
        /// Finds a committed road by identifier.
        /// </summary>
        /// <param name="Id">Road identifier.</param>
        /// <returns>The road, or null when it is not in the set.</returns>
        public RoadFeature? FindRoad
            (
            string Id
            )
        {
            for (int Index = 0; Index < _Roads.Count; Index++)
            {
                if (string.Equals(_Roads[Index].Id, Id, StringComparison.Ordinal))
                    return _Roads[Index];
            }

            return null;
        }

        /// <summary>
        /// Finds a committed reference point by identifier.
        /// </summary>
        /// <param name="Id">Reference-point identifier.</param>
        /// <returns>The point, or null when it is not in the set.</returns>
        public ReferencePointFeature? FindReferencePoint
            (
            string Id
            )
        {
            for (int Index = 0; Index < _ReferencePoints.Count; Index++)
            {
                if (string.Equals(_ReferencePoints[Index].Id, Id, StringComparison.Ordinal))
                    return _ReferencePoints[Index];
            }

            return null;
        }

        /// <summary>
        /// Removes one committed well. Later well ids are left unchanged.
        /// </summary>
        /// <param name="Id">Well identifier.</param>
        /// <returns>True when a well with that id was removed.</returns>
        public bool RemoveWell
            (
            string Id
            )
        {
            for (int Index = 0; Index < _Wells.Count; Index++)
            {
                if (!string.Equals(_Wells[Index].Id, Id, StringComparison.Ordinal))
                    continue;

                _Wells.RemoveAt(Index);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes one committed road. Later road ids are left unchanged.
        /// </summary>
        /// <param name="Id">Road identifier.</param>
        /// <returns>True when a road with that id was removed.</returns>
        public bool RemoveRoad
            (
            string Id
            )
        {
            for (int Index = 0; Index < _Roads.Count; Index++)
            {
                if (!string.Equals(_Roads[Index].Id, Id, StringComparison.Ordinal))
                    continue;

                _Roads.RemoveAt(Index);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes one committed reference point. Later reference-point ids are left unchanged.
        /// </summary>
        /// <param name="Id">Reference-point identifier.</param>
        /// <returns>True when a point with that id was removed.</returns>
        public bool RemoveReferencePoint
            (
            string Id
            )
        {
            for (int Index = 0; Index < _ReferencePoints.Count; Index++)
            {
                if (!string.Equals(_ReferencePoints[Index].Id, Id, StringComparison.Ordinal))
                    continue;

                _ReferencePoints.RemoveAt(Index);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Replaces the set with features read from a project file.
        /// </summary>
        /// <param name="Wells">Wells in file order.</param>
        /// <param name="Roads">Roads in file order.</param>
        /// <param name="ReferencePoints">Reference points in file order.</param>
        /// <param name="NextWellNumber">Next well id suffix stored in the file.</param>
        /// <param name="NextRoadNumber">Next road id suffix stored in the file.</param>
        /// <param name="NextReferenceNumber">Next reference-point id suffix stored in the file.</param>
        public void Restore
            (
            IReadOnlyList<WellFeature> Wells,
            IReadOnlyList<RoadFeature> Roads,
            IReadOnlyList<ReferencePointFeature> ReferencePoints,
            int NextWellNumber,
            int NextRoadNumber,
            int NextReferenceNumber
            )
        {
            _Wells.Clear();
            _Roads.Clear();
            _ReferencePoints.Clear();
            _Wells.AddRange(Wells);
            _Roads.AddRange(Roads);
            _ReferencePoints.AddRange(ReferencePoints);
            _NextWellNumber = Math.Max(1, NextWellNumber);
            _NextRoadNumber = Math.Max(1, NextRoadNumber);
            _NextReferenceNumber = Math.Max(1, NextReferenceNumber);
            BumpNextNumberPastExistingIds();
        }

        /// <summary>
        /// Advances id counters past any restored identifier so new features do not reuse an id.
        /// </summary>
        private void BumpNextNumberPastExistingIds
            (
            )
        {
            for (int Index = 0; Index < _Wells.Count; Index++)
            {
                int Number = ParseIdNumber(_Wells[Index].Id, "well_");
                if (Number >= _NextWellNumber)
                    _NextWellNumber = Number + 1;
            }

            for (int Index = 0; Index < _Roads.Count; Index++)
            {
                int Number = ParseIdNumber(_Roads[Index].Id, "road_");
                if (Number >= _NextRoadNumber)
                    _NextRoadNumber = Number + 1;
            }

            for (int Index = 0; Index < _ReferencePoints.Count; Index++)
            {
                int Number = ParseIdNumber(_ReferencePoints[Index].Id, "reference_");
                if (Number >= _NextReferenceNumber)
                    _NextReferenceNumber = Number + 1;
            }
        }

        /// <summary>
        /// Reads the numeric suffix of a feature id.
        /// </summary>
        /// <param name="Id">Feature identifier.</param>
        /// <param name="Prefix">Expected id prefix including the underscore.</param>
        /// <returns>The suffix, or 0 when the id does not match.</returns>
        static private int ParseIdNumber
            (
            string Id,
            string Prefix
            )
        {
            if (!Id.StartsWith(Prefix, StringComparison.Ordinal))
                return 0;

            return int.TryParse(Id.Substring(Prefix.Length), out int Number) ? Number : 0;
        }
    }
}
