using AgGradeDesigner.Core.Geo;

namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Builds and validates haul flow-arrow triangles in haul-grid UTM coordinates.
    /// </summary>
    public static class HaulFlowArrowGeometry
    {
        /// <summary>Arrow tip length as a fraction of one grid cell edge (Render.cs parity).</summary>
        public const double ARROW_LENGTH_CELL_FRACTION = 0.45;

        /// <summary>Arrow base half-width as a fraction of one grid cell edge.</summary>
        public const double ARROW_HALF_BASE_CELL_FRACTION = 0.16;

        /// <summary>Distance from cell centre to arrow base along the flow axis.</summary>
        public const double ARROW_BASE_OFFSET_FRACTION = 0.35;

        /// <summary>Hollow triangle vertices for one flow arrow.</summary>
        public readonly struct ArrowTriangleUtm
        {
            /// <summary>Tip easting in metres.</summary>
            public double TipE { get; init; }

            /// <summary>Tip northing in metres.</summary>
            public double TipN { get; init; }

            /// <summary>Left base easting in metres.</summary>
            public double LeftE { get; init; }

            /// <summary>Left base northing in metres.</summary>
            public double LeftN { get; init; }

            /// <summary>Right base easting in metres.</summary>
            public double RightE { get; init; }

            /// <summary>Right base northing in metres.</summary>
            public double RightN { get; init; }
        }

        /// <summary>
        /// Returns whether all triangle vertices lie inside valid haul grid cells.
        /// </summary>
        /// <param name="Grid">Haul processing grid.</param>
        /// <param name="Triangle">Arrow triangle in UTM metres.</param>
        /// <returns>True when tip and both base corners are on valid cells.</returns>
        public static bool AreAllVerticesInValidCells
            (
            HaulBuilder.Grid Grid,
            ArrowTriangleUtm Triangle
            )
        {
            return IsValidUtmPoint(Grid, Triangle.TipE, Triangle.TipN) &&
                IsValidUtmPoint(Grid, Triangle.LeftE, Triangle.LeftN) &&
                IsValidUtmPoint(Grid, Triangle.RightE, Triangle.RightN);
        }

        /// <summary>
        /// Builds one flow-arrow triangle for a grid cell when every vertex stays on valid cells.
        /// </summary>
        /// <param name="Grid">Haul processing grid.</param>
        /// <param name="Flow">Transport flow field.</param>
        /// <param name="Bx">Cell column index.</param>
        /// <param name="By">Cell row index.</param>
        /// <param name="Triangle">Built triangle vertices when successful.</param>
        /// <returns>True when the arrow is drawable without crossing invalid cells.</returns>
        public static bool TryBuildDrawableTriangle
            (
            HaulBuilder.Grid Grid,
            HaulBuilder.FlowField Flow,
            int Bx,
            int By,
            out ArrowTriangleUtm Triangle
            )
        {
            Triangle = default;
            if (!Grid.Valid[By, Bx] || !Flow.HasArrow[By, Bx])
                return false;

            (double CenterE, double CenterN) = Grid.CellCenterXy(Bx, By);
            double HeadingDeg = Flow.DirectionDeg(Bx, By);
            double HeadingRad = HeadingDeg * Math.PI / 180.0;
            double AxisEast = Math.Sin(HeadingRad);
            double AxisNorth = Math.Cos(HeadingRad);
            double PerpEast = -AxisNorth;
            double PerpNorth = AxisEast;
            double LengthM = Grid.GridSizeM * ARROW_LENGTH_CELL_FRACTION;
            double HalfBaseM = Grid.GridSizeM * ARROW_HALF_BASE_CELL_FRACTION;
            double BaseOffsetM = LengthM * ARROW_BASE_OFFSET_FRACTION;

            Triangle = new ArrowTriangleUtm
            {
                TipE = CenterE + (LengthM * AxisEast),
                TipN = CenterN + (LengthM * AxisNorth),
                LeftE = CenterE - (BaseOffsetM * AxisEast) + (HalfBaseM * PerpEast),
                LeftN = CenterN - (BaseOffsetM * AxisNorth) + (HalfBaseM * PerpNorth),
                RightE = CenterE - (BaseOffsetM * AxisEast) - (HalfBaseM * PerpEast),
                RightN = CenterN - (BaseOffsetM * AxisNorth) - (HalfBaseM * PerpNorth)
            };

            return AreAllVerticesInValidCells(Grid, Triangle);
        }

        /// <summary>
        /// Converts one haul-grid cell centre to WGS-84 coordinates.
        /// </summary>
        /// <param name="Grid">Haul processing grid.</param>
        /// <param name="Bx">Cell column index.</param>
        /// <param name="By">Cell row index.</param>
        /// <param name="Lat">Output latitude in decimal degrees.</param>
        /// <param name="Lon">Output longitude in decimal degrees.</param>
        /// <returns>True when conversion succeeds.</returns>
        public static bool TryGetCellCenterLatLon
            (
            HaulBuilder.Grid Grid,
            int Bx,
            int By,
            out double Lat,
            out double Lon
            )
        {
            (double CenterE, double CenterN) = Grid.CellCenterXy(Bx, By);
            return TryUtmToLatLon(Grid, CenterE, CenterN, out Lat, out Lon);
        }

        /// <summary>
        /// Converts one haul-grid UTM coordinate to WGS-84 using the grid zone.
        /// </summary>
        /// <param name="Grid">Haul processing grid.</param>
        /// <param name="Easting">Point easting in metres.</param>
        /// <param name="Northing">Point northing in metres.</param>
        /// <param name="Lat">Output latitude in decimal degrees.</param>
        /// <param name="Lon">Output longitude in decimal degrees.</param>
        /// <returns>True when conversion succeeds.</returns>
        public static bool TryUtmToLatLon
            (
            HaulBuilder.Grid Grid,
            double Easting,
            double Northing,
            out double Lat,
            out double Lon
            )
        {
            Lat = 0.0;
            Lon = 0.0;
            if (!IsInsideGridExtent(Grid, Easting, Northing))
                return false;

            UTM.ToLatLon(Grid.Zone, Grid.IsNorthern, Easting, Northing, out Lat, out Lon);
            return true;
        }

        /// <summary>
        /// Returns whether a UTM point lies inside a valid haul grid cell.
        /// </summary>
        /// <param name="Grid">Haul processing grid.</param>
        /// <param name="Easting">Point easting in metres.</param>
        /// <param name="Northing">Point northing in metres.</param>
        /// <returns>True when the containing cell exists and is valid.</returns>
        public static bool IsValidUtmPoint
            (
            HaulBuilder.Grid Grid,
            double Easting,
            double Northing
            )
        {
            if (!TryGetCellIndex(Grid, Easting, Northing, out int Bx, out int By))
                return false;

            return Grid.Valid[By, Bx];
        }

        /// <summary>
        /// Maps a UTM point to the haul grid cell that contains it.
        /// </summary>
        /// <param name="Grid">Haul processing grid.</param>
        /// <param name="Easting">Point easting in metres.</param>
        /// <param name="Northing">Point northing in metres.</param>
        /// <param name="Bx">Output column index.</param>
        /// <param name="By">Output row index.</param>
        /// <returns>True when the point lies inside the grid extent.</returns>
        public static bool TryGetCellIndex
            (
            HaulBuilder.Grid Grid,
            double Easting,
            double Northing,
            out int Bx,
            out int By
            )
        {
            Bx = (int)Math.Floor((Easting - Grid.MinX) / Grid.GridSizeM);
            By = (int)Math.Floor((Northing - Grid.MinY) / Grid.GridSizeM);

            return Bx >= 0 &&
                By >= 0 &&
                Bx < Grid.Width &&
                By < Grid.Height;
        }

        /// <summary>
        /// Returns whether a UTM point lies inside the haul grid bounding box.
        /// </summary>
        /// <param name="Grid">Haul processing grid.</param>
        /// <param name="Easting">Point easting in metres.</param>
        /// <param name="Northing">Point northing in metres.</param>
        /// <returns>True when the point is inside the grid extent.</returns>
        public static bool IsInsideGridExtent
            (
            HaulBuilder.Grid Grid,
            double Easting,
            double Northing
            )
        {
            double MaxE = Grid.MinX + (Grid.Width * Grid.GridSizeM);
            double MaxN = Grid.MinY + (Grid.Height * Grid.GridSizeM);
            return Easting >= Grid.MinX &&
                Easting <= MaxE &&
                Northing >= Grid.MinY &&
                Northing <= MaxN;
        }
    }
}
