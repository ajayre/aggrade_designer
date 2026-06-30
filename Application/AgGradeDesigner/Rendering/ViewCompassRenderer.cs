using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;

using SkiaSharp;

namespace AgGradeDesigner.Rendering
{
    /// <summary>
    /// Draws the map compass overlay in the top-right corner of the field viewer.
    /// </summary>
    static internal class ViewCompassRenderer
    {
        private const float COMPASS_SIZE = 52f;
        private const float COMPASS_MARGIN = 14f;
        private const float NORTH_SAMPLE_M = 100.0f;
        private static readonly SKColor DIAL_FILL = new SKColor(37, 40, 37, 255);
        private static readonly SKColor DIAL_STROKE = new SKColor(120, 120, 120);
        private static readonly SKColor NORTH_MARK = SKColors.White;
        private static readonly SKColor TICK_COLOR = new SKColor(180, 180, 180);

        /// <summary>
        /// Draws the compass dial for the active view mode.
        /// </summary>
        /// <param name="Canvas">Target canvas.</param>
        /// <param name="Viewport">Viewer viewport rectangle.</param>
        /// <param name="Mode">Active 2D or 3D view mode.</param>
        /// <param name="Camera">Orbit camera used to align north in 3D; null in 2D.</param>
        static internal void Draw
            (
            SKCanvas Canvas,
            SKRect Viewport,
            ViewMode Mode,
            FieldCamera3D? Camera = null
            )
        {
            float CentreX = Viewport.Right - COMPASS_MARGIN - (COMPASS_SIZE * 0.5f);
            float CentreY = Viewport.Top + COMPASS_MARGIN + (COMPASS_SIZE * 0.5f);
            float Radius = COMPASS_SIZE * 0.5f;

            Canvas.Save();
            Canvas.Translate(CentreX, CentreY);

            if ((Mode == ViewMode.ThreeD) && (Camera != null))
            {
                FieldCamera3D ActiveCamera = Camera.Value;
                if (TryComputeNorthScreenAngleDeg(ActiveCamera, Viewport, out float NorthAngleDeg))
                    Canvas.RotateDegrees(NorthAngleDeg);
            }

            using SKPaint FillPaint = new SKPaint
            {
                Color = DIAL_FILL,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };
            using SKPaint StrokePaint = new SKPaint
            {
                Color = DIAL_STROKE,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f
            };
            Canvas.DrawCircle(0, 0, Radius, FillPaint);
            Canvas.DrawCircle(0, 0, Radius, StrokePaint);

            using SKPaint TickPaint = new SKPaint
            {
                Color = TICK_COLOR,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f,
                StrokeCap = SKStrokeCap.Round
            };

            for (int Tick = 0; Tick < 4; Tick++)
            {
                double AngleRad = Tick * Math.PI * 0.5;
                float Inner = Radius - 8f;
                float Outer = Radius - 2f;
                Canvas.DrawLine(
                    (float)(Math.Sin(AngleRad) * Inner),
                    (float)(-Math.Cos(AngleRad) * Inner),
                    (float)(Math.Sin(AngleRad) * Outer),
                    (float)(-Math.Cos(AngleRad) * Outer),
                    TickPaint);
            }

            using SKPaint ArrowPaint = new SKPaint
            {
                Color = NORTH_MARK,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };
            using SKPath Arrow = new SKPath();
            Arrow.MoveTo(0, -Radius + 6f);
            Arrow.LineTo(-5f, -Radius + 18f);
            Arrow.LineTo(5f, -Radius + 18f);
            Arrow.Close();
            Canvas.DrawPath(Arrow, ArrowPaint);

            using SKFont NorthFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 13f);
            using SKPaint TextPaint = new SKPaint
            {
                Color = NORTH_MARK,
                IsAntialias = true
            };
            Canvas.DrawText("N", 0, -Radius + 30f, SKTextAlign.Center, NorthFont, TextPaint);

            Canvas.Restore();
        }

        /// <summary>
        /// Computes the screen angle (degrees) from dial-up to geographic north.
        /// </summary>
        static private bool TryComputeNorthScreenAngleDeg
            (
            FieldCamera3D Camera,
            SKRect Viewport,
            out float NorthAngleDeg
            )
        {
            Camera.Project(
                Camera.CentreLat,
                Camera.CentreLon,
                Camera.MinElevation,
                Viewport,
                FlatPlane: true,
                out float CentreX,
                out float CentreY,
                out _);

            UTM.UTMCoordinate CentreUtm = Camera.CentreUtm;
            UTM.ToLatLon(
                CentreUtm.Zone,
                CentreUtm.IsNorthernHemisphere,
                CentreUtm.Easting,
                CentreUtm.Northing + NORTH_SAMPLE_M,
                out double NorthLat,
                out double NorthLon);

            Camera.Project(NorthLat, NorthLon, Camera.MinElevation, Viewport, FlatPlane: true, out float NorthX, out float NorthY, out _);

            float NorthDx = NorthX - CentreX;
            float NorthDy = NorthY - CentreY;
            float NorthLen = (float)Math.Sqrt((NorthDx * NorthDx) + (NorthDy * NorthDy));
            if (NorthLen <= 1e-3f)
            {
                NorthAngleDeg = 0f;
                return false;
            }

            // Dial "up" is screen-up (-Y); rotate the rose so N tracks projected north.
            NorthAngleDeg = (float)(Math.Atan2(NorthDx, -NorthDy) * 180.0 / Math.PI);
            return true;
        }
    }
}
