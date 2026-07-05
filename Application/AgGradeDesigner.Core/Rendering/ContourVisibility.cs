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
            return Grid.CompositeSlopePercent >= MinDesignGradeForContoursPercent;
        }
    }
}
