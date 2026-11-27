using AgGradeDesigner.Core;

namespace AgGradeDesigner.Core.Features
{
    /// <summary>
    /// Snaps a proposed road segment to a fixed screen angle.
    /// The pointer keeps its distance from the last vertex and moves onto the nearest increment.
    /// </summary>
    static public class RoadSegmentSnap
    {
        /// <summary>Angle step of a snapped road segment, in degrees.</summary>
        public const double INCREMENT_DEGREES = 15.0;

        /// <summary>Pointer movement too short to define a segment direction, in pixels.</summary>
        private const double MIN_SEGMENT_PX = 1.0;

        /// <summary>
        /// Moves the pointer onto the nearest <see cref="INCREMENT_DEGREES"/> ray from the last vertex.
        /// Screen Y grows downward. A multiple of 15° is the same set of directions measured from east or from north.
        /// </summary>
        /// <param name="OriginX">Last vertex, horizontal control pixels.</param>
        /// <param name="OriginY">Last vertex, vertical control pixels.</param>
        /// <param name="PointerX">Pointer horizontal control pixels.</param>
        /// <param name="PointerY">Pointer vertical control pixels.</param>
        /// <param name="SnappedX">Snapped horizontal control pixels.</param>
        /// <param name="SnappedY">Snapped vertical control pixels.</param>
        /// <returns>False when the pointer is too close to the last vertex to choose a direction.</returns>
        static public bool TrySnapScreenPoint
            (
            double OriginX,
            double OriginY,
            double PointerX,
            double PointerY,
            out double SnappedX,
            out double SnappedY
            )
        {
            SnappedX = PointerX;
            SnappedY = PointerY;
            double Dx = PointerX - OriginX;
            double Dy = PointerY - OriginY;
            double Length = Math.Sqrt((Dx * Dx) + (Dy * Dy));
            if (Length < MIN_SEGMENT_PX)
                return false;

            double StepRadians = INCREMENT_DEGREES * Math.PI / FieldConstants.DEGREES_PER_RADIAN;
            double Angle = Math.Atan2(Dy, Dx);
            // intent: a halfway angle takes the next step. Banker's rounding would leave 7.5° on the previous one.
            double Steps = Math.Round(Angle / StepRadians, MidpointRounding.AwayFromZero);
            double SnappedAngle = Steps * StepRadians;
            SnappedX = OriginX + (Length * Math.Cos(SnappedAngle));
            SnappedY = OriginY + (Length * Math.Sin(SnappedAngle));
            return true;
        }
    }
}
