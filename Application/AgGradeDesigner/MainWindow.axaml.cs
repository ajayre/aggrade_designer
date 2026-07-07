using AgGradeDesigner.Controls;
using AgGradeDesigner.Core.MapTools;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Diagnostics;
using AgGradeDesigner.Rendering;
using AgGradeDesigner.ViewModels;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

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
        /// <summary>Haul route scrub overlay paired with the full-width viewer.</summary>
        private HaulRouteScrubOverlay? _FullHaulOverlay;
        /// <summary>Haul route scrub overlay paired with the split viewer.</summary>
        private HaulRouteScrubOverlay? _SplitHaulOverlay;

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
            _FullHaulOverlay = this.FindControl<HaulRouteScrubOverlay>("FullHaulRouteScrubOverlay");
            _SplitHaulOverlay = this.FindControl<HaulRouteScrubOverlay>("SplitHaulRouteScrubOverlay");
            if (_FullViewer != null)
            {
                WireViewerEvents(Vm, _FullViewer);
                WireMapToolEvents(Vm, _FullViewer);
                _FullViewer.IsPointerOverHaulScrubChrome = Point =>
                    IsPointerOverHaulScrub(_FullViewer, _FullHaulOverlay, Point);
            }

            if (_SplitViewer != null)
            {
                WireViewerEvents(Vm, _SplitViewer);
                WireMapToolEvents(Vm, _SplitViewer);
                _SplitViewer.IsPointerOverHaulScrubChrome = Point =>
                    IsPointerOverHaulScrub(_SplitViewer, _SplitHaulOverlay, Point);
            }

            Vm.PrepareViewerAsync = async (Progress, Mode) =>
                await ResolveActiveViewer(Vm).PrepareViewAsync(Progress, Mode);
            Vm.PrepareViewerProgressiveAsync = async (Progress, OnEarlyDisplayReady, Mode) =>
                await ResolveActiveViewer(Vm).PrepareViewProgressiveAsync(Progress, OnEarlyDisplayReady, Mode);
            Vm.EnterViewerTransition = () => SetAllViewersSuppressSynchronousPrepare(true);
            Vm.ExitViewerTransition = () => ReleaseActiveViewerSuppressSynchronousPrepare();
            Vm.EnterImportInputBlock = () => SetAllViewersImportInputBlocked(true);
            Vm.ExitImportInputBlock = () => SetAllViewersImportInputBlocked(false);
            Vm.MarkViewerImportHandoffPendingFirstDraw = () =>
                ResolveActiveViewer(Vm).MarkImportHandoffPendingFirstCompositeDraw();
            Vm.PresentViewerCachedFrame = () =>
            {
                FieldViewerControl Viewer = ResolveActiveViewer(Vm);
                Viewer.TryMaterializeCompositeForCurrentView();
                Viewer.InvalidateVisual();
            };
            Vm.HasMaterializedViewerCompositeFrame = () =>
                ResolveActiveViewer(Vm).HasMaterializedCompositeFrameForCurrentView();
            Vm.EnsureViewerImportHandoffReadyAsync = () =>
            {
                FieldViewerControl Viewer = ResolveActiveViewer(Vm);
                ImportDiagnosticsLog.Write(
                    "Import",
                    "EnsureHandoffDispatch",
                    $"viewer={(Vm.IsAiPanelVisible ? "split" : "full")} size={Viewer.GetViewportSize().Width}x{Viewer.GetViewportSize().Height}");
                return Viewer.EnsureImportHandoffReadyAsync();
            };
            Vm.ScheduleViewerBackgroundPrewarm = () =>
                ResolveActiveViewer(Vm).ScheduleBackgroundPrewarmForCurrentView();
            Vm.GetViewerViewportSize = () => ResolveActiveViewer(Vm).GetViewportSize();
            Vm.WriteViewerImportDiagnosticsDelegate = Phase =>
            {
                FieldViewerControl Viewer = ResolveActiveViewer(Vm);
                ImportDiagnosticsLog.Write(
                    "ViewerSelect",
                    Phase,
                    $"active={(Vm.IsAiPanelVisible ? "split" : "full")} bounds={Viewer.Bounds.Width:0}x{Viewer.Bounds.Height:0}");
                Viewer.WriteImportDiagnostics(Phase);
            };
            Vm.MarkViewerImportPrepareComplete = () => ResolveActiveViewer(Vm).MarkImportPrepareComplete();
            Vm.ResetViewerForEmptyProject = () => ResolveActiveViewer(Vm).ResetForEmptyProject();
            Vm.HasCachedViewerFrame = Mode => ResolveActiveViewer(Vm).HasCachedFrameForMode(Mode);
            Vm.QueueInitial3DCameraFit = () => ResolveActiveViewer(Vm).QueueInitial3DCameraFit();
            Vm.InvalidateViewer3DFrameCache = () => ResolveActiveViewer(Vm).InvalidateFrameCacheForViewMode(ViewMode.ThreeD);
            Vm.InvalidateViewer2DFrameCache = () => ResolveActiveViewer(Vm).InvalidateFrameCacheForViewMode(ViewMode.TwoD);
            Vm.InvalidateViewerSurface = () =>
            {
                _FullViewer?.InvalidateSurface();
                _SplitViewer?.InvalidateSurface();
            };
            Vm.InvalidateViewerVisual = () =>
            {
                _FullViewer?.InvalidateVisual();
                _SplitViewer?.InvalidateVisual();
            };

            Vm.PropertyChanged += (_, Args) =>
            {
                FieldViewerControl Viewer = ResolveActiveViewer(Vm);
                string? Name = Args.PropertyName;

                if (Name == nameof(MainWindowViewModel.HaulFramePngBytes) ||
                    Name == nameof(MainWindowViewModel.HaulRouteOverlayPngBytes) ||
                    Name == nameof(MainWindowViewModel.HaulFieldBoundaryPngBytes) ||
                    Name == nameof(MainWindowViewModel.HaulFieldBoundaryMinLat) ||
                    Name == nameof(MainWindowViewModel.HaulFieldBoundaryMaxLat) ||
                    Name == nameof(MainWindowViewModel.HaulFieldBoundaryMinLon) ||
                    Name == nameof(MainWindowViewModel.HaulFieldBoundaryMaxLon) ||
                    Name == nameof(MainWindowViewModel.HaulFrameKind) ||
                    Name == nameof(MainWindowViewModel.HasHaulFramePlacement) ||
                    Name == nameof(MainWindowViewModel.HaulFrameMinLat) ||
                    Name == nameof(MainWindowViewModel.HaulFrameMaxLat) ||
                    Name == nameof(MainWindowViewModel.HaulFrameMinLon) ||
                    Name == nameof(MainWindowViewModel.HaulFrameMaxLon) ||
                    Name == nameof(MainWindowViewModel.FieldContentView))
                {
                    if (Vm.IsApplyingHaulFrame)
                        return;

                    _FullViewer?.InvalidateVisual();
                    _SplitViewer?.InvalidateVisual();
                }

                if (Name == nameof(MainWindowViewModel.IsAiPanelVisible))
                {
                    ReleaseActiveViewerSuppressSynchronousPrepare();
                    _FullViewer?.InvalidateVisual();
                    _SplitViewer?.InvalidateVisual();
                    return;
                }

                if (Vm.SuppressViewerFrameInvalidation)
                {
                    if (Name == nameof(MainWindowViewModel.Display) ||
                        Name == nameof(MainWindowViewModel.SatelliteToggle) ||
                        Name == nameof(MainWindowViewModel.SurveyPointsToggle) ||
                        Name == nameof(MainWindowViewModel.ContoursToggle) ||
                        Name == nameof(MainWindowViewModel.HillshadeToggle) ||
                        Name == nameof(MainWindowViewModel.BenchmarksToggle) ||
                        Name == nameof(MainWindowViewModel.DesignOverlayToggle))
                    {
                        Viewer.InvalidateVisual();
                    }
                    else if (Name == nameof(MainWindowViewModel.CentreLat) ||
                             Name == nameof(MainWindowViewModel.CentreLon) ||
                             Name == nameof(MainWindowViewModel.Zoom) ||
                             Name == nameof(MainWindowViewModel.VerticalExaggeration) ||
                             Name == nameof(MainWindowViewModel.TopoCutFillOpacity))
                    {
                        Viewer.ScheduleInteractiveRenderedFrameRefresh();
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
                    Name == nameof(MainWindowViewModel.SatelliteToggle) ||
                    Name == nameof(MainWindowViewModel.SurveyPointsToggle) ||
                    Name == nameof(MainWindowViewModel.ContoursToggle) ||
                    Name == nameof(MainWindowViewModel.HillshadeToggle) ||
                    Name == nameof(MainWindowViewModel.BenchmarksToggle) ||
                    Name == nameof(MainWindowViewModel.DesignOverlayToggle))
                {
                    Viewer.InvalidateRenderedFrame();
                }
                else if (Name == nameof(MainWindowViewModel.CentreLat) ||
                         Name == nameof(MainWindowViewModel.CentreLon) ||
                         Name == nameof(MainWindowViewModel.Zoom) ||
                         Name == nameof(MainWindowViewModel.VerticalExaggeration) ||
                         Name == nameof(MainWindowViewModel.TopoCutFillOpacity))
                {
                    Viewer.ScheduleInteractiveRenderedFrameRefresh();
                }
            };

            AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
            AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Bubble);

            SizeChanged += (_, Args) =>
            {
                if (!ImportDiagnosticsLog.ShouldLog())
                    return;

                ImportDiagnosticsLog.WriteViewport(
                    "MainWindow",
                    (int)Args.NewSize.Width,
                    (int)Args.NewSize.Height,
                    $"client={ClientSize.Width:0}x{ClientSize.Height:0}");
                _FullViewer?.NotifyDiagnosticsBoundsChanged("full");
                _SplitViewer?.NotifyDiagnosticsBoundsChanged("split");
            };

            PointerPressed += OnMainWindowPointerPressed;
        }

        /// <summary>
        /// Handles keyboard route scrub shortcuts while the haul view is active (§7.4.32).
        /// </summary>
        /// <param name="Sender">Event source.</param>
        /// <param name="Args">Key event arguments.</param>
        private void OnWindowKeyDown
            (
            object? Sender,
            KeyEventArgs Args
            )
        {
            if (DataContext is not MainWindowViewModel Vm || Vm.FieldContentView != FieldContentView.Haul)
                return;

            bool Ctrl = Args.KeyModifiers.HasFlag(KeyModifiers.Control);
            int Step = Ctrl ? 10 : 1;
            if (Args.Key is Key.Left or Key.Down)
            {
                Vm.StepHaulRouteScrub(-Step);
                Args.Handled = true;
            }
            else if (Args.Key is Key.Right or Key.Up)
            {
                Vm.StepHaulRouteScrub(Step);
                Args.Handled = true;
            }
        }

        /// <summary>
        /// Deactivates the active map-editing tool when the user clicks outside the field viewer viewport (TOOLS-SPEC §2.0.13).
        /// </summary>
        /// <param name="Sender">Event source.</param>
        /// <param name="Args">Pointer event arguments.</param>
        private void OnMainWindowPointerPressed
            (
            object? Sender,
            PointerPressedEventArgs Args
            )
        {
            if (DataContext is not MainWindowViewModel Vm)
                return;

            if (Vm.ActiveMapTool == MapEditingToolKind.None)
                return;

            FieldViewerControl ActiveViewer = ResolveActiveViewer(Vm);
            if (!ActiveViewer.IsVisible)
                return;

            Avalonia.Point ViewerPoint = Args.GetPosition(ActiveViewer);
            if ((ViewerPoint.X >= 0.0) &&
                (ViewerPoint.Y >= 0.0) &&
                (ViewerPoint.X <= ActiveViewer.Bounds.Width) &&
                (ViewerPoint.Y <= ActiveViewer.Bounds.Height))
            {
                return;
            }

            Vm.DeactivateMapTool();
        }

        /// <summary>
        /// Returns true when a viewport point lies over the haul route scrub panel for the paired overlay.
        /// </summary>
        private static bool IsPointerOverHaulScrub
            (
            FieldViewerControl Viewer,
            HaulRouteScrubOverlay? Overlay,
            Point ViewerPoint
            )
        {
            if (Overlay == null || !Overlay.IsVisible)
                return false;

            Point? OverlayPoint = Overlay.TranslatePoint(ViewerPoint, Viewer);
            if (OverlayPoint == null)
                return false;

            bool Hit = Overlay.IsPointOverScrubPanel(OverlayPoint.Value);
            if (ImportDiagnosticsLog.ShouldLog())
            {
                ImportDiagnosticsLog.WriteHaulScrub(
                    "ChromeHitTest",
                    $"viewer=({ViewerPoint.X:0},{ViewerPoint.Y:0}) overlay=({OverlayPoint.Value.X:0},{OverlayPoint.Value.Y:0}) hit={Hit} {Overlay.DescribeScrubPanelBounds()}");
            }

            return Hit;
        }

        private void SetAllViewersImportInputBlocked
            (
            bool Blocked
            )
        {
            _FullViewer?.SetImportInputBlocked(Blocked);
            _SplitViewer?.SetImportInputBlocked(Blocked);
        }

        /// <summary>
        /// Enables or disables synchronous frame rebuilds on every viewer instance.
        /// </summary>
        /// <param name="Suppress">True while a background prepare is in progress.</param>
        private void SetAllViewersSuppressSynchronousPrepare
            (
            bool Suppress
            )
        {
            if (_FullViewer != null)
                _FullViewer.SuppressSynchronousPrepare = Suppress;

            if (_SplitViewer != null)
                _SplitViewer.SuppressSynchronousPrepare = Suppress;
        }

        /// <summary>
        /// Restores synchronous rendering only on the viewer currently shown to the user.
        /// </summary>
        private void ReleaseActiveViewerSuppressSynchronousPrepare
            (
            )
        {
            MainWindowViewModel Vm = (MainWindowViewModel)DataContext!;
            bool SplitActive = Vm.IsAiPanelVisible;

            if (_FullViewer != null)
                _FullViewer.SuppressSynchronousPrepare = SplitActive;

            if (_SplitViewer != null)
                _SplitViewer.SuppressSynchronousPrepare = !SplitActive;
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

            Viewer.HaulRouteScrubStepRequested += (Delta, Multiply) =>
                Vm.StepHaulRouteScrub(Delta, Multiply);
        }

        /// <summary>
        /// Wires map-editing tool session state and deactivation callbacks for one viewer.
        /// </summary>
        /// <param name="Vm">Main window view model.</param>
        /// <param name="Viewer">Field viewer control.</param>
        private void WireMapToolEvents
            (
            MainWindowViewModel Vm,
            FieldViewerControl Viewer
            )
        {
            Viewer.ToolDataSession = Vm.ToolDataSession;
            Viewer.ActiveMapTool = Vm.ActiveMapTool;
            Viewer.RequestDeactivateMapTool = () => Vm.DeactivateMapTool();
            Vm.InvalidateViewerDecorations = () => ResolveActiveViewer(Vm).InvalidateVisual();

            Vm.PropertyChanged += (_, Args) =>
            {
                if (Args.PropertyName == nameof(MainWindowViewModel.ActiveMapTool))
                {
                    _FullViewer!.ActiveMapTool = Vm.ActiveMapTool;
                    _SplitViewer!.ActiveMapTool = Vm.ActiveMapTool;
                }
            };
        }
    }
}
