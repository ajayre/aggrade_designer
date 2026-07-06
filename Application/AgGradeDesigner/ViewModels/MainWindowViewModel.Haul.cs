using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Diagnostics;
using AgGradeDesigner.Rendering;
using AgGradeDesigner.Services;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// Haul Builder integration for <see cref="MainWindowViewModel"/> (HAUL-SPEC Phase 1).
    /// </summary>
    public sealed partial class MainWindowViewModel
    {
        /// <summary>Route scrub controller for frame cache and prefetch.</summary>
        private readonly HaulScrubController _HaulScrubController = new HaulScrubController();

        /// <summary>Whether the current project includes displayable haul data.</summary>
        [ObservableProperty]
        private bool _HasHaulData;

        /// <summary>Whether the haul field content view can be selected.</summary>
        [ObservableProperty]
        private bool _CanHaulView;

        /// <summary>Whether the haul view button is highlighted.</summary>
        [ObservableProperty]
        private bool _IsHaulActive;

        /// <summary>Whether the HAUL ROUTE sidebar section is visible.</summary>
        [ObservableProperty]
        private bool _ShowHaulRouteSection;

        /// <summary>Current route scrub index bound to the vertical slider.</summary>
        [ObservableProperty]
        private int _RouteScrubIndex;

        /// <summary>Maximum route scrub index (<c>N - 1</c>).</summary>
        [ObservableProperty]
        private int _RouteScrubMaximum;

        /// <summary>Read-only route position label (<c>{current}/{total}</c>).</summary>
        [ObservableProperty]
        private string _RoutePositionLabel = "0/0";

        /// <summary>PNG bytes for the active haul scrub frame.</summary>
        [ObservableProperty]
        private byte[]? _HaulFramePngBytes;

        /// <summary>Suppresses scrub drag notifications while bindings are synced from the controller.</summary>
        private bool _SuppressHaulScrubNotify;

        /// <summary>Cancels the foreground haul frame progress pump.</summary>
        private CancellationTokenSource? _HaulFrameProgressCts;

        /// <summary>Whether generate-haul is available for the current project.</summary>
        public bool CanGenerateHaul => HasSurvey && HasDesign && !IsViewerBusy && !IsDesignBusy;

        /// <summary>
        /// Initializes haul scrub controller callbacks.
        /// </summary>
        private void InitializeHaulIntegration
            (
            )
        {
            _HaulScrubController.FrameChanged += OnHaulFrameChanged;
            _HaulScrubController.StateChanged += OnHaulScrubStateChanged;
            _HaulScrubController.ForegroundGenerationStarted += OnHaulForegroundGenerationStarted;
            _HaulScrubController.ForegroundGenerationCompleted += OnHaulForegroundGenerationCompleted;
            _HaulScrubController.PrefetchDiagnosticSink = (Phase, Detail) =>
                ImportDiagnosticsLog.WriteHaulPrefetch(Phase, Detail);
        }

        /// <summary>
        /// Switches the viewer to the haul field content view.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanHaulView))]
        private async Task SelectHaulAsync
            (
            )
        {
            if (!CanHaulView || IsViewerBusy)
                return;

            DeactivateMapTool();
            await ActivateHaulViewAsync(Generated: false).ConfigureAwait(true);
        }

        /// <summary>
        /// Opens the haul configuration dialog and runs haul generation when accepted.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanGenerateHaul))]
        private async Task OpenHaulDialogAsync
            (
            )
        {
            if (!CanGenerateHaul)
                return;

            HaulDialogViewModel? Result = await DialogService.ShowHaulDialogAsync().ConfigureAwait(true);
            if (Result == null)
                return;

            await RunHaulGenerationAsync().ConfigureAwait(true);
        }

        /// <summary>
        /// Runs haul generation for the active project using persisted haul settings.
        /// </summary>
        private async Task RunHaulGenerationAsync
            (
            )
        {
            IsDesignBusy = true;
            SetDesignBusyProgress(5, "Generating haul plan…");
            await YieldUiAsync().ConfigureAwait(true);
            try
            {
                HaulSettings Settings = UserSettingsService.Instance.ActiveHaulSettings;
                SetDesignBusyProgress(10, "Generating haul plan…");
                using CancellationTokenSource GenerateProgressCts = new CancellationTokenSource();
                Task GenerateProgressTask = RunDesignProgressPumpAsync(
                    10,
                    55,
                    "Generating haul plan…",
                    GenerateProgressCts.Token);
                HaulData Data = await Task.Run(() => HaulEngineService.Generate(_Project, Settings)).ConfigureAwait(true);
                GenerateProgressCts.Cancel();
                await GenerateProgressTask.ConfigureAwait(true);

                if (!Data.IsValid)
                {
                    string Detail = Data.Payload.Messages.Count > 0
                        ? string.Join(Environment.NewLine, Data.Payload.Messages)
                        : "The haul engine did not produce any routes.";
                    await DialogService.ShowMessageAsync("Haul generation failed", Detail).ConfigureAwait(true);
                    return;
                }

                _Project.HaulData = Data;
                _Project.MarkModified();
                RefreshHaulState();
                SetDesignBusyProgress(58, "Saving haul data…");
                SetDesignBusyProgress(60, "Preparing haul view…");
                using CancellationTokenSource PrepareProgressCts = new CancellationTokenSource();
                Task PrepareProgressTask = RunDesignProgressPumpAsync(
                    60,
                    78,
                    "Preparing haul view…",
                    PrepareProgressCts.Token);
                await ActivateHaulViewAsync(Generated: true).ConfigureAwait(true);
                PrepareProgressCts.Cancel();
                await PrepareProgressTask.ConfigureAwait(true);
                SetDesignBusyProgress(80, "Rendering haul frame…");
                using CancellationTokenSource FrameProgressCts = new CancellationTokenSource();
                Task FrameProgressTask = RunDesignProgressPumpAsync(
                    80,
                    95,
                    "Rendering haul frame…",
                    FrameProgressCts.Token);
                await WaitForFirstHaulFrameAsync().ConfigureAwait(true);
                FrameProgressCts.Cancel();
                await FrameProgressTask.ConfigureAwait(true);
                SetDesignBusyProgress(100, "Ready");
            }
            catch (Exception Ex)
            {
                await DialogService.ShowMessageAsync("Haul generation failed", Ex.Message).ConfigureAwait(true);
            }
            finally
            {
                ClearDesignBusyOverlay();
            }
        }

        /// <summary>
        /// Activates the haul view, forces 2D mode, and attaches scrub state.
        /// </summary>
        /// <param name="Generated">True when activation follows a fresh haul generation run.</param>
        private async Task ActivateHaulViewAsync
            (
            bool Generated
            )
        {
            if (_Project.HaulData == null || !_Project.HasHaulData)
                return;

            if (Generated)
                _HaulScrubController.Clear();

            _Project.ViewerState.FieldContentView = FieldContentView.Haul;
            _Project.ViewerState.ViewMode = ViewMode.TwoD;
            _SuppressModeChange = true;
            IsTwoD = true;
            IsThreeD = false;
            ShowVerticalExaggeration = false;
            ShowHaulRouteSection = false;
            _SuppressModeChange = false;

            RefreshViewButtons();
            UpdateLegend();
            OnPropertyChanged(nameof(FieldContentView));
            OnPropertyChanged(nameof(CanSwitchViewerView));

            HaulSettings Settings = UserSettingsService.Instance.ActiveHaulSettings;
            await Task.Run(() => _Project.HaulData.EnsureGuidanceForDisplay(_Project, Settings)).ConfigureAwait(true);
            await _HaulScrubController.AttachAsync(_Project, _Project.HaulData).ConfigureAwait(true);
            SyncHaulScrubBindings();
            await YieldUiAsync().ConfigureAwait(true);
            ShowHaulRouteSection = true;

            if (PrepareViewerAsync != null)
            {
                SuppressViewerFrameInvalidation = true;
                try
                {
                    await PrepareViewerAsync(EmptyViewerProgress, ViewMode.TwoD).ConfigureAwait(true);
                }
                finally
                {
                    SuppressViewerFrameInvalidation = false;
                }
            }

            InvalidateViewerSurface?.Invoke();
        }

        /// <summary>
        /// Refreshes haul-related enablement flags from project state.
        /// </summary>
        private void RefreshHaulState
            (
            )
        {
            HasHaulData = _Project.HasHaulData;
            CanHaulView = HasHaulData;
            NotifyGenerateHaulAvailability();
            SelectHaulCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Notifies bindings and commands that depend on <see cref="CanGenerateHaul"/>.
        /// </summary>
        private void NotifyGenerateHaulAvailability
            (
            )
        {
            OnPropertyChanged(nameof(CanGenerateHaul));
            OpenHaulDialogCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Applies open-project haul view rules (§7.5.4).
        /// </summary>
        private void ApplyOpenHaulFieldContentView
            (
            )
        {
            if (!_Project.HasHaulData || _Project.HaulData == null)
                return;

            _Project.ViewerState.FieldContentView = FieldContentView.Haul;
            _Project.ViewerState.ViewMode = ViewMode.TwoD;
            _SuppressModeChange = true;
            IsTwoD = true;
            IsThreeD = false;
            ShowVerticalExaggeration = false;
            _SuppressModeChange = false;
            RefreshViewButtons();
            _ = AttachOpenHaulScrubAsync();
        }

        /// <summary>
        /// Attaches haul scrub state after opening a project without blocking the UI thread.
        /// </summary>
        private async Task AttachOpenHaulScrubAsync
            (
            )
        {
            if (_Project.HaulData == null)
                return;

            HaulSettings Settings = UserSettingsService.Instance.ActiveHaulSettings;
            await Task.Run(() => _Project.HaulData.EnsureGuidanceForDisplay(_Project, Settings)).ConfigureAwait(true);
            await _HaulScrubController.AttachAsync(_Project, _Project.HaulData).ConfigureAwait(true);
            SyncHaulScrubBindings();
            await YieldUiAsync().ConfigureAwait(true);
            ShowHaulRouteSection = true;
            InvalidateViewerSurface?.Invoke();
        }

        /// <summary>
        /// Copies scrub controller state into bindable properties.
        /// </summary>
        private void SyncHaulScrubBindings
            (
            )
        {
            _SuppressHaulScrubNotify = true;
            try
            {
                RouteScrubIndex = _HaulScrubController.RouteScrubIndex;
                RouteScrubMaximum = Math.Max(0, _HaulScrubController.RouteStepCount - 1);
                RoutePositionLabel = _HaulScrubController.RoutePositionLabel;
                OnPropertyChanged(nameof(HaulFrameCacheTicks));
                OnPropertyChanged(nameof(HaulFrameCacheRevision));
            }
            finally
            {
                _SuppressHaulScrubNotify = false;
            }
        }

        /// <summary>
        /// Handles user-driven route scrub slider changes from the overlay control.
        /// </summary>
        /// <param name="Value">New slider value.</param>
        public void HandleRouteScrubSliderChanged
            (
            int Value
            )
        {
            if (!ShowHaulRouteSection || _Project.ViewerState.FieldContentView != FieldContentView.Haul)
                return;

            RouteScrubIndex = Value;
            RoutePositionLabel = _HaulScrubController.RouteStepCount <= 0
                ? "0/0"
                : $"{Value + 1}/{_HaulScrubController.RouteStepCount}";
            _HaulScrubController.NotifySliderDrag(Value);
        }

        /// <summary>
        /// Tick-mark cache states for the route scrub slider (§7.4.27).
        /// </summary>
        public IReadOnlyList<bool> HaulFrameCacheTicks
        {
            get
            {
                int Count = _HaulScrubController.RouteStepCount;
                bool[] Ticks = new bool[Count];
                for (int Index = 0; Index < Count; Index++)
                    Ticks[Index] = _HaulScrubController.Cache.IsCached(Index);

                return Ticks;
            }
        }

        /// <summary>
        /// Increments whenever prefetch or foreground generation updates the frame cache.
        /// </summary>
        public int HaulFrameCacheRevision { get; private set; }

        /// <summary>
        /// Updates the live route scrub index while the slider is dragged.
        /// </summary>
        /// <param name="Value">New slider value.</param>
        partial void OnRouteScrubIndexChanged
            (
            int Value
            )
        {
            if (_SuppressHaulScrubNotify)
                return;
        }

        /// <summary>
        /// Commits the route scrub index when the slider is released.
        /// </summary>
        public void CommitRouteScrubFromSlider
            (
            )
        {
            _HaulScrubController.NotifySliderReleased();
            SyncHaulScrubBindings();
        }

        /// <summary>
        /// Steps the route scrub index from keyboard input (§7.4.32).
        /// </summary>
        /// <param name="Delta">Frame delta.</param>
        /// <param name="Multiply">Multiplier for Ctrl+arrow stepping.</param>
        public void StepHaulRouteScrub
            (
            int Delta,
            int Multiply = 1
            )
        {
            _HaulScrubController.StepRouteScrubIndex(Delta * Multiply);
            SyncHaulScrubBindings();
        }

        /// <summary>
        /// Handles haul frame PNG updates from the scrub controller.
        /// </summary>
        /// <param name="PngBytes">New frame bytes.</param>
        private void OnHaulFrameChanged
            (
            byte[]? PngBytes
            )
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(
                () =>
                {
                    HaulFramePresenter.ClearCache();
                    HaulFramePngBytes = PngBytes;
                    InvalidateViewerSurface?.Invoke();
                },
                Avalonia.Threading.DispatcherPriority.Render);
        }

        /// <summary>
        /// Handles scrub cache and label updates from the scrub controller.
        /// </summary>
        private void OnHaulScrubStateChanged
            (
            )
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(
                () =>
                {
                    RoutePositionLabel = _HaulScrubController.RoutePositionLabel;
                    HaulFrameCacheRevision++;
                    OnPropertyChanged(nameof(HaulFrameCacheTicks));
                    OnPropertyChanged(nameof(HaulFrameCacheRevision));
                },
                Avalonia.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// Clears haul scrub state when haul data is removed.
        /// </summary>
        private void ClearHaulSession
            (
            )
        {
            _HaulFrameProgressCts?.Cancel();
            _HaulScrubController.Clear();
            HaulFramePngBytes = null;
            HaulFramePresenter.ClearCache();
            ShowHaulRouteSection = false;
            RouteScrubMaximum = 0;
            RouteScrubIndex = 0;
            RoutePositionLabel = "0/0";
            HaulFrameCacheRevision = 0;
        }

        /// <summary>
        /// Advances the design busy overlay gradually while long-running haul work continues.
        /// </summary>
        /// <param name="StartPercent">Starting progress percent.</param>
        /// <param name="EndPercent">Maximum progress percent for this phase.</param>
        /// <param name="Message">Status message.</param>
        /// <param name="CancellationToken">Cancellation token.</param>
        /// <returns>A task that completes when the pump stops.</returns>
        private async Task RunDesignProgressPumpAsync
            (
            int StartPercent,
            int EndPercent,
            string Message,
            CancellationToken CancellationToken
            )
        {
            int Current = StartPercent;
            SetDesignBusyProgress(Current, Message);
            using PeriodicTimer Timer = new PeriodicTimer(TimeSpan.FromMilliseconds(350));
            try
            {
                while (Current < EndPercent &&
                       await Timer.WaitForNextTickAsync(CancellationToken).ConfigureAwait(true))
                {
                    Current = Math.Min(EndPercent, Current + 2);
                    SetDesignBusyProgress(Current, Message);
                }
            }
            catch (OperationCanceledException)
            {
                SetDesignBusyProgress(Math.Max(Current, StartPercent), Message);
            }
        }

        /// <summary>
        /// Shows the viewer progress overlay while a haul scrub frame is generated on demand.
        /// </summary>
        /// <param name="RouteScrubIndex">Route scrub index being rendered.</param>
        private void OnHaulForegroundGenerationStarted
            (
            int RouteScrubIndex
            )
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(
                () =>
                {
                    _HaulFrameProgressCts?.Cancel();
                    _HaulFrameProgressCts = new CancellationTokenSource();
                    string Message = $"Rendering route {RouteScrubIndex + 1}…";
                    BeginViewerBusy(Message, 10);
                    _ = RunHaulFrameProgressPumpAsync(Message, _HaulFrameProgressCts.Token);
                },
                Avalonia.Threading.DispatcherPriority.Normal);
        }

        /// <summary>
        /// Dismisses the viewer progress overlay after haul foreground frame generation completes.
        /// </summary>
        private void OnHaulForegroundGenerationCompleted
            (
            )
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(
                () =>
                {
                    _HaulFrameProgressCts?.Cancel();
                    if (!IsViewerBusy)
                        return;

                    SetViewerBusyProgress(100, "Ready");
                    EndViewerBusy();
                },
                Avalonia.Threading.DispatcherPriority.Normal);
        }

        /// <summary>
        /// Advances the viewer busy overlay while a haul scrub frame is rendered.
        /// </summary>
        /// <param name="Message">Status message.</param>
        /// <param name="CancellationToken">Cancellation token.</param>
        /// <returns>A task that completes when the pump stops.</returns>
        private async Task RunHaulFrameProgressPumpAsync
            (
            string Message,
            CancellationToken CancellationToken
            )
        {
            int Current = 10;
            try
            {
                using PeriodicTimer Timer = new PeriodicTimer(TimeSpan.FromMilliseconds(300));
                while (Current < 90 &&
                       await Timer.WaitForNextTickAsync(CancellationToken).ConfigureAwait(true))
                {
                    Current += 5;
                    SetViewerBusyProgress(Current, Message);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <summary>
        /// Waits until the scrub controller produces the first haul frame PNG.
        /// </summary>
        /// <returns>A task that completes when frame bytes are available or the wait times out.</returns>
        private Task WaitForFirstHaulFrameAsync
            (
            )
        {
            if (HaulFramePngBytes is { Length: > 0 })
                return Task.CompletedTask;

            TaskCompletionSource Tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Timer? TimeoutTimer = null;

            void OnFrame
                (
                byte[]? PngBytes
                )
            {
                if (PngBytes is not { Length: > 0 })
                    return;

                _HaulScrubController.FrameChanged -= OnFrame;
                TimeoutTimer?.Dispose();
                Tcs.TrySetResult();
            }

            _HaulScrubController.FrameChanged += OnFrame;

            if (HaulFramePngBytes is { Length: > 0 })
            {
                _HaulScrubController.FrameChanged -= OnFrame;
                return Task.CompletedTask;
            }

            TimeoutTimer = new Timer(
                _ =>
                {
                    _HaulScrubController.FrameChanged -= OnFrame;
                    Tcs.TrySetResult();
                },
                null,
                TimeSpan.FromMinutes(2),
                Timeout.InfiniteTimeSpan);

            return Tcs.Task;
        }
    }
}
