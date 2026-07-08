using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Persistence;

using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Persistence
{
    /// <summary>
    /// Unit tests for <see cref="AgDesignSerializer"/> viewer state persistence.
    /// </summary>
    public sealed class AgDesignSerializerViewerStateTests
    {
        /// <summary>
        /// Vertical exaggeration used by this component.
        /// </summary>

        private const double VERTICAL_EXAGGERATION = 3.5;
        /// <summary>
        /// Centre lat used by this component.
        /// </summary>

        private const double CENTRE_LAT = 40.1;
        /// <summary>
        /// Centre lon used by this component.
        /// </summary>

        private const double CENTRE_LON = -93.4;
        /// <summary>
        /// Orbit heading deg used by this component.
        /// </summary>

        private const double ORBIT_HEADING_DEG = 120;
        /// <summary>
        /// Vertical exaggeration precision used by this component.
        /// </summary>

        private const int VERTICAL_EXAGGERATION_PRECISION = 3;
        /// <summary>
        /// Coord precision used by this component.
        /// </summary>

        private const int COORD_PRECISION = 4;
        /// <summary>
        /// Heading precision used by this component.
        /// </summary>

        private const int HEADING_PRECISION = 3;

        /// <summary>
        /// Verifies viewer state fields survive a save and load cycle.
        /// </summary>
        [Fact]
        public void SaveAndLoad_PreservesViewerState
            (
            )
        {
            string Path = TempFile.Path(ProjectDocument.FILE_EXTENSION);
            try
            {
                ProjectDocument Project = new ProjectDocument();
                Project.ViewerState.ViewMode = ViewMode.ThreeD;
                Project.ViewerState.FieldContentView = FieldContentView.Proposed;
                Project.ViewerState.Display.Satellite = true;
                Project.ViewerState.Display.HaulFlow = true;
                Project.ViewerState.Display.VerticalExaggeration = VERTICAL_EXAGGERATION;
                Project.ViewerState.Camera2D.CentreLat = CENTRE_LAT;
                Project.ViewerState.Camera2D.CentreLon = CENTRE_LON;
                Project.ViewerState.Camera3D.OrbitHeadingDeg = ORBIT_HEADING_DEG;

                AgDesignSerializer.Save(Project, Path);
                ProjectDocument Loaded = AgDesignSerializer.Load(Path);

                // Camera, view mode, and display toggles must round-trip verbatim.
                Assert.Equal(ViewMode.ThreeD, Loaded.ViewerState.ViewMode);
                Assert.Equal(FieldContentView.Proposed, Loaded.ViewerState.FieldContentView);
                Assert.True(Loaded.ViewerState.Display.Satellite);
                Assert.True(Loaded.ViewerState.Display.HaulFlow);
                Assert.Equal(VERTICAL_EXAGGERATION, Loaded.ViewerState.Display.VerticalExaggeration, VERTICAL_EXAGGERATION_PRECISION);
                Assert.Equal(CENTRE_LAT, Loaded.ViewerState.Camera2D.CentreLat, COORD_PRECISION);
                Assert.Equal(CENTRE_LON, Loaded.ViewerState.Camera2D.CentreLon, COORD_PRECISION);
                Assert.Equal(ORBIT_HEADING_DEG, Loaded.ViewerState.Camera3D.OrbitHeadingDeg, HEADING_PRECISION);
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }
    }
}
