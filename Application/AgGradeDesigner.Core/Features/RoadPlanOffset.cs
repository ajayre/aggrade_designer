using AgGradeDesigner.Core;

namespace AgGradeDesigner.Core.Features
{
    /// <summary>
    /// Builds a polyline parallel to a road centreline at a constant ground distance.
    /// </summary>
    static public class RoadPlanOffset
    {
        /// <summary>Segments shorter than this have no direction, in metres.</summary>
        private const double MIN_SEGMENT_M = 0.001;

        /// <summary>Floor for metres per degree of longitude so a polar cosine cannot divide the offset away.</summary>
        private const double MIN_METRES_PER_DEGREE_LON = 1.0;

        /// <summary>Longest miter join, as a multiple of the offset, before a sharp corner is clipped.</summary>
        private const double MITER_LIMIT = 4.0;

        /// <summary>Below this dot product the two side normals nearly oppose, so the miter would shoot outward.</summary>
        private const double MIN_MITER_DENOM = 0.25;

        /// <summary>
        /// Offsets a centreline. Positive is to the left of the direction of travel; negative is to the right.
        /// </summary>
        /// <param name="Vertices">Centreline from start to end.</param>
        /// <param name="OffsetM">Ground distance from the centreline in metres.</param>
        /// <returns>Parallel vertices. Empty when the centreline has no segment.</returns>
        static public List<FeatureVertex> Offset
            (
            IReadOnlyList<FeatureVertex> Vertices,
            double OffsetM
            )
        {
            List<FeatureVertex> Result = new List<FeatureVertex>();
            int Count = Vertices.Count;
            if ((Count < FieldFeatureSet.MIN_ROAD_VERTICES) || double.IsNaN(OffsetM) || double.IsInfinity(OffsetM))
                return Result;

            double OriginLat = Vertices[0].Latitude;
            double OriginLon = Vertices[0].Longitude;
            double MetresPerLat = FieldConstants.METRES_PER_DEGREE_LAT;
            double MetresPerLon = MetresPerDegreeLon(OriginLat);

            double[] East = new double[Count];
            double[] North = new double[Count];
            for (int Index = 0; Index < Count; Index++)
            {
                East[Index] = (Vertices[Index].Longitude - OriginLon) * MetresPerLon;
                North[Index] = (Vertices[Index].Latitude - OriginLat) * MetresPerLat;
            }

            int SegmentCount = Count - 1;
            double[] DirEast = new double[SegmentCount];
            double[] DirNorth = new double[SegmentCount];
            bool[] HasDir = new bool[SegmentCount];
            bool AnySegment = false;
            for (int Segment = 0; Segment < SegmentCount; Segment++)
            {
                HasDir[Segment] = TryUnit(
                    East[Segment + 1] - East[Segment],
                    North[Segment + 1] - North[Segment],
                    out DirEast[Segment],
                    out DirNorth[Segment]);
                if (HasDir[Segment])
                    AnySegment = true;
            }

            if (!AnySegment)
                return Result;

            for (int Index = 0; Index < Count; Index++)
            {
                // A vertex with no neighbouring segment keeps the previous join instead of dropping out of the line.
                if (!TryOffsetPoint(
                    Index,
                    East,
                    North,
                    HasDir,
                    DirEast,
                    DirNorth,
                    OffsetM,
                    out double OffsetEast,
                    out double OffsetNorth))
                {
                    OffsetEast = East[Index];
                    OffsetNorth = North[Index];
                }

                Result.Add(new FeatureVertex(
                    OriginLat + (OffsetNorth / MetresPerLat),
                    OriginLon + (OffsetEast / MetresPerLon)));
            }

            return Result;
        }

        /// <summary>
        /// Metres per degree of longitude at a latitude. The equator scale is shortened by the cosine.
        /// </summary>
        /// <param name="Latitude">Latitude in decimal degrees.</param>
        /// <returns>Metres per degree of longitude.</returns>
        static private double MetresPerDegreeLon
            (
            double Latitude
            )
        {
            double CosLat = Math.Cos(Latitude * Math.PI / FieldConstants.DEGREES_PER_RADIAN);
            double MetresPerLon = FieldConstants.METRES_PER_DEGREE_LAT * CosLat;
            if (Math.Abs(MetresPerLon) < MIN_METRES_PER_DEGREE_LON)
                return MetresPerLon < 0.0 ? -MIN_METRES_PER_DEGREE_LON : MIN_METRES_PER_DEGREE_LON;

            return MetresPerLon;
        }

        /// <summary>
        /// Places one vertex on the offset polyline, mitering the join so the sides stay parallel.
        /// </summary>
        /// <param name="Index">Vertex index.</param>
        /// <param name="East">Centreline easting in metres.</param>
        /// <param name="North">Centreline northing in metres.</param>
        /// <param name="HasDir">True when the matching segment has a direction.</param>
        /// <param name="DirEast">Segment east component.</param>
        /// <param name="DirNorth">Segment north component.</param>
        /// <param name="OffsetM">Signed ground offset in metres.</param>
        /// <param name="OffsetEast">Offset easting in metres.</param>
        /// <param name="OffsetNorth">Offset northing in metres.</param>
        /// <returns>True when a side normal was available.</returns>
        static private bool TryOffsetPoint
            (
            int Index,
            double[] East,
            double[] North,
            bool[] HasDir,
            double[] DirEast,
            double[] DirNorth,
            double OffsetM,
            out double OffsetEast,
            out double OffsetNorth
            )
        {
            OffsetEast = East[Index];
            OffsetNorth = North[Index];
            bool HasIn = TryDirectionBefore(Index, HasDir, DirEast, DirNorth, out double InEast, out double InNorth);
            bool HasOut = TryDirectionAfter(Index, HasDir, DirEast, DirNorth, out double OutEast, out double OutNorth);
            if (!HasIn && !HasOut)
                return false;

            // Left of travel is (-north, east). A negative offset then falls on the right.
            if (!HasIn)
            {
                LeftNormal(OutEast, OutNorth, out double OutNormEast, out double OutNormNorth);
                OffsetEast += OutNormEast * OffsetM;
                OffsetNorth += OutNormNorth * OffsetM;
                return true;
            }

            LeftNormal(InEast, InNorth, out double InNormEast, out double InNormNorth);
            if (!HasOut)
            {
                OffsetEast += InNormEast * OffsetM;
                OffsetNorth += InNormNorth * OffsetM;
                return true;
            }

            LeftNormal(OutEast, OutNorth, out double NextNormEast, out double NextNormNorth);
            double MiterEast = InNormEast + NextNormEast;
            double MiterNorth = InNormNorth + NextNormNorth;
            if (!TryUnit(MiterEast, MiterNorth, out MiterEast, out MiterNorth))
            {
                // The two normals cancel on a reversal, so stay on the incoming side.
                OffsetEast += InNormEast * OffsetM;
                OffsetNorth += InNormNorth * OffsetM;
                return true;
            }

            double Denom = (MiterEast * InNormEast) + (MiterNorth * InNormNorth);
            if (Math.Abs(Denom) < MIN_MITER_DENOM)
            {
                OffsetEast += InNormEast * OffsetM;
                OffsetNorth += InNormNorth * OffsetM;
                return true;
            }

            double Scale = OffsetM / Denom;
            double MaxScale = Math.Abs(OffsetM) * MITER_LIMIT;
            if (Math.Abs(Scale) > MaxScale)
                Scale = Math.CopySign(MaxScale, Scale);

            OffsetEast += MiterEast * Scale;
            OffsetNorth += MiterNorth * Scale;
            return true;
        }

        /// <summary>
        /// Finds the last real segment direction that arrives at a vertex.
        /// </summary>
        /// <param name="Index">Vertex index.</param>
        /// <param name="HasDir">True when the matching segment has a direction.</param>
        /// <param name="DirEast">Segment east component.</param>
        /// <param name="DirNorth">Segment north component.</param>
        /// <param name="East">Unit east component.</param>
        /// <param name="North">Unit north component.</param>
        /// <returns>True when a segment before the vertex has a direction.</returns>
        static private bool TryDirectionBefore
            (
            int Index,
            bool[] HasDir,
            double[] DirEast,
            double[] DirNorth,
            out double East,
            out double North
            )
        {
            for (int Segment = Index - 1; Segment >= 0; Segment--)
            {
                if (!HasDir[Segment])
                    continue;

                East = DirEast[Segment];
                North = DirNorth[Segment];
                return true;
            }

            East = 0.0;
            North = 0.0;
            return false;
        }

        /// <summary>
        /// Finds the first real segment direction that leaves a vertex.
        /// </summary>
        /// <param name="Index">Vertex index.</param>
        /// <param name="HasDir">True when the matching segment has a direction.</param>
        /// <param name="DirEast">Segment east component.</param>
        /// <param name="DirNorth">Segment north component.</param>
        /// <param name="East">Unit east component.</param>
        /// <param name="North">Unit north component.</param>
        /// <returns>True when a segment at or after the vertex has a direction.</returns>
        static private bool TryDirectionAfter
            (
            int Index,
            bool[] HasDir,
            double[] DirEast,
            double[] DirNorth,
            out double East,
            out double North
            )
        {
            for (int Segment = Index; Segment < HasDir.Length; Segment++)
            {
                if (!HasDir[Segment])
                    continue;

                East = DirEast[Segment];
                North = DirNorth[Segment];
                return true;
            }

            East = 0.0;
            North = 0.0;
            return false;
        }

        /// <summary>
        /// Left-hand normal of a travel direction in the east/north plane.
        /// </summary>
        /// <param name="DirEast">Unit east component of travel.</param>
        /// <param name="DirNorth">Unit north component of travel.</param>
        /// <param name="NormEast">Unit east component of the left normal.</param>
        /// <param name="NormNorth">Unit north component of the left normal.</param>
        static private void LeftNormal
            (
            double DirEast,
            double DirNorth,
            out double NormEast,
            out double NormNorth
            )
        {
            NormEast = -DirNorth;
            NormNorth = DirEast;
        }

        /// <summary>
        /// Normalises a plan vector. Very short vectors are treated as having no direction.
        /// </summary>
        /// <param name="East">Easting component in metres.</param>
        /// <param name="North">Northing component in metres.</param>
        /// <param name="UnitEast">Unit east component.</param>
        /// <param name="UnitNorth">Unit north component.</param>
        /// <returns>True when the vector is long enough to define a direction.</returns>
        static private bool TryUnit
            (
            double East,
            double North,
            out double UnitEast,
            out double UnitNorth
            )
        {
            double Length = Math.Sqrt((East * East) + (North * North));
            if (Length < MIN_SEGMENT_M)
            {
                UnitEast = 0.0;
                UnitNorth = 0.0;
                return false;
            }

            UnitEast = East / Length;
            UnitNorth = North / Length;
            return true;
        }
    }
}
