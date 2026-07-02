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
        /// <summary>Compass widget width and height in screen pixels.</summary>
        private const float COMPASS_SIZE = 52f;
        /// <summary>Radius of the compass dial (half of <see cref="COMPASS_SIZE"/>).</summary>
        private const float COMPASS_RADIUS = COMPASS_SIZE * 0.5f;
        /// <summary>Offset from widget corner to dial centre along one axis.</summary>
        private const float COMPASS_CENTRE_OFFSET = COMPASS_RADIUS;
        /// <summary>Number of cardinal tick marks drawn on the dial.</summary>
        private const float TICK_COUNT = 4f;
        /// <summary>Radians between adjacent cardinal ticks (π/2).</summary>
        private const float TICK_ANGLE_STEP_RADIANS = 1.5707963267948966f;
        /// <summary>Inner radius inset for tick mark inner endpoints.</summary>
        private const float TICK_INNER_RADIUS_INSET = 8f;
        /// <summary>Outer radius inset for tick mark outer endpoints.</summary>
        private const float TICK_OUTER_RADIUS_INSET = 2f;
        /// <summary>Inset from dial edge to north arrow tip.</summary>
        private const float NORTH_ARROW_TIP_INSET = 6f;
        /// <summary>Inset from dial edge to north arrow base.</summary>
        private const float NORTH_ARROW_BASE_INSET = 18f;
        /// <summary>Half-width of the north arrow triangle base.</summary>
        private const float NORTH_ARROW_HALF_WIDTH = 5f;
        /// <summary>Vertical inset for the "N" label below the arrow tip.</summary>
        private const float NORTH_LABEL_INSET = 30f;
        /// <summary>Stroke width for dial outline and tick marks.</summary>
        private const float DIAL_STROKE_WIDTH = 1.5f;
        /// <summary>Font size for the north label.</summary>
        private const float NORTH_LABEL_FONT_SIZE = 13f;
        /// <summary>Minimum projected north vector length before rotation is skipped.</summary>
        private const float MIN_NORTH_VECTOR_LENGTH = 1e-3f;
        /// <summary>Degrees in half a circle (radians-to-degrees helper).</summary>
        private const double DEGREES_PER_RADIAN = 180.0;

        /// <summary>Fill colour for the compass dial background.</summary>
        private static readonly SKColor DIAL_FILL = new SKColor(37, 40, 37, 255);
        /// <summary>Stroke colour for the compass dial outline.</summary>
        private static readonly SKColor DIAL_STROKE = new SKColor(120, 120, 120);
        /// <summary>Colour for the north arrow and label.</summary>
        private static readonly SKColor NORTH_MARK = SKColors.White;
        /// <summary>Colour for cardinal tick marks.</summary>
        private static readonly SKColor TICK_COLOR = new SKColor(180, 180, 180);

        /// <summary>
        /// Returns the compass bounds in viewport coordinates.
        /// </summary>
        static internal SKRect GetBounds
            (
            SKRect Viewport
            )
        {
            float Left = Viewport.Right - RenderingConstants.OVERLAY_WIDGET_MARGIN - COMPASS_SIZE;
            float Top = Viewport.Top + RenderingConstants.OVERLAY_WIDGET_MARGIN;
            return SKRect.Create(Left, Top, COMPASS_SIZE, COMPASS_SIZE);
        }

        /// <summary>
        /// Returns true when a screen point is inside the compass dial.
        /// </summary>
        static internal bool HitTest
            (
            SKRect Viewport,
            float ScreenX,
            float ScreenY
            )
        {
            SKRect Bounds = GetBounds(Viewport);
            float CentreX = Bounds.MidX;
            float CentreY = Bounds.MidY;
            float Radius = Bounds.Width * 0.5f;
            float Dx = ScreenX - CentreX;
            float Dy = ScreenY - CentreY;
            return ((Dx * Dx) + (Dy * Dy)) <= (Radius * Radius);
        }

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
            float CentreX = Viewport.Right - RenderingConstants.OVERLAY_WIDGET_MARGIN - COMPASS_CENTRE_OFFSET;
            float CentreY = Viewport.Top + RenderingConstants.OVERLAY_WIDGET_MARGIN + COMPASS_CENTRE_OFFSET;
            float Radius = COMPASS_RADIUS;

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
                StrokeWidth = DIAL_STROKE_WIDTH
            };
            Canvas.DrawCircle(0, 0, Radius, FillPaint);
            Canvas.DrawCircle(0, 0, Radius, StrokePaint);

            using SKPaint TickPaint = new SKPaint
            {
                Color = TICK_COLOR,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = DIAL_STROKE_WIDTH,
                StrokeCap = SKStrokeCap.Round
            };

            for (int Tick = 0; Tick < (int)TICK_COUNT; Tick++)
            {
                double AngleRad = Tick * TICK_ANGLE_STEP_RADIANS;
                float Inner = Radius - TICK_INNER_RADIUS_INSET;
                float Outer = Radius - TICK_OUTER_RADIUS_INSET;
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
            Arrow.MoveTo(0, -Radius + NORTH_ARROW_TIP_INSET);
            Arrow.LineTo(-NORTH_ARROW_HALF_WIDTH, -Radius + NORTH_ARROW_BASE_INSET);
            Arrow.LineTo(NORTH_ARROW_HALF_WIDTH, -Radius + NORTH_ARROW_BASE_INSET);
            Arrow.Close();
            Canvas.DrawPath(Arrow, ArrowPaint);

            using SKFont NorthFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), NORTH_LABEL_FONT_SIZE);
            using SKPaint TextPaint = new SKPaint
            {
                Color = NORTH_MARK,
                IsAntialias = true
            };
            Canvas.DrawText("N", 0, -Radius + NORTH_LABEL_INSET, SKTextAlign.Center, NorthFont, TextPaint);

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
                CentreUtm.Northing + RenderingConstants.GROUND_SAMPLE_SPAN_M,
                out double NorthLat,
                out double NorthLon);

            Camera.Project(NorthLat, NorthLon, Camera.MinElevation, Viewport, FlatPlane: true, out float NorthX, out float NorthY, out _);

            float NorthDx = NorthX - CentreX;
            float NorthDy = NorthY - CentreY;
            float NorthLen = (float)Math.Sqrt((NorthDx * NorthDx) + (NorthDy * NorthDy));
            if (NorthLen <= MIN_NORTH_VECTOR_LENGTH)
            {
                NorthAngleDeg = 0f;
                return false;
            }

            // Dial "up" is screen-up (-Y); rotate the rose so N tracks projected north.
            NorthAngleDeg = (float)(Math.Atan2(NorthDx, -NorthDy) * (DEGREES_PER_RADIAN / Math.PI));
            return true;
        }
    }
}
