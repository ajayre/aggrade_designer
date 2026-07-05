namespace AgGradeDesigner.Core.MapTools
{
    /// <summary>
    /// Axis-aligned screen-space rectangle used for erase hit testing and preview (TOOLS-SPEC §3.3).
    /// </summary>
    public readonly struct ToolScreenRect
    {
        /// <summary>Left edge in viewport pixels.</summary>
        public float Left { get; init; }

        /// <summary>Top edge in viewport pixels.</summary>
        public float Top { get; init; }

        /// <summary>Right edge in viewport pixels.</summary>
        public float Right { get; init; }

        /// <summary>Bottom edge in viewport pixels.</summary>
        public float Bottom { get; init; }

        /// <summary>
        /// Builds a normalized rectangle from two drag corners.
        /// </summary>
        /// <param name="StartX">Pointer-down X in viewport pixels.</param>
        /// <param name="StartY">Pointer-down Y in viewport pixels.</param>
        /// <param name="EndX">Current pointer X in viewport pixels.</param>
        /// <param name="EndY">Current pointer Y in viewport pixels.</param>
        /// <returns>Normalized rectangle with ordered edges.</returns>
        static public ToolScreenRect FromDrag
            (
            float StartX,
            float StartY,
            float EndX,
            float EndY
            )
        {
            float Left = Math.Min(StartX, EndX);
            float Top = Math.Min(StartY, EndY);
            float Right = Math.Max(StartX, EndX);
            float Bottom = Math.Max(StartY, EndY);

            // Pure vertical or horizontal drags otherwise produce zero-width/height rects and miss features.
            if ((Right - Left) < ToolDataConstants.ERASE_RECT_MIN_DIMENSION_PX)
                Right = Left + ToolDataConstants.ERASE_RECT_MIN_DIMENSION_PX;
            if ((Bottom - Top) < ToolDataConstants.ERASE_RECT_MIN_DIMENSION_PX)
                Bottom = Top + ToolDataConstants.ERASE_RECT_MIN_DIMENSION_PX;

            return new ToolScreenRect
            {
                Left = Left,
                Top = Top,
                Right = Right,
                Bottom = Bottom,
            };
        }

        /// <summary>
        /// Resolves erase selection mode from vertical drag direction in screen space (TOOLS-SPEC addendum §3.3.1).
        /// </summary>
        /// <param name="StartX">Pointer-down X in viewport pixels.</param>
        /// <param name="StartY">Pointer-down Y in viewport pixels.</param>
        /// <param name="EndX">Pointer-up X in viewport pixels.</param>
        /// <param name="EndY">Pointer-up Y in viewport pixels.</param>
        /// <returns>Selection rule for segment deletion.</returns>
        static public EraseSelectionMode ResolveSelectionMode
            (
            float StartX,
            float StartY,
            float EndX,
            float EndY
            )
        {
            bool DraggedDownward = EndY >= StartY;
            bool DraggedRightward = EndX >= StartX;

            // Drag down (EndY >= StartY) uses enter-and-inside per TOOLS-SPEC-ADDENDUM1 §3.3.1 / §3.3.2.
            if (DraggedDownward)
                return EraseSelectionMode.EnterAndCompletelyInside;

            return EraseSelectionMode.CompletelyInside;
        }

        /// <summary>
        /// Returns whether a screen point lies inside this rectangle inclusive of edges.
        /// </summary>
        /// <param name="ScreenX">Horizontal screen coordinate in pixels.</param>
        /// <param name="ScreenY">Vertical screen coordinate in pixels.</param>
        /// <returns>True when the point is inside the rectangle.</returns>
        public bool ContainsScreenPoint
            (
            float ScreenX,
            float ScreenY
            )
        {
            return (ScreenX >= Left) &&
                (ScreenX <= Right) &&
                (ScreenY >= Top) &&
                (ScreenY <= Bottom);
        }
    }
}
