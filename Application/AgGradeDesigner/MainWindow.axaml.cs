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
        /// <summary>The single field viewer. A bottom palette docks under it instead of swapping in another viewer.</summary>
        private FieldViewerControl? _FullViewer;
        /// <summary>Haul route scrub overlay drawn on top of the field viewer.</summary>
        private HaulRouteScrubOverlay? _FullHaulOverlay;
        /// <summary>Minimum map height in pixels while a bottom palette is open.</summary>
        private const double BOTTOM_PALETTE_MAP_MIN_HEIGHT = 120;
        /// <summary>Minimum docked palette height in pixels.</summary>
        private const double BOTTOM_PALETTE_MIN_HEIGHT = 180;

        /// <summary>When true, the next close proceeds without re-prompting for unsaved changes.</summary>
        private bool _AllowCloseWithoutPrompt;

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
            _FullHaulOverlay = this.FindControl<HaulRouteScrubOverlay>("FullHaulRouteScrubOverlay");
            if (_FullViewer != null)
            {
                _FullViewer.FeatureEditSession = Vm.FeatureEdits;
                WireViewerEvents(Vm, _FullViewer);
                WireMapToolEvents(Vm, _FullViewer);
                _FullViewer.IsPointerOverHaulScrubChrome = Point =>
                    IsPointerOverHaulScrub(_FullViewer, _FullHaulOverlay, Point);
                _FullViewer.FrameCacheRebuildProgressChanged = (Busy, Percent) =>
                    Vm.SetFrameCacheRebuildProgress(Busy, Percent);
            }

            Vm.PrepareViewerAsync = async (Progress, Mode) =>
                await ResolveActiveViewer().PrepareViewAsync(Progress, Mode);
            Vm.SyncHaulThreeDRouteScrubIndexToViewer = Index =>
                _FullViewer?.SetValue(FieldViewerControl.HaulThreeDRouteScrubIndexProperty, Index);
            Vm.PrepareViewerProgressiveAsync = async (Progress, OnEarlyDisplayReady, Mode) =>
                await ResolveActiveViewer().PrepareViewProgressiveAsync(Progress, OnEarlyDisplayReady, Mode);
            Vm.EnterViewerTransition = () => SetAllViewersSuppressSynchronousPrepare(true);
            Vm.ExitViewerTransition = () => ReleaseActiveViewerSuppressSynchronousPrepare();
            Vm.EnterImportInputBlock = () => SetAllViewersImportInputBlocked(true);
            Vm.ExitImportInputBlock = () => SetAllViewersImportInputBlocked(false);
            Vm.MarkViewerImportHandoffPendingFirstDraw = () =>
                ResolveActiveViewer().MarkImportHandoffPendingFirstCompositeDraw();
            Vm.PresentViewerCachedFrame = () =>
            {
                FieldViewerControl Viewer = ResolveActiveViewer();
                Viewer.TryMaterializeCompositeForCurrentView();
                Viewer.InvalidateVisual();
            };
            Vm.HasMaterializedViewerCompositeFrame = () =>
                ResolveActiveViewer().HasMaterializedCompositeFrameForCurrentView();
            Vm.EnsureViewerImportHandoffReadyAsync = () =>
            {
                FieldViewerControl Viewer = ResolveActiveViewer();
                ImportDiagnosticsLog.Write(
                    "Import",
                    "EnsureHandoffDispatch",
                    $"viewer=main size={Viewer.GetViewportSize().Width}x{Viewer.GetViewportSize().Height}");
                return Viewer.EnsureImportHandoffReadyAsync();
            };
            Vm.ScheduleViewerBackgroundPrewarm = () =>
                ResolveActiveViewer().ScheduleBackgroundPrewarmForCurrentView();
            Vm.GetViewerViewportSize = () => ResolveActiveViewer().GetViewportSize();
            Vm.WriteViewerImportDiagnosticsDelegate = Phase =>
            {
                FieldViewerControl Viewer = ResolveActiveViewer();
                ImportDiagnosticsLog.Write(
                    "ViewerSelect",
                    Phase,
                    $"active=main bounds={Viewer.Bounds.Width:0}x{Viewer.Bounds.Height:0}");
                Viewer.WriteImportDiagnostics(Phase);
            };
            Vm.MarkViewerImportPrepareComplete = () => ResolveActiveViewer().MarkImportPrepareComplete();
            Vm.ResetViewerForEmptyProject = () => ResolveActiveViewer().ResetForEmptyProject();
            Vm.HasCachedViewerFrame = Mode => ResolveActiveViewer().HasCachedFrameForMode(Mode);
            Vm.CanReusePreparedViewerFrame = Mode => ResolveActiveViewer().HasCachedFrameForMode(Mode);
            Vm.QueueInitial3DCameraFit = () => ResolveActiveViewer().QueueInitial3DCameraFit();
            Vm.InvalidateViewer3DFrameCache = () => ResolveActiveViewer().InvalidateFrameCacheForViewMode(ViewMode.ThreeD);
            Vm.InvalidateViewer2DFrameCache = () => ResolveActiveViewer().InvalidateFrameCacheForViewMode(ViewMode.TwoD);
            Vm.CancelViewer3DInteractiveFrameBuilds = () =>
                ResolveActiveViewer().CancelInFlightInteractiveFrameBuilds(ViewMode.ThreeD);
            Vm.CancelViewer2DInteractiveFrameBuilds = () =>
                ResolveActiveViewer().CancelInFlightInteractiveFrameBuilds(ViewMode.TwoD);
            Vm.InvalidateViewerSurface = () => _FullViewer?.InvalidateSurface();
            Vm.InvalidateViewerVisual = () => _FullViewer?.InvalidateVisual();

            Vm.PropertyChanged += (_, Args) =>
            {
                FieldViewerControl Viewer = ResolveActiveViewer();
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
                }

                if (Name == nameof(MainWindowViewModel.IsBottomPaletteVisible))
                {
                    // Resize the existing viewer. Do not swap in a second viewer that has no frame yet.
                    ApplyBottomPaletteLayout(Vm.IsBottomPaletteVisible);
                    ReleaseActiveViewerSuppressSynchronousPrepare();
                    _FullViewer?.InvalidateVisual();
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
                        Name == nameof(MainWindowViewModel.HaulFlowToggle) ||
                        Name == nameof(MainWindowViewModel.WaterToggle) ||
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
                        Viewer.ScheduleInteractiveRenderParameterRefresh();
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
                    Name == nameof(MainWindowViewModel.HaulFlowToggle) ||
                    Name == nameof(MainWindowViewModel.WaterToggle) ||
                    Name == nameof(MainWindowViewModel.DesignOverlayToggle))
                {
                    // intent: 3D keeps the last drawn frame while display toggles rebuild; full invalidation flashes black
                    if (Viewer.ViewMode == ViewMode.ThreeD)
                        Viewer.ScheduleInteractiveRenderedFrameRefresh();
                    else if (Viewer.UsesHaulBitmapTwoDRendering)
                        Viewer.InvalidateVisual();
                    else
                        Viewer.InvalidateRenderedFrame();
                }
                else if (Name == nameof(MainWindowViewModel.CentreLat) ||
                         Name == nameof(MainWindowViewModel.CentreLon) ||
                         Name == nameof(MainWindowViewModel.Zoom) ||
                         Name == nameof(MainWindowViewModel.VerticalExaggeration) ||
                         Name == nameof(MainWindowViewModel.TopoCutFillOpacity))
                {
                    Viewer.ScheduleInteractiveRenderParameterRefresh();
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
                _FullViewer?.NotifyDiagnosticsBoundsChanged("main");
            };

            PointerPressed += OnMainWindowPointerPressed;
            Closing += OnMainWindowClosing;
        }

        /// <summary>
        /// Prompts to save unsaved project changes before the main window closes.
        /// </summary>
        /// <param name="Sender">Event source.</param>
        /// <param name="Args">Closing event arguments.</param>
        private void OnMainWindowClosing
            (
            object? Sender,
            WindowClosingEventArgs Args
            )
        {
            if (_AllowCloseWithoutPrompt)
                return;

            if (DataContext is not MainWindowViewModel Vm || !Vm.Project.IsModified)
                return;

            // intent: defer close until the async save prompt completes
            Args.Cancel = true;
            _ = PromptCloseAsync(Vm);
        }

        /// <summary>
        /// Shows the unsaved-changes prompt and closes the window when the user confirms.
        /// </summary>
        /// <param name="Vm">Main window view model.</param>
        private async Task PromptCloseAsync
            (
            MainWindowViewModel Vm
            )
        {
            bool Proceed = await Vm.TryPromptSaveModifiedProjectAsync("closing").ConfigureAwait(true);
            if (!Proceed)
                return;

            _AllowCloseWithoutPrompt = true;
            Close();
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
            if (DataContext is not MainWindowViewModel Vm)
                return;

            if ((Args.Key == Key.Escape) && Vm.FeatureEdits.IsActive)
            {
                // Escape discards the unsaved well or road edit without using the sidebar tools.
                Vm.Features.CancelEdit();
                Args.Handled = true;
                return;
            }

            if (Vm.FieldContentView != FieldContentView.Haul ||
                !Vm.ShowHaulRouteSection)
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

            FieldViewerControl ActiveViewer = ResolveActiveViewer();
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
        }

        /// <summary>
        /// Enables or disables synchronous frame rebuilds on the field viewer.
        /// </summary>
        /// <param name="Suppress">True while a background prepare is in progress.</param>
        private void SetAllViewersSuppressSynchronousPrepare
            (
            bool Suppress
            )
        {
            if (_FullViewer != null)
                _FullViewer.SuppressSynchronousPrepare = Suppress;
        }

        /// <summary>
        /// Restores synchronous rendering on the field viewer after a background prepare.
        /// </summary>
        private void ReleaseActiveViewerSuppressSynchronousPrepare
            (
            )
        {
            if (_FullViewer != null)
                _FullViewer.SuppressSynchronousPrepare = false;
        }

        /// <summary>
        /// Gives the docked palette a share of the central column, or collapses that row when no palette is open.
        /// The same field viewer stays mounted so its last frame remains on screen.
        /// </summary>
        /// <param name="Open">True when AI or Features is docked.</param>
        private void ApplyBottomPaletteLayout
            (
            bool Open
            )
        {
            Grid? Host = this.FindControl<Grid>("ViewerHost");
            if ((Host == null) || (Host.RowDefinitions.Count < 3))
                return;

            RowDefinition MapRow = Host.RowDefinitions[0];
            RowDefinition PaletteRow = Host.RowDefinitions[2];
            if (Open)
            {
                MapRow.Height = new GridLength(2, GridUnitType.Star);
                MapRow.MinHeight = BOTTOM_PALETTE_MAP_MIN_HEIGHT;
                PaletteRow.Height = new GridLength(1, GridUnitType.Star);
                PaletteRow.MinHeight = BOTTOM_PALETTE_MIN_HEIGHT;
                return;
            }

            MapRow.Height = new GridLength(1, GridUnitType.Star);
            MapRow.MinHeight = 0;
            PaletteRow.Height = new GridLength(0);
            PaletteRow.MinHeight = 0;
        }

        /// <summary>
        /// Returns the field viewer in the central column.
        /// </summary>
        /// <returns>Active field viewer control.</returns>
        private FieldViewerControl ResolveActiveViewer
            (
            )
        {
            return _FullViewer!;
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

            Viewer.PointerPositionChanged += (Lat, Lon) => Vm.OnPointerPositionChanged(Lat, Lon);
            Viewer.PointerLeftView += (_, _) => Vm.OnPointerLeftView();

            Viewer.HaulRouteScrubStepRequested += (Delta, Multiply) =>
                Vm.StepHaulRouteScrub(Delta, Multiply);

            Viewer.HaulPivotLocationSetRequested += (Lat, Lon) =>
                Vm.SetHaulPivotLocation(Lat, Lon);
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
            Vm.InvalidateViewerDecorations = () => ResolveActiveViewer().InvalidateVisual();

            Vm.PropertyChanged += (_, Args) =>
            {
                if (Args.PropertyName == nameof(MainWindowViewModel.ActiveMapTool))
                {
                    if (_FullViewer != null)
                        _FullViewer.ActiveMapTool = Vm.ActiveMapTool;
                }
            };
        }
    }
}
