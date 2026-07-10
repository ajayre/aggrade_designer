using System.Diagnostics;
using System.Reflection;

using AgGradeDesigner.Controls;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Water;
using AgGradeDesigner.Rendering;
using AgGradeDesigner.ViewModels;

namespace AgGradeDesigner.Core.Tests.TestFixtures
{
    /// <summary>
    /// Wires <see cref="MainWindowViewModel"/> to a headless viewer for field-content switch regression tests.
    /// </summary>
    internal static class FieldContentViewSwitchRegressionHarness
    {
        /// <summary>Maximum allowed warm proposed ↔ existing round-trip time in milliseconds.</summary>
        internal const int MaxWarmRoundTripMs = 120_000;

        /// <summary>
        /// Session wiring the main-window prepare callbacks to one headless viewer.
        /// </summary>
        internal sealed class Session
        {
            /// <summary>View model under test.</summary>
            internal MainWindowViewModel ViewModel { get; }

            /// <summary>Headless field viewer bound to the view model.</summary>
            internal FieldViewerControl Viewer { get; }

            /// <summary>Times <see cref="FieldViewerControl.InvalidateSurface"/> was invoked through viewer callbacks.</summary>
            internal int SurfaceInvalidateCount { get; private set; }

            /// <summary>
            /// Initializes a wired view-model and viewer session.
            /// </summary>
            /// <param name="ViewModel">Main window view model.</param>
            /// <param name="Viewer">Headless field viewer.</param>
            internal Session
                (
                MainWindowViewModel ViewModel,
                FieldViewerControl Viewer
                )
            {
                this.ViewModel = ViewModel;
                this.Viewer = Viewer;
            }

            /// <summary>
            /// Resets instrumentation counters before a warm-switch measurement window.
            /// </summary>
            internal void ResetInstrumentationCounters
                (
                )
            {
                SurfaceInvalidateCount = 0;
                Viewer.TestResetImportInstrumentation();
            }

            /// <summary>
            /// Switches the active field content view through the same commands used in the main window.
            /// </summary>
            /// <param name="View">Target content view.</param>
            /// <returns>A task that completes when the switch finishes.</returns>
            internal async Task SwitchContentViewAsync
                (
                FieldContentView View
                )
            {
                switch (View)
                {
                    case FieldContentView.Existing:
                        await ViewModel.SelectExistingCommand.ExecuteAsync(null);
                        break;
                    case FieldContentView.Proposed:
                        await ViewModel.SelectProposedCommand.ExecuteAsync(null);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(View), View, "Unsupported content view for switch regression.");
                }
            }

            /// <summary>
            /// Records one surface invalidation routed from the view model.
            /// </summary>
            internal void RecordSurfaceInvalidation
                (
                )
            {
                SurfaceInvalidateCount++;
            }
        }

        /// <summary>
        /// Creates a 3D session with proposed-only water overlay enabled, mirroring the regression scenario.
        /// </summary>
        /// <param name="Project">Imported design project.</param>
        /// <returns>Wired view-model and viewer session.</returns>
        internal static Session CreateThreeDSessionWithProposedWater
            (
            ProjectDocument Project
            )
        {
            GraphicsRuntime.Initialize();

            Project.ViewerState.ViewMode = ViewMode.ThreeD;
            Project.ViewerState.FieldContentView = FieldContentView.Proposed;
            Project.ViewerState.Display.Water = true;
            Project.Water.ProposedSurfaceCache = CreateSampleWaterCache(
                WaterBasisSurfaceKind.Proposed,
                WaterConstants.DEFAULT_PONDING_RAINFALL_MM,
                WaterConstants.DEFAULT_PONDING_CURVE_NUMBER);
            Project.Water.ExistingSurfaceCache = null;

            FieldViewerControl Viewer = ImportHandoffHarness.CreateViewer(Project);
            Viewer.ViewMode = ViewMode.ThreeD;
            Viewer.SuppressSynchronousPrepare = false;
            Viewer.EnsureSurfaceBuiltForFit();

            MainWindowViewModel ViewModel = new MainWindowViewModel();
            LoadProjectIntoViewModel(ViewModel, Project);
            ViewModel.WaterToggle = true;

            Session Session = new Session(ViewModel, Viewer);
            WireViewModelCallbacks(Session);
            SyncViewerFromProject(Viewer, Project);
            return Session;
        }

        /// <summary>
        /// Cold-builds proposed and existing 3D frames so later switches can reuse cached packets.
        /// </summary>
        /// <param name="Session">Wired session.</param>
        /// <returns>A task that completes when both content views are cached.</returns>
        internal static async Task WarmBothContentViewCachesAsync
            (
            Session Session
            )
        {
            await Session.SwitchContentViewAsync(FieldContentView.Proposed).ConfigureAwait(true);
            Assert.True(
                HasMonolithicThreeDCache(Session.Viewer, FieldContentView.Proposed),
                "Proposed 3D monolithic frame cache missing after cold prepare.");

            await Session.SwitchContentViewAsync(FieldContentView.Existing).ConfigureAwait(true);
            Assert.True(
                HasMonolithicThreeDCache(Session.Viewer, FieldContentView.Existing),
                "Existing 3D monolithic frame cache missing after cold prepare.");
        }

        /// <summary>
        /// Executes a warm proposed ↔ existing round trip and returns elapsed milliseconds.
        /// </summary>
        /// <param name="Session">Wired session with both content views already cached.</param>
        /// <returns>Round-trip duration in milliseconds.</returns>
        internal static async Task<long> RunWarmRoundTripAsync
            (
            Session Session
            )
        {
            Session.ResetInstrumentationCounters();
            Stopwatch Watch = Stopwatch.StartNew();
            // intent: return to proposed first so both switches cross a content-view boundary
            await Session.SwitchContentViewAsync(FieldContentView.Proposed).ConfigureAwait(true);
            await Session.SwitchContentViewAsync(FieldContentView.Existing).ConfigureAwait(true);
            await Session.SwitchContentViewAsync(FieldContentView.Proposed).ConfigureAwait(true);
            Session.ViewModel.PresentViewerCachedFrame?.Invoke();
            Watch.Stop();
            return Watch.ElapsedMilliseconds;
        }

        /// <summary>
        /// Copies the current 3D monolithic frame bytes for pixel analysis.
        /// </summary>
        /// <param name="Session">Wired session.</param>
        /// <returns>BGRA pixel buffer or null when no frame is cached.</returns>
        internal static byte[]? CopyCurrentThreeDFrameBytes
            (
            Session Session
            )
        {
            FieldContentView ContentView = Session.ViewModel.FieldContentView;
            return Session.Viewer.TestCopy3DMonolithicPacketBytes(ContentView);
        }

        /// <summary>
        /// Returns true when a monolithic 3D packet is cached for the requested content view.
        /// </summary>
        /// <param name="Viewer">Headless field viewer.</param>
        /// <param name="ContentView">Content view slot to inspect.</param>
        /// <returns>True when cached pixel bytes exist.</returns>
        internal static bool HasMonolithicThreeDCache
            (
            FieldViewerControl Viewer,
            FieldContentView ContentView
            )
        {
            return Viewer.TestCopy3DMonolithicPacketBytes(ContentView) != null;
        }

        /// <summary>
        /// Assigns a project document to the view model and refreshes bindable state.
        /// </summary>
        /// <param name="ViewModel">Main window view model.</param>
        /// <param name="Project">Project to load.</param>
        static private void LoadProjectIntoViewModel
            (
            MainWindowViewModel ViewModel,
            ProjectDocument Project
            )
        {
            FieldInfo ProjectField = typeof(MainWindowViewModel).GetField("_Project", BindingFlags.Instance | BindingFlags.NonPublic)!;
            ProjectField.SetValue(ViewModel, Project);

            MethodInfo RefreshAll = typeof(MainWindowViewModel).GetMethod("RefreshAll", BindingFlags.Instance | BindingFlags.NonPublic)!;
            RefreshAll.Invoke(ViewModel, null);
        }

        /// <summary>
        /// Mirrors the main-window viewer callbacks needed for content-view switching tests.
        /// </summary>
        /// <param name="Session">Session to wire.</param>
        static private void WireViewModelCallbacks
            (
            Session Session
            )
        {
            MainWindowViewModel ViewModel = Session.ViewModel;
            FieldViewerControl Viewer = Session.Viewer;

            ViewModel.EnterViewerTransition = () => Viewer.SuppressSynchronousPrepare = true;
            ViewModel.ExitViewerTransition = () => Viewer.SuppressSynchronousPrepare = false;
            ViewModel.PresentViewerCachedFrame = () =>
            {
                Viewer.TryMaterializeCompositeForCurrentView();
                Viewer.InvalidateVisual();
            };
            ViewModel.HasMaterializedViewerCompositeFrame = () => Viewer.HasMaterializedCompositeFrameForCurrentView();
            ViewModel.HasCachedViewerFrame = Mode => Viewer.HasCachedFrameForMode(Mode);
            ViewModel.CanReusePreparedViewerFrame = Mode => Viewer.HasCachedFrameForMode(Mode);
            ViewModel.InvalidateViewer3DFrameCache = () => Viewer.InvalidateFrameCacheForViewMode(ViewMode.ThreeD);
            ViewModel.InvalidateViewer2DFrameCache = () => Viewer.InvalidateFrameCacheForViewMode(ViewMode.TwoD);
            ViewModel.InvalidateViewerSurface = () =>
            {
                Session.RecordSurfaceInvalidation();
                Viewer.InvalidateSurface();
            };
            ViewModel.InvalidateViewerVisual = () => Viewer.InvalidateVisual();
            ViewModel.InvalidateViewerDecorations = () => Viewer.InvalidateVisual();
            ViewModel.PrepareViewerAsync = async (Progress, Mode) =>
                await Viewer.PrepareViewAsync(Progress, Mode).ConfigureAwait(true);

            ViewModel.PropertyChanged += (_, Args) =>
            {
                string? Name = Args.PropertyName;
                if (Name == nameof(MainWindowViewModel.FieldContentView))
                {
                    Viewer.InvalidateVisual();
                    return;
                }

                if (ViewModel.SuppressViewerFrameInvalidation)
                    return;

                if (Name == nameof(MainWindowViewModel.Display) ||
                    Name == nameof(MainWindowViewModel.WaterToggle) ||
                    Name == nameof(MainWindowViewModel.HaulFlowToggle) ||
                    Name == nameof(MainWindowViewModel.ContoursToggle) ||
                    Name == nameof(MainWindowViewModel.HillshadeToggle))
                {
                    if (Viewer.ViewMode == ViewMode.ThreeD)
                        Viewer.ScheduleInteractiveRenderedFrameRefresh();
                    else
                        Viewer.InvalidateRenderedFrame();
                }
            };
        }

        /// <summary>
        /// Keeps viewer control state aligned with the loaded project document.
        /// </summary>
        /// <param name="Viewer">Headless field viewer.</param>
        /// <param name="Project">Active project.</param>
        static private void SyncViewerFromProject
            (
            FieldViewerControl Viewer,
            ProjectDocument Project
            )
        {
            Viewer.Project = Project;
            Viewer.DisplaySettings = Project.ViewerState.Display;
        }

        /// <summary>
        /// Builds a small valid water cache entry for proposed-only overlay tests.
        /// </summary>
        /// <param name="BasisSurface">Basis surface kind.</param>
        /// <param name="RainfallMm">Rainfall used for the cache.</param>
        /// <param name="CurveNumber">Curve number used for the cache.</param>
        /// <returns>Valid cache entry.</returns>
        static private WaterLayerCacheEntry CreateSampleWaterCache
            (
            WaterBasisSurfaceKind BasisSurface,
            uint RainfallMm,
            uint CurveNumber
            )
        {
            float[] Depths = [0f, 0.5f, 0f, 0f];
            WaterLayerCacheEntry Entry = new WaterLayerCacheEntry
            {
                BasisSurface = BasisSurface,
                RouteScrubIndex = BasisSurface == WaterBasisSurfaceKind.HaulCutFill ? 0 : null,
                PondingRainfallMm = RainfallMm,
                PondingCurveNumber = CurveNumber,
                GridWidth = 2,
                GridHeight = 2,
                CellSizeM = FieldConstants.BIN_SIZE_M,
                MinLat = 40.0,
                MaxLat = 40.01,
                MinLon = -88.0,
                MaxLon = -87.99,
                PondDepthM = Depths,
            };
            PondingAnalysisEngine.AssignSmoothOverlay(Entry, new WaterSettings
            {
                PondingRainfallMm = RainfallMm,
                PondingCurveNumber = CurveNumber,
            });
            Entry.PayloadBlob = WaterLayerBlobCodec.Encode(Entry);
            return Entry;
        }
    }
}
