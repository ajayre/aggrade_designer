using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Tests.TestFixtures;
using AgGradeDesigner.Rendering;

using SkiaSharp;

namespace AgGradeDesigner.Core.Tests.Performance
{
    /// <summary>
    /// Guards 3D rendering at true-scale vertical exaggeration (slider at 0×).
    /// </summary>
    public sealed class VerticalExaggeration3DTests
    {
        /// <summary>
        /// The Shop 2 existing topo must render at true scale without throwing.
        /// </summary>
        [Fact]
        public void TheShop2_Existing_Draw3D_TrueScale_DoesNotThrow()
        {
            string Shop2Path = Path.Combine(TestDataPaths.TestDataDirectory, "TheShop2_2ft.agd");
            if (!File.Exists(Shop2Path))
                return;

            (FieldSurfaceSampler Surface, ProjectDocument Project) = FieldRenderer3DBenchmark.BuildShop2Scene();
            DisplaySettings Display = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: false);
            Display.VerticalExaggeration = 0.0;

            FieldRenderer3DBenchmark.Render3DFrame(Surface, Project, Display);
        }

        /// <summary>
        /// True scale with satellite and hillshade enabled must also complete.
        /// </summary>
        [Fact]
        public void TheShop2_Existing_Draw3D_TrueScale_WithSatelliteAndHillshade_DoesNotThrow()
        {
            string Shop2Path = Path.Combine(TestDataPaths.TestDataDirectory, "TheShop2_2ft.agd");
            if (!File.Exists(Shop2Path))
                return;

            (FieldSurfaceSampler Surface, ProjectDocument Project) = FieldRenderer3DBenchmark.BuildShop2Scene();
            DisplaySettings Display = new DisplaySettings
            {
                Satellite = true,
                Contours = true,
                Hillshade = true,
                VerticalExaggeration = 0.0
            };

            SatelliteTileService Satellite = new SatelliteTileService();
            double OrbitLat = (Surface.MinLat + Surface.MaxLat) * 0.5;
            double OrbitLon = (Surface.MinLon + Surface.MaxLon) * 0.5;
            (int Zoom, IReadOnlyList<(int TileX, int TileY)> Tiles) = SatelliteMapLayout3D.ComputePlinthTiles(
                OrbitLat,
                OrbitLon,
                1609.344 * 2.0 * Math.Sqrt(2.0),
                MetresPerPixel: 3.0);
            foreach ((int TileX, int TileY) in Tiles)
                FieldRenderer3DBenchmark.SeedSolidSatelliteTile(Satellite, Zoom, TileX, TileY, new SKColor(90, 110, 70));

            FieldRenderer3DBenchmark.Render3DFrame(Surface, Project, Display, Satellite: Satellite);
        }
    }
}
