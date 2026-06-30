using AgGradeDesigner.Controls;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.ViewModels;

using Avalonia.Controls;

namespace AgGradeDesigner
{
    /// <summary>
    /// Primary application window hosting the field viewer and toolbar bindings.
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Initializes the window, view model, and field viewer event wiring.
        /// </summary>
        public MainWindow
            (
            )
        {
            InitializeComponent();
            MainWindowViewModel Vm = new MainWindowViewModel();
            DataContext = Vm;

            FieldViewerControl? Viewer = this.FindControl<FieldViewerControl>("FieldViewer");
            if (Viewer != null)
            {
                Vm.PrepareViewerAsync = async (Progress, Mode) =>
                {
                    Viewer.SuppressSynchronousPrepare = true;
                    try
                    {
                        return await Viewer.PrepareViewAsync(Progress, Mode);
                    }
                    finally
                    {
                        Viewer.SuppressSynchronousPrepare = false;
                    }
                };

                Vm.HasCachedViewerFrame = Mode => Viewer.HasCachedFrameForMode(Mode);

                Vm.ApplyOrbitFit = () =>
                {
                    Viewer.EnsureSurfaceBuiltForFit();
                    (int Width, int Height) = Viewer.GetViewportSize();
                    Vm.FitOrbitCameraFromViewer(Viewer.FieldSurface, Width, Height);
                };

                // Keep status bar coordinates and 3D orbit camera in sync with the live viewer.
                Viewer.ViewCentreChanged += (_, _) =>
                {
                    Vm.OnViewCentreChanged(Viewer.CentreLatitude, Viewer.CentreLongitude, Viewer.ZoomFactor);
                    if (Viewer.ViewMode == ViewMode.ThreeD)
                    {
                        Vm.OnOrbitCameraChanged(
                            Viewer.OrbitHeadingDeg,
                            Viewer.OrbitPitchDeg,
                            Viewer.OrbitDistanceM,
                            Viewer.OrbitCentreLatitude,
                            Viewer.OrbitCentreLongitude);
                    }
                    else
                    {
                        Vm.OnOrbitCentreChanged(Viewer.OrbitCentreLatitude, Viewer.OrbitCentreLongitude);
                    }
                };
            }

            // Invalidate Skia surface or visual layers when project or display bindings change.
            Vm.PropertyChanged += (_, Args) =>
            {
                if (Viewer == null)
                    return;

                string? Name = Args.PropertyName;
                if (Name == nameof(MainWindowViewModel.Project))
                {
                    Viewer.InvalidateSurface();
                }

                if (Name == nameof(MainWindowViewModel.HasSurvey) ||
                    Name == nameof(MainWindowViewModel.HasDesign))
                {
                    Viewer.InvalidateSurface();
                }

                if (Vm.SuppressViewerFrameInvalidation)
                    return;

                if (Name == nameof(MainWindowViewModel.Display) ||
                    Name == nameof(MainWindowViewModel.CentreLat) ||
                    Name == nameof(MainWindowViewModel.CentreLon) ||
                    Name == nameof(MainWindowViewModel.Zoom) ||
                    Name == nameof(MainWindowViewModel.SatelliteToggle) ||
                    Name == nameof(MainWindowViewModel.SurveyPointsToggle) ||
                    Name == nameof(MainWindowViewModel.ContoursToggle) ||
                    Name == nameof(MainWindowViewModel.HillshadeToggle) ||
                    Name == nameof(MainWindowViewModel.BenchmarksToggle) ||
                    Name == nameof(MainWindowViewModel.DesignOverlayToggle) ||
                    Name == nameof(MainWindowViewModel.VerticalExaggeration))
                {
                    Viewer.InvalidateRenderedFrame();
                }
            };
        }
    }
}
