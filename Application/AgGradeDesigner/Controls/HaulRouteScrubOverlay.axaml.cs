using System.ComponentModel;

using AgGradeDesigner.Diagnostics;
using AgGradeDesigner.ViewModels;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace AgGradeDesigner.Controls
{
    /// <summary>
    /// Vertical route scrub overlay shown inside the field viewer during Haul view (HAUL-SPEC §7.4.10).
    /// </summary>
    public partial class HaulRouteScrubOverlay : UserControl
    {
        /// <summary>Grey tick colour for uncached frames (§7.4.27).</summary>
        private static readonly IBrush UncachedTickBrush = new SolidColorBrush(Color.Parse("#A8A8A8"));

        /// <summary>Green tick colour for cached frames (§7.4.28).</summary>
        private static readonly IBrush CachedTickBrush = new SolidColorBrush(Color.Parse("#2E7D32"));

        /// <summary>Bound view model when haul route scrub is active.</summary>
        private MainWindowViewModel? _ViewModel;

        /// <summary>Prevents user-change handlers while the slider is synced from the view model.</summary>
        private bool _SyncingFromViewModel;

        /// <summary>Whether a debounced tick-mark redraw is already queued.</summary>
        private bool _TickRedrawScheduled;

        /// <summary>Canvas height used for the last tick-mark paint.</summary>
        private double _LastTickCanvasHeight = -1;

        /// <summary>Route step count used for the last tick-mark paint.</summary>
        private int _LastTickStepCount = -1;

        /// <summary>Cached frame count used for the last tick-mark paint.</summary>
        private int _LastTickCachedCount = -1;

        /// <summary>Fingerprint of cached tick states used for the last tick-mark paint.</summary>
        private int _LastTickCacheFingerprint = -1;

        /// <summary>
        /// Whether a follow-up tick-mark redraw is needed after the current one runs.
        /// </summary>
        private bool _TickRedrawPending;

        /// <summary>
        /// Initializes the haul route scrub overlay control.
        /// </summary>
        public HaulRouteScrubOverlay
            (
            )
        {
            InitializeComponent();
            TickCanvas.SizeChanged += (_, _) => ScheduleRedrawTickMarks();
            DataContextChanged += OnDataContextChanged;
            PropertyChanged += OnOverlayPropertyChanged;
            WirePointerDiagnostics();
        }

        /// <summary>
        /// Returns true when the supplied point in overlay coordinates lies over the scrub panel chrome.
        /// </summary>
        /// <param name="PointInOverlay">Pointer position relative to this overlay control.</param>
        public bool IsPointOverScrubPanel
            (
            Point PointInOverlay
            )
        {
            if (!IsVisible)
                return false;

            Rect PanelBounds = Bounds;
            if (PanelBounds.Width <= 0 || PanelBounds.Height <= 0)
                PanelBounds = ScrubPanelBorder.Bounds;

            return PanelBounds.Width > 0 &&
                   PanelBounds.Height > 0 &&
                   PanelBounds.Contains(PointInOverlay);
        }

        /// <summary>
        /// Describes the scrub panel bounds for diagnostic logging.
        /// </summary>
        public string DescribeScrubPanelBounds
            (
            )
        {
            Rect PanelBounds = Bounds.Width > 0 ? Bounds : ScrubPanelBorder.Bounds;
            return $"panel=({PanelBounds.X:0},{PanelBounds.Y:0},{PanelBounds.Width:0}x{PanelBounds.Height:0}) " +
                   $"overlay=({Bounds.Width:0}x{Bounds.Height:0}) visible={IsVisible} hit={IsHitTestVisible}";
        }

        /// <summary>
        /// Registers pointer handlers on the scrub panel and slider for debug logging.
        /// </summary>
        private void WirePointerDiagnostics
            (
            )
        {
            ScrubPanelBorder.AddHandler(
                PointerPressedEvent,
                OnScrubPanelPointerPressed,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
                handledEventsToo: true);
            ScrubPanelBorder.AddHandler(
                PointerReleasedEvent,
                OnScrubPanelPointerReleased,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
                handledEventsToo: true);

            RouteScrubSlider.AddHandler(
                PointerPressedEvent,
                OnSliderPointerPressed,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
                handledEventsToo: true);
            RouteScrubSlider.AddHandler(
                PointerMovedEvent,
                OnSliderPointerMoved,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
                handledEventsToo: true);
            RouteScrubSlider.AddHandler(
                PointerReleasedEvent,
                OnSliderPointerReleased,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
                handledEventsToo: true);
            RouteScrubSlider.AddHandler(
                PointerCaptureLostEvent,
                OnSliderPointerCaptureLost,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
                handledEventsToo: true);
        }

        /// <summary>
        /// Logs scrub panel pointer presses and ensures the slider keeps capture during drags.
        /// </summary>
        private void OnScrubPanelPointerPressed
            (
            object? Sender,
            PointerPressedEventArgs Args
            )
        {
            Point OverlayPoint = Args.GetPosition(this);
            Point PanelPoint = Args.GetPosition(ScrubPanelBorder);
            LogPointerEvent(
                "Panel.Pressed",
                OverlayPoint,
                Args,
                $"panel=({PanelPoint.X:0},{PanelPoint.Y:0}) {DescribeScrubPanelBounds}");

            if (ReferenceEquals(Args.Source, RouteScrubSlider) ||
                ReferenceEquals(Args.Source, ScrubPanelBorder))
            {
                return;
            }

            RouteScrubSlider.Focus();
            if (Args.Pointer.Captured == null)
                Args.Pointer.Capture(RouteScrubSlider);
        }

        /// <summary>
        /// Logs scrub panel pointer releases.
        /// </summary>
        private void OnScrubPanelPointerReleased
            (
            object? Sender,
            PointerReleasedEventArgs Args
            )
        {
            Point OverlayPoint = Args.GetPosition(this);
            LogPointerEvent("Panel.Released", OverlayPoint, Args);
            CommitScrubFromViewModel();
        }

        /// <summary>
        /// Logs slider pointer presses and captures the pointer for thumb/track drags.
        /// </summary>
        private void OnSliderPointerPressed
            (
            object? Sender,
            PointerPressedEventArgs Args
            )
        {
            Point OverlayPoint = Args.GetPosition(this);
            Point SliderPoint = Args.GetPosition(RouteScrubSlider);
            LogPointerEvent(
                "Slider.Pressed",
                OverlayPoint,
                Args,
                $"slider=({SliderPoint.X:0},{SliderPoint.Y:0}) value={RouteScrubSlider.Value:0}/{RouteScrubSlider.Maximum:0}");

            RouteScrubSlider.Focus();
            if (Args.Pointer.Captured == null)
                Args.Pointer.Capture(RouteScrubSlider);
        }

        /// <summary>
        /// Logs throttled slider pointer moves during drags.
        /// </summary>
        private void OnSliderPointerMoved
            (
            object? Sender,
            PointerEventArgs Args
            )
        {
            if (!ImportDiagnosticsLog.ShouldLog())
                return;

            if (!Args.GetCurrentPoint(RouteScrubSlider).Properties.IsLeftButtonPressed)
                return;

            Point OverlayPoint = Args.GetPosition(this);
            ImportDiagnosticsLog.WriteHaulScrubPointer(
                "Slider.Move",
                OverlayPoint.X,
                OverlayPoint.Y,
                $"value={RouteScrubSlider.Value:0} capture={DescribeCapture(Args.Pointer)} handled={Args.Handled}");
        }

        /// <summary>
        /// Logs slider pointer releases.
        /// </summary>
        private void OnSliderPointerReleased
            (
            object? Sender,
            PointerReleasedEventArgs Args
            )
        {
            Point OverlayPoint = Args.GetPosition(this);
            LogPointerEvent(
                "Slider.Released",
                OverlayPoint,
                Args,
                $"value={RouteScrubSlider.Value:0} capture={DescribeCapture(Args.Pointer)}");
        }

        /// <summary>
        /// Logs unexpected pointer capture loss on the scrub slider.
        /// </summary>
        private void OnSliderPointerCaptureLost
            (
            object? Sender,
            PointerCaptureLostEventArgs Args
            )
        {
            ImportDiagnosticsLog.WriteHaulScrub(
                "Slider.CaptureLost",
                $"value={RouteScrubSlider.Value:0}");
            CommitScrubFromViewModel();
        }

        /// <summary>
        /// Commits the scrub index when the user releases the route slider.
        /// </summary>
        /// <param name="Sender">Route scrub slider.</param>
        /// <param name="Args">Pointer release arguments.</param>
        private void OnRouteScrubPointerReleased
            (
            object? Sender,
            PointerReleasedEventArgs Args
            )
        {
            Point OverlayPoint = Args.GetPosition(this);
            LogPointerEvent(
                "Slider.ReleasedHandler",
                OverlayPoint,
                Args,
                $"value={RouteScrubSlider.Value:0}");

            CommitScrubFromViewModel();
        }

        /// <summary>
        /// Commits the live scrub index and resumes background prefetch.
        /// </summary>
        private void CommitScrubFromViewModel
            (
            )
        {
            if (DataContext is MainWindowViewModel ViewModel)
                ViewModel.CommitRouteScrubFromSlider();
        }

        /// <summary>
        /// Forwards user slider changes to the haul scrub controller.
        /// </summary>
        /// <param name="Sender">Route scrub slider.</param>
        /// <param name="Args">Value change arguments.</param>
        private void OnRouteScrubValueChanged
            (
            object? Sender,
            RangeBaseValueChangedEventArgs Args
            )
        {
            if (_SyncingFromViewModel)
            {
                ImportDiagnosticsLog.WriteHaulScrub(
                    "Slider.ValueChanged",
                    $"ignored sync old={Args.OldValue:0} new={Args.NewValue:0}");
                return;
            }

            if (DataContext is not MainWindowViewModel ViewModel)
                return;

            ImportDiagnosticsLog.WriteHaulScrub(
                "Slider.ValueChanged",
                $"user old={Args.OldValue:0} new={Args.NewValue:0}");
            ViewModel.HandleRouteScrubSliderChanged((int)Args.NewValue);
        }

        /// <summary>
        /// Subscribes to view-model tick-mark updates.
        /// </summary>
        /// <param name="Sender">Overlay control.</param>
        /// <param name="Args">Data context change arguments.</param>
        private void OnDataContextChanged
            (
            object? Sender,
            EventArgs Args
            )
        {
            if (_ViewModel != null)
                _ViewModel.PropertyChanged -= OnViewModelPropertyChanged;

            _ViewModel = DataContext as MainWindowViewModel;
            if (_ViewModel != null)
                _ViewModel.PropertyChanged += OnViewModelPropertyChanged;

            SyncSliderFromViewModel();
            ScheduleRedrawTickMarks();
        }

        /// <summary>
        /// Syncs the slider thumb when bound scrub index changes.
        /// </summary>
        /// <param name="Sender">Overlay control.</param>
        /// <param name="Args">Property change arguments.</param>
        private void OnOverlayPropertyChanged
            (
            object? Sender,
            AvaloniaPropertyChangedEventArgs Args
            )
        {
            if (Args.Property != DataContextProperty)
                return;

            SyncSliderFromViewModel();
        }

        /// <summary>
        /// Redraws frame cache tick marks when scrub cache state changes.
        /// </summary>
        /// <param name="Sender">View model.</param>
        /// <param name="Args">Property change arguments.</param>
        private void OnViewModelPropertyChanged
            (
            object? Sender,
            PropertyChangedEventArgs Args
            )
        {
            if (Args.PropertyName is nameof(MainWindowViewModel.RouteScrubIndex) or
                nameof(MainWindowViewModel.RouteScrubMaximum) or
                nameof(MainWindowViewModel.ShowHaulRouteSection))
            {
                SyncSliderFromViewModel();
            }

            if (Args.PropertyName is nameof(MainWindowViewModel.HaulFrameCacheTicks) or
                nameof(MainWindowViewModel.HaulFrameCacheRevision) or
                nameof(MainWindowViewModel.RouteScrubMaximum) or
                nameof(MainWindowViewModel.ShowHaulRouteSection))
            {
                _LastTickCacheFingerprint = -1;
                _LastTickCachedCount = -1;
                ScheduleRedrawTickMarks();
            }
        }

        /// <summary>
        /// Pushes the current view-model scrub index to the slider without triggering drag handling.
        /// </summary>
        private void SyncSliderFromViewModel
            (
            )
        {
            if (_ViewModel == null || !_ViewModel.ShowHaulRouteSection)
                return;

            int Maximum = _ViewModel.RouteScrubMaximum;
            int Value = Math.Clamp(_ViewModel.RouteScrubIndex, 0, Math.Max(0, Maximum));

            _SyncingFromViewModel = true;
            try
            {
                RouteScrubSlider.Maximum = Maximum;
                RouteScrubSlider.Value = Value;
            }
            finally
            {
                _SyncingFromViewModel = false;
            }
        }

        /// <summary>
        /// Coalesces tick-mark redraw requests so prefetch updates cannot flood the UI thread.
        /// </summary>
        private void ScheduleRedrawTickMarks
            (
            )
        {
            if (_TickRedrawScheduled)
            {
                _TickRedrawPending = true;
                return;
            }

            _TickRedrawScheduled = true;
            Dispatcher.UIThread.Post(
                () =>
                {
                    _TickRedrawScheduled = false;
                    RedrawTickMarks();
                    if (!_TickRedrawPending)
                        return;

                    _TickRedrawPending = false;
                    ScheduleRedrawTickMarks();
                },
                DispatcherPriority.Background);
        }

        /// <summary>
        /// Paints horizontal frame cache tick marks beside the scrub track (§7.4.27).
        /// </summary>
        private void RedrawTickMarks
            (
            )
        {
            if (_ViewModel == null || !_ViewModel.ShowHaulRouteSection)
            {
                TickCanvas.Children.Clear();
                return;
            }

            IReadOnlyList<bool> Ticks = _ViewModel.HaulFrameCacheTicks;
            int StepCount = Ticks.Count;
            if (StepCount <= 0)
            {
                TickCanvas.Children.Clear();
                return;
            }

            double Height = TickCanvas.Bounds.Height;
            if (Height <= 0)
                return;

            int CachedCount = 0;
            int Fingerprint = StepCount;
            for (int Index = 0; Index < StepCount; Index++)
            {
                if (!Ticks[Index])
                    continue;

                CachedCount++;
                Fingerprint ^= (Index + 1) * 397;
            }

            if (Math.Abs(Height - _LastTickCanvasHeight) < 0.5 &&
                StepCount == _LastTickStepCount &&
                CachedCount == _LastTickCachedCount &&
                Fingerprint == _LastTickCacheFingerprint)
            {
                return;
            }

            _LastTickCanvasHeight = Height;
            _LastTickStepCount = StepCount;
            _LastTickCachedCount = CachedCount;
            _LastTickCacheFingerprint = Fingerprint;
            TickCanvas.Children.Clear();

            const double BarCenterX = 7;
            TickCanvas.Children.Add(new Line
            {
                StartPoint = new Point(BarCenterX, 0),
                EndPoint = new Point(BarCenterX, Height),
                Stroke = UncachedTickBrush,
                StrokeThickness = 4
            });

            int TickLineCount = Math.Min(StepCount, Math.Max(1, (int)Math.Floor(Height)));
            for (int LineIndex = 0; LineIndex < TickLineCount; LineIndex++)
            {
                int StartIndex = (int)Math.Floor((double)LineIndex / TickLineCount * StepCount);
                int EndIndex = (int)Math.Floor((double)(LineIndex + 1) / TickLineCount * StepCount);
                if (EndIndex <= StartIndex)
                    EndIndex = Math.Min(StepCount, StartIndex + 1);

                bool Cached = false;
                for (int Index = StartIndex; Index < EndIndex; Index++)
                {
                    if (Ticks[Index])
                    {
                        Cached = true;
                        break;
                    }
                }

                double Y = Height - (((LineIndex + 0.5) / TickLineCount) * Height);
                TickCanvas.Children.Add(new Line
                {
                    StartPoint = new Point(0, Y),
                    EndPoint = new Point(14, Y),
                    Stroke = Cached ? CachedTickBrush : UncachedTickBrush,
                    StrokeThickness = Cached ? 2.0 : 1.0
                });
            }
        }

        /// <summary>
        /// Writes one haul scrub pointer diagnostic line when debug logging is enabled.
        /// </summary>
        private void LogPointerEvent
            (
            string Phase,
            Point OverlayPoint,
            RoutedEventArgs Args,
            string? Extra = null
            )
        {
            if (!ImportDiagnosticsLog.ShouldLog())
                return;

            string Detail = string.Join(
                " ",
                new[]
                {
                    Extra,
                    $"source={DescribeInputSource(Args.Source)}",
                    $"handled={Args.Handled}",
                    Args is PointerEventArgs PointerArgs ? $"capture={DescribeCapture(PointerArgs.Pointer)}" : null
                }.Where(Part => !string.IsNullOrEmpty(Part)));

            ImportDiagnosticsLog.WriteHaulScrubPointer(Phase, OverlayPoint.X, OverlayPoint.Y, Detail);
        }

        /// <summary>
        /// Describes an input event source for diagnostic logging.
        /// </summary>
        private static string DescribeInputSource
            (
            object? Source
            )
        {
            if (Source is not Control Control)
                return Source?.GetType().Name ?? "null";

            string Name = string.IsNullOrEmpty(Control.Name) ? string.Empty : $"#{Control.Name}";
            return $"{Control.GetType().Name}{Name}";
        }

        /// <summary>
        /// Describes the current pointer capture target for diagnostic logging.
        /// </summary>
        private static string DescribeCapture
            (
            IPointer Pointer
            )
        {
            if (Pointer.Captured is not Control Captured)
                return Pointer.Captured?.GetType().Name ?? "none";

            string Name = string.IsNullOrEmpty(Captured.Name) ? string.Empty : $"#{Captured.Name}";
            return $"{Captured.GetType().Name}{Name}";
        }
    }
}
