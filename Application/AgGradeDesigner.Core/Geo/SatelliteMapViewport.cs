using System;

namespace AgGradeDesigner.Core.Geo
{
    /// <summary>
    /// Visible Web Mercator tile set and world-space origin for a north-up map viewport.
    /// </summary>
    public sealed class SatelliteMapViewport
    {
        /// <summary>Web Mercator zoom level used for tile indexing.</summary>
        public int Zoom { get; init; }

        /// <summary>Scale factor from world pixels to viewport pixels.</summary>
        public double Scale { get; init; }

        /// <summary>World pixel X coordinate of the viewport left edge.</summary>
        public double LeftWorld { get; init; }

        /// <summary>World pixel Y coordinate of the viewport top edge.</summary>
        public double TopWorld { get; init; }

        /// <summary>Tiles intersecting the viewport with screen-space placement.</summary>
        public IReadOnlyList<SatelliteTileRequest> Tiles { get; init; } = Array.Empty<SatelliteTileRequest>();
    }
}
