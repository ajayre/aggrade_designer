using AgGradeDesigner.Controls;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Rendering;
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
            GraphicsRuntime.Initialize();
            MainWindowViewModel Vm = new MainWindowViewModel();
            Vm.GraphicsRuntimeStatus = GraphicsRuntime.DescribeVulkanStatus();
            DataContext = Vm;

            FieldViewerControl? Viewer = this.FindControl<FieldViewerControl>("FieldViewer");
            if (Viewer != null)
            {
                Vm.PrepareViewerAsync = async (Progress, Mode) =>
                    await Viewer.PrepareViewAsync(Progress, Mode);

                Vm.PrepareViewerProgressiveAsync = async (Progress, OnEarlyDisplayReady, Mode) =>
                    await Viewer.PrepareViewProgressiveAsync(Progress, OnEarlyDisplayReady, Mode);

                Vm.EnterViewerTransition = () => Viewer.SuppressSynchronousPrepare = true;
                Vm.ExitViewerTransition = () => Viewer.SuppressSynchronousPrepare = false;

                Vm.PresentViewerCachedFrame = () =>
                {
                    Viewer.MaterializeCachedLayerBitmapsForCurrentView();
                    Viewer.TryPresentCachedFrame();
                    Viewer.InvalidateVisual();
                };

                Vm.ResetViewerForEmptyProject = () => Viewer.ResetForEmptyProject();

                Vm.HasCachedViewerFrame = Mode => Viewer.HasCachedFrameForMode(Mode);

                Vm.QueueInitial3DCameraFit = () => Viewer.QueueInitial3DCameraFit();

                Vm.InvalidateViewer3DFrameCache = () => Viewer.InvalidateFrameCacheForViewMode(ViewMode.ThreeD);
                Vm.InvalidateViewer2DFrameCache = () => Viewer.InvalidateFrameCacheForViewMode(ViewMode.TwoD);

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
                if (Vm.SuppressViewerFrameInvalidation)
                {
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
                        Name == nameof(MainWindowViewModel.VerticalExaggeration) ||
                        Name == nameof(MainWindowViewModel.TopoCutFillOpacity))
                    {
                        Viewer.InvalidateVisual();
                    }

                    return;
                }

                if (Name == nameof(MainWindowViewModel.Project))
                {
                    Viewer.InvalidateSurface();
                }

                if (Name == nameof(MainWindowViewModel.HasSurvey) ||
                    Name == nameof(MainWindowViewModel.HasDesign))
                {
                    Viewer.InvalidateSurface();
                }

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
                else if (Name == nameof(MainWindowViewModel.TopoCutFillOpacity))
                {
                    Viewer.InvalidateVisual();
                }
            };
        }
    }
}
