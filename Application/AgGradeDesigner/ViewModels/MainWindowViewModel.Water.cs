using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Water;
using AgGradeDesigner.Services;

using CommunityToolkit.Mvvm.Input;

namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// Water dialog, calculation, and DISPLAY toggle integration (WATER-SPEC §7).
    /// </summary>
    public sealed partial class MainWindowViewModel
    {
        /// <summary>Cancellation source for the in-flight water calculation progress pump.</summary>
        private CancellationTokenSource? _WaterCalculationProgressCts;

        /// <summary>
        /// Opens the Water dialog and runs surface water layer calculation when the user confirms.
        /// </summary>
        [RelayCommand]
        private async Task OpenWaterDialogAsync
            (
            )
        {
            WaterDialogViewModel? Result = await DialogService.ShowWaterDialogAsync(_Project.Water.Settings).ConfigureAwait(true);
            if (Result == null)
                return;

            WaterSettings PreviousSettings = _Project.Water.Settings.Clone();
            _Project.Water.Settings = Result.ToWaterSettings();
            _Project.MarkModified();

            if (!WaterLayerCalculator.CanComputeForActiveView(_Project, FieldContentView, GetActiveRouteScrubIndex()))
            {
                RefreshWaterToggleAvailability();
                return;
            }

            bool HydrologyChanged = !PreviousSettings.MatchesHydrologySettings(_Project.Water.Settings);
            bool DisplayChanged = !PreviousSettings.MatchesDisplaySettings(_Project.Water.Settings);
            bool CacheMissingOrStale = WaterLayerResolver.IsCacheMissingOrStale(
                _Project,
                FieldContentView,
                GetActiveRouteScrubIndex());

            // intent: display-only edits rebuild the overlay from stored depths when hydrology cache is still valid
            if (DisplayChanged && !HydrologyChanged && !CacheMissingOrStale)
            {
                WaterLayerOverlayRefresh.RefreshProjectCaches(_Project);
                ApplyWaterLayerAfterSettingsChange();
                return;
            }

            if (!CacheMissingOrStale)
            {
                ApplyWaterLayerAfterSettingsChange();
                return;
            }

            await RunWaterLayerCalculationAsync().ConfigureAwait(true);
        }

        /// <summary>
        /// Enables the water toggle when a layer is available and refreshes viewer decorations.
        /// </summary>
        private void ApplyWaterLayerAfterSettingsChange
            (
            )
        {
            RefreshWaterToggleAvailability();
            if (!WaterToggle && WaterToggleEnabled)
                WaterToggle = true;

            InvalidateViewerAfterWaterChange();
        }

        /// <summary>
        /// Runs surface water layer calculation asynchronously with a viewer progress overlay.
        /// </summary>
        /// <returns>A task that completes when calculation finishes or is cancelled.</returns>
        private async Task RunWaterLayerCalculationAsync
            (
            )
        {
            string Message = WaterConstants.CALCULATING_WATER_LAYER_MESSAGE;
            BeginViewerBusy(Message, WaterConstants.PROGRESS_START_PERCENT);
            _WaterCalculationProgressCts?.Cancel();
            _WaterCalculationProgressCts = new CancellationTokenSource();
            CancellationToken Token = _WaterCalculationProgressCts.Token;
            _ = RunWaterCalculationProgressPumpAsync(Message, Token);

            try
            {
                IProgress<int> Progress = CreateViewerProgressReporter(Message, WaterConstants.PROGRESS_START_PERCENT, WaterConstants.PROGRESS_COMPLETE_PERCENT);
                await WaterLayerCalculator.ComputeAsync(
                    _Project,
                    FieldContentView,
                    GetActiveRouteScrubIndex(),
                    Progress,
                    Token).ConfigureAwait(true);

                RefreshWaterToggleAvailability();
                if (WaterToggleEnabled && !WaterToggle)
                    WaterToggle = true;

                InvalidateViewerAfterWaterChange();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception Ex)
            {
                await DialogService.ShowMessageAsync("Water layer", Ex.Message).ConfigureAwait(true);
            }
            finally
            {
                _WaterCalculationProgressCts?.Cancel();
                if (IsViewerBusy)
                {
                    SetViewerBusyProgress(WaterConstants.PROGRESS_COMPLETE_PERCENT, Message);
                    EndViewerBusy();
                }
            }
        }

        /// <summary>
        /// Advances the viewer busy overlay while water calculation runs when sub-progress is sparse.
        /// </summary>
        /// <param name="Message">Status message.</param>
        /// <param name="CancellationToken">Cancellation token.</param>
        /// <returns>A task that completes when the pump stops.</returns>
        private async Task RunWaterCalculationProgressPumpAsync
            (
            string Message,
            CancellationToken CancellationToken
            )
        {
            int Current = WaterConstants.PROGRESS_DEM_READY_PERCENT;
            try
            {
                using PeriodicTimer Timer = new PeriodicTimer(TimeSpan.FromMilliseconds(350));
                while ((Current < WaterConstants.PROGRESS_OVERLAY_START_PERCENT) &&
                       await Timer.WaitForNextTickAsync(CancellationToken).ConfigureAwait(true))
                {
                    Current += 2;
                    SetViewerBusyProgress(Current, Message);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <summary>
        /// Updates Water toggle enablement for the active field content view.
        /// </summary>
        private void RefreshWaterToggleAvailability
            (
            )
        {
            bool Enabled = WaterLayerResolver.HasWaterLayerToShow(
                _Project,
                FieldContentView,
                GetActiveRouteScrubIndex());
            WaterToggleEnabled = Enabled;
            if (!Enabled && WaterToggle)
            {
                _SuppressDisplaySync = true;
                WaterToggle = false;
                _SuppressDisplaySync = false;
                SyncDisplayToProject();
            }

            OnPropertyChanged(nameof(WaterToggleEnabled));
        }

        /// <summary>
        /// Returns the active haul route scrub index, or zero when haul is inactive.
        /// </summary>
        /// <returns>Route scrub index for haul water cache lookup.</returns>
        private int GetActiveRouteScrubIndex
            (
            )
        {
            if (!IsHaulActive)
                return 0;

            return Math.Max(0, RouteScrubIndex);
        }

        /// <summary>
        /// Updates water toggle state after haul scrub or other view-context changes.
        /// Only requests a viewer refresh when overlay visibility actually changes.
        /// </summary>
        private void RefreshWaterToggleAfterViewSwitch
            (
            )
        {
            bool PreviousWater = _Project.ViewerState.Display.Water;
            RefreshWaterToggleAvailability();
            if (_Project.ViewerState.Display.Water != PreviousWater)
                InvalidateViewerAfterWaterChange();
        }

        /// <summary>
        /// Requests a decoration refresh after water overlay visibility changes.
        /// </summary>
        private void InvalidateViewerAfterWaterChange
            (
            )
        {
            // intent: rebuild overlays via the display-toggle path; never invalidate the elevation surface
            OnPropertyChanged(nameof(Display));
        }
    }
}
