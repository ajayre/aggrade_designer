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
        /// <summary>Full-width field viewer used when the AI panel is hidden.</summary>
        private FieldViewerControl? _FullViewer;
        /// <summary>Split-column field viewer shown beside the AI chat panel.</summary>
        private FieldViewerControl? _SplitViewer;

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

            _FullViewer = this.FindControl<FieldViewerControl>("FieldViewer");
            _SplitViewer = this.FindControl<FieldViewerControl>("FieldViewerSplit");
            if (_FullViewer != null)
                WireViewerEvents(Vm, _FullViewer);
            if (_SplitViewer != null)
                WireViewerEvents(Vm, _SplitViewer);

            Vm.PrepareViewerAsync = async (Progress, Mode) =>
                await ResolveActiveViewer(Vm).PrepareViewAsync(Progress, Mode);
            Vm.PrepareViewerProgressiveAsync = async (Progress, OnEarlyDisplayReady, Mode) =>
                await ResolveActiveViewer(Vm).PrepareViewProgressiveAsync(Progress, OnEarlyDisplayReady, Mode);
            Vm.EnterViewerTransition = () => ResolveActiveViewer(Vm).SuppressSynchronousPrepare = true;
            Vm.ExitViewerTransition = () => ResolveActiveViewer(Vm).SuppressSynchronousPrepare = false;
            Vm.PresentViewerCachedFrame = () =>
            {
                FieldViewerControl Viewer = ResolveActiveViewer(Vm);
                Viewer.MaterializeCachedLayerBitmapsForCurrentView();
                Viewer.TryPresentCachedFrame();
                Viewer.InvalidateVisual();
            };
            Vm.ResetViewerForEmptyProject = () => ResolveActiveViewer(Vm).ResetForEmptyProject();
            Vm.HasCachedViewerFrame = Mode => ResolveActiveViewer(Vm).HasCachedFrameForMode(Mode);
            Vm.QueueInitial3DCameraFit = () => ResolveActiveViewer(Vm).QueueInitial3DCameraFit();
            Vm.InvalidateViewer3DFrameCache = () => ResolveActiveViewer(Vm).InvalidateFrameCacheForViewMode(ViewMode.ThreeD);
            Vm.InvalidateViewer2DFrameCache = () => ResolveActiveViewer(Vm).InvalidateFrameCacheForViewMode(ViewMode.TwoD);

            Vm.PropertyChanged += (_, Args) =>
            {
                FieldViewerControl Viewer = ResolveActiveViewer(Vm);
                string? Name = Args.PropertyName;

                if (Name == nameof(MainWindowViewModel.IsAiPanelVisible))
                {
                    _FullViewer?.InvalidateVisual();
                    _SplitViewer?.InvalidateVisual();
                    return;
                }

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
                    Viewer.InvalidateSurface();

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

        /// <summary>
        /// Returns the field viewer currently shown in the central column.
        /// </summary>
        /// <param name="Vm">Main window view model.</param>
        /// <returns>Active field viewer control.</returns>
        private FieldViewerControl ResolveActiveViewer
            (
            MainWindowViewModel Vm
            )
        {
            if (Vm.IsAiPanelVisible && _SplitViewer != null)
                return _SplitViewer;

            return _FullViewer ?? _SplitViewer!;
        }

        /// <summary>
        /// Subscribes one viewer instance to camera change notifications.
        /// </summary>
        /// <param name="Vm">Main window view model.</param>
        /// <param name="Viewer">Field viewer control.</param>
        private static void WireViewerEvents
            (
            MainWindowViewModel Vm,
            FieldViewerControl Viewer
            )
        {
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
    }
}
