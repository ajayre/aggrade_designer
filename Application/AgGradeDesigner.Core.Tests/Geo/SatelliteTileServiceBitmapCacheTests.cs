using AgGradeDesigner.Core.Geo;

using SkiaSharp;

namespace AgGradeDesigner.Core.Tests.Geo
{
    /// <summary>
    /// Verifies decoded satellite tile bitmaps are cached for repeated 3D frame renders.
    /// </summary>
    public sealed class SatelliteTileServiceBitmapCacheTests
    {
        /// <summary>
        /// Repeated GetTileBitmap calls for the same tile should return the same instance.
        /// </summary>
        [Fact]
        public void GetTileBitmap_SecondCall_ReturnsSameInstance
            (
            )
        {
            SatelliteTileService Service = new SatelliteTileService();
            using SKBitmap Tile = new SKBitmap(256, 256);
            Tile.Erase(new SKColor(90, 110, 70));
            using SKImage Image = SKImage.FromBitmap(Tile);
            using SKData Data = Image.Encode(SKEncodedImageFormat.Png, 100);
            Service.SeedTile(12, 100, 200, Data.ToArray());

            SKBitmap? First = Service.GetTileBitmap(12, 100, 200);
            SKBitmap? Second = Service.GetTileBitmap(12, 100, 200);

            Assert.NotNull(First);
            Assert.Same(First, Second);
        }
    }
}
