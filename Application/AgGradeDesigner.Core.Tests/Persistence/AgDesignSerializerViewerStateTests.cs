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
        /// Verifies viewer state fields survive a save and load cycle.
        /// </summary>
        [Fact]
        public void SaveAndLoad_PreservesViewerState
            (
            )
        {
            string Path = TempFile.Write(".agdesign", "");
            try
            {
                ProjectDocument Project = new ProjectDocument();
                Project.ViewerState.ViewMode = ViewMode.ThreeD;
                Project.ViewerState.FieldContentView = FieldContentView.Proposed;
                Project.ViewerState.Display.Satellite = true;
                Project.ViewerState.Display.VerticalExaggeration = 3.5;
                Project.ViewerState.Camera2D.CentreLat = 40.1;
                Project.ViewerState.Camera2D.CentreLon = -93.4;
                Project.ViewerState.Camera3D.OrbitHeadingDeg = 120;

                AgDesignSerializer.Save(Project, Path);
                ProjectDocument Loaded = AgDesignSerializer.Load(Path);

                // Camera, view mode, and display toggles must round-trip verbatim.
                Assert.Equal(ViewMode.ThreeD, Loaded.ViewerState.ViewMode);
                Assert.Equal(FieldContentView.Proposed, Loaded.ViewerState.FieldContentView);
                Assert.True(Loaded.ViewerState.Display.Satellite);
                Assert.Equal(3.5, Loaded.ViewerState.Display.VerticalExaggeration, 3);
                Assert.Equal(40.1, Loaded.ViewerState.Camera2D.CentreLat, 4);
                Assert.Equal(-93.4, Loaded.ViewerState.Camera2D.CentreLon, 4);
                Assert.Equal(120, Loaded.ViewerState.Camera3D.OrbitHeadingDeg, 3);
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }
    }
}
