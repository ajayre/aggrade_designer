using AgGradeDesigner.Core.Geo;

using SkiaSharp;

namespace AgGradeDesigner.Core.Tests.Geo
{
    /// <summary>
    /// Verifies decoded satellite tile bitmaps are cached for repeated 3D frame renders.
    /// </summary>
    [Trait("Category", "RenderingRegression")]
    public sealed class SatelliteTileServiceBitmapCacheTests
    {
        /// <summary>
        /// Tile zoom used by this component.
        /// </summary>

        private const int TILE_ZOOM = 12;
        /// <summary>
        /// Tile x used by this component.
        /// </summary>

        private const int TILE_X = 100;
        /// <summary>
        /// Tile y used by this component.
        /// </summary>

        private const int TILE_Y = 200;
        /// <summary>
        /// Tile size px used by this component.
        /// </summary>

        private const int TILE_SIZE_PX = 256;
        /// <summary>
        /// Tile color r used by this component.
        /// </summary>

        private const byte TILE_COLOR_R = 90;
        /// <summary>
        /// Tile color g used by this component.
        /// </summary>

        private const byte TILE_COLOR_G = 110;
        /// <summary>
        /// Tile color b used by this component.
        /// </summary>

        private const byte TILE_COLOR_B = 70;
        /// <summary>
        /// PNG encode quality passed to Skia when exporting composites.
        /// </summary>

        private const int PNG_ENCODE_QUALITY = 100;

        /// <summary>
        /// Repeated GetTileBitmap calls for the same tile should return the same instance.
        /// </summary>
        [Fact]
        public void GetTileBitmap_SecondCall_ReturnsSameInstance
            (
            )
        {
            SatelliteTileService Service = new SatelliteTileService();
            using SKBitmap Tile = new SKBitmap(TILE_SIZE_PX, TILE_SIZE_PX);
            Tile.Erase(new SKColor(TILE_COLOR_R, TILE_COLOR_G, TILE_COLOR_B));
            using SKImage Image = SKImage.FromBitmap(Tile);
            using SKData Data = Image.Encode(SKEncodedImageFormat.Png, PNG_ENCODE_QUALITY);
            Service.SeedTile(TILE_ZOOM, TILE_X, TILE_Y, Data.ToArray());

            SKBitmap? First = Service.GetTileBitmap(TILE_ZOOM, TILE_X, TILE_Y);
            SKBitmap? Second = Service.GetTileBitmap(TILE_ZOOM, TILE_X, TILE_Y);

            Assert.NotNull(First);
            Assert.Same(First, Second);
        }
    }
}
