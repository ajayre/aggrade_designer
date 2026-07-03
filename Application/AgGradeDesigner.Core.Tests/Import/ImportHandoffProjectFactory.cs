using AgGradeDesigner.Controls;
using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Import;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.TestFixtures;
using AgGradeDesigner.Rendering;

namespace AgGradeDesigner.Core.Tests.Import
{
    /// <summary>
    /// Builds imported AGD projects for handoff regression tests.
    /// </summary>
    internal static class ImportHandoffProjectFactory
    {
        /// <summary>Production default viewport width used before the viewer is laid out.</summary>
        internal const int ProductionDefaultViewportWidth = 800;

        /// <summary>Production default viewport height used before the viewer is laid out.</summary>
        internal const int ProductionDefaultViewportHeight = 600;

        static private string TheShop2AgdPath { get; } =
            Path.Combine(TestDataPaths.TestDataDirectory, "TheShop2_2ft.agd");

        /// <summary>
        /// Creates a project document from the FakeField_01 AGD fixture.
        /// </summary>
        internal static ProjectDocument CreateFakeField01Project
            (
            )
        {
            return CreateProjectFromAgd(TestDataPaths.FakeField01Agd);
        }

        /// <summary>
        /// Creates a project document from the TheShop2_2ft AGD fixture.
        /// </summary>
        internal static ProjectDocument CreateTheShop2Project
            (
            )
        {
            if (!File.Exists(TheShop2AgdPath))
                throw new FileNotFoundException($"Missing fixture: {TheShop2AgdPath}");

            return CreateProjectFromAgd(TheShop2AgdPath);
        }

        /// <summary>
        /// Applies the same 2D/3D camera fit used during AGD import in the main window.
        /// </summary>
        internal static void ApplyImportCameraFit
            (
            ProjectDocument Project,
            FieldViewerControl Viewer,
            int ViewportWidth,
            int ViewportHeight
            )
        {
            if (!FieldBounds.TryFromProject(Project, out FieldBounds Bounds))
                return;

            MapViewFitResult Fit = MapViewFitter.Fit(Bounds, ViewportWidth, ViewportHeight);
            Project.ViewerState.Camera2D.CentreLat = Fit.CentreLat;
            Project.ViewerState.Camera2D.CentreLon = Fit.CentreLon;
            Project.ViewerState.Camera2D.Zoom = Fit.ZoomFactor;
            Project.ViewerState.Camera3D.OrbitCentreLat = Fit.CentreLat;
            Project.ViewerState.Camera3D.OrbitCentreLon = Fit.CentreLon;
            OrbitViewFitter.ApplyInitialOrientation(Project.ViewerState.Camera3D);

            Viewer.CentreLatitude = Fit.CentreLat;
            Viewer.CentreLongitude = Fit.CentreLon;
            Viewer.ZoomFactor = Fit.ZoomFactor;
            Viewer.OrbitCentreLatitude = Fit.CentreLat;
            Viewer.OrbitCentreLongitude = Fit.CentreLon;
            Viewer.OrbitHeadingDeg = Project.ViewerState.Camera3D.OrbitHeadingDeg;
            Viewer.OrbitPitchDeg = Project.ViewerState.Camera3D.OrbitPitchDeg;
            Viewer.OrbitDistanceM = Project.ViewerState.Camera3D.DistanceM;
        }

        static private ProjectDocument CreateProjectFromAgd
            (
            string Path
            )
        {
            AgdImportResult Result = AgdService.Import(Path);
            ProjectDocument Project = new ProjectDocument
            {
                Survey = Result.Survey,
                DesignParameters = Result.Parameters,
                DesignGrid = Result.Grid,
                SurveySourceFormat = SurveySourceFormat.Agd
            };
            Project.ViewerState.FieldContentView = FieldContentView.Proposed;
            return Project;
        }
    }
}
