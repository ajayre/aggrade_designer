using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Rendering
{
    /// <summary>
    /// Decides when elevation contour lines should be drawn for a field content view.
    /// </summary>
    static public class ContourVisibility
    {
        /// <summary>
        /// Minimum design composite grade (percent) required before proposed contours are drawn.
        /// </summary>
        public const double MinDesignGradeForContoursPercent = 0.03;

        /// <summary>Multiplier converting plane gradient magnitude to slope percent.</summary>
        private const double SlopePercentScale = 100.0;

        /// <summary>
        /// Returns true when contour lines should be extracted and drawn for the active view.
        /// </summary>
        /// <param name="Display">Viewer display toggles.</param>
        /// <param name="Project">Current project, when available.</param>
        /// <param name="ContentView">Active scalar field view.</param>
        /// <returns>True when contours should be shown.</returns>
        static public bool ShouldDrawContours
            (
            DisplaySettings Display,
            ProjectDocument? Project,
            FieldContentView ContentView
            )
        {
            if (!Display.Contours)
                return false;

            if (ContentView != FieldContentView.Proposed)
                return true;

            DesignGrid? Grid = Project?.DesignGrid;
            if (Grid == null)
                return true;

            // intent: near-flat proposed planes produce unusable micro-contours; skip them entirely
            return ResolveProposedGradePercent(Grid) >= MinDesignGradeForContoursPercent;
        }

        /// <summary>
        /// Resolves the proposed-surface grade used for contour gating.
        /// </summary>
        /// <param name="Grid">Design grid with composite slope and/or plane coefficients.</param>
        /// <returns>Grade percent from persisted composite slope, else from plane magnitude.</returns>
        static public double ResolveProposedGradePercent
            (
            DesignGrid Grid
            )
        {
            if (Grid.CompositeSlopePercent > 0.0)
                return Grid.CompositeSlopePercent;

            // Legacy projects omitted CompositeSlopePercent; derive from persisted plane gradients.
            double Mag = Math.Sqrt((Grid.PlaneA * Grid.PlaneA) + (Grid.PlaneB * Grid.PlaneB));
            return Mag * SlopePercentScale;
        }
    }
}
