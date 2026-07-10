using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Orchestrates haul scrub frame generation, caching, and background prefetch (§4.7, §7.4.15–§7.4.25).
    /// </summary>
    public sealed class HaulScrubController : IDisposable
    {
        /// <summary>Minimum interval between partial route sprites during active scrubbing.</summary>
        private const int ScrubPartialIntervalMs = 50;

        /// <summary>Milliseconds to yield between prefetch frames so the UI stays responsive (§7.4.23).</summary>
        private const int PrefetchYieldMs = 75;

        /// <summary>Frames to generate before a longer idle pause.</summary>
        private const int PrefetchBurstFrameCount = 2;

        /// <summary>Longer pause after each prefetch burst to keep the machine responsive.</summary>
        private const int PrefetchBurstPauseMs = 300;

        /// <summary>Pause before restarting prefetch when work remains.</summary>
        private const int PrefetchBetweenBatchMs = 400;

        /// <summary>Minimum interval between prefetch tick-mark UI updates.</summary>
        private const int PrefetchStateRaiseIntervalMs = 500;

        /// <summary>Prefetch frames to batch before tick-mark UI updates.</summary>
        private const int PrefetchStateRaiseFrameBatch = 16;

        /// <summary>Active haul data being scrubbed.</summary>
        private HaulData? _Data;

        /// <summary>Parsed AGD field rebuilt from the open project.</summary>
        private HaulBuilder.AgdField? _Field;

        /// <summary>Session frame cache.</summary>
        private readonly HaulScrubFrameCache _Cache = new HaulScrubFrameCache();

        /// <summary>Foreground generation cancellation.</summary>
        private CancellationTokenSource? _ForegroundCts;

        /// <summary>Background prefetch cancellation.</summary>
        private CancellationTokenSource? _PrefetchCts;

        /// <summary>Partial route sprite generation cancellation during slider drag.</summary>
        private CancellationTokenSource? _PartialFrameCts;

        /// <summary>Whether the partial-frame pump worker is running.</summary>
        private volatile bool _PartialPumpRunning;

        /// <summary>Route scrub index last shown as a partial sprite during scrubbing.</summary>
        private int _LastDisplayedPartialIndex = -1;

        /// <summary>Timestamp of the last partial route sprite displayed during scrubbing.</summary>
        private long _LastPartialDisplayTicks;

        /// <summary>Latest live route scrub index during drag.</summary>
        private int _LiveRouteScrubIndex;

        /// <summary>Whether the user is interacting with the route scrub slider.</summary>
        private bool _SliderInteractionActive;

        /// <summary>Whether initial sequential prefetch has finished (§7.4.24).</summary>
        private bool _InitialPrefetchComplete;

        /// <summary>Next index to resume when §7.4.24 sequential prefetch continues after relocation (§7.4.17).</summary>
        private int _DeferredSequentialNextIndex = 1;

        /// <summary>Progress cursor updated as sequential prefetch caches frames.</summary>
        private int _SequentialPrefetchCursor = 1;

        /// <summary>When true, prefetch bidirectionally from <see cref="_BidirectionalAnchorIndex"/> before resuming sequential.</summary>
        private bool _PreferBidirectionalFromCurrent;

        /// <summary>Route scrub index around which relocated bidirectional prefetch runs.</summary>
        private int _BidirectionalAnchorIndex;

        /// <summary>Whether foreground frame generation is in progress (§7.4.19).</summary>
        private volatile bool _ForegroundGenerationActive;

        /// <summary>When true, all haul scrub background workers stay paused while haul 3D is active.</summary>
        private bool _SuspendedForThreeDView;

        /// <summary>Prefetch frames generated since the last UI tick-mark refresh.</summary>
        private int _PrefetchFramesSinceStateRaise;

        /// <summary>Timestamp of the last prefetch tick-mark UI refresh.</summary>
        private long _LastPrefetchStateRaiseTicks;

        /// <summary>Lock guarding mutable controller state.</summary>
        private readonly object _Lock = new object();

        /// <summary>Number of discrete route scrub steps.</summary>
        public int RouteStepCount { get; private set; }

        /// <summary>Current committed route scrub index.</summary>
        public int RouteScrubIndex { get; private set; }

        /// <summary>
        /// Route scrub index currently shown in the UI (live index while the slider is dragged).
        /// </summary>
        public int DisplayedRouteScrubIndex => GetDisplayedRouteScrubIndex();

        /// <summary>Read-only route position label in <c>{current}/{total}</c> form.</summary>
        public string RoutePositionLabel =>
            RouteStepCount <= 0
                ? "0/0"
                : $"{(GetDisplayedRouteScrubIndex() + 1)}/{RouteStepCount}";

        /// <summary>Session frame cache exposed for tick-mark queries.</summary>
        public HaulScrubFrameCache Cache => _Cache;

        /// <summary>Raised when the displayed haul frame changes on the UI thread.</summary>
        public event Action<HaulScrubFrame?>? FrameChanged;

        /// <summary>Raised when cache or scrub index state changes on the UI thread.</summary>
        public event Action? StateChanged;

        /// <summary>Raised when foreground frame generation starts for a route scrub index.</summary>
        public event Action<int>? ForegroundGenerationStarted;

        /// <summary>Raised when foreground frame generation completes or is cancelled.</summary>
        public event Action? ForegroundGenerationCompleted;

        /// <summary>Optional sink for prefetch diagnostic lines (wired from the UI layer).</summary>
        public Action<string, string>? PrefetchDiagnosticSink { get; set; }

        /// <summary>Optional sink for scrub drag / partial / complete frame diagnostics (wired from the UI layer).</summary>
        public Action<string, string>? ScrubDiagnosticSink { get; set; }

        /// <summary>
        /// Attaches haul data and rebuilds the AGD field from the open project on a background thread.
        /// </summary>
        /// <param name="Project">Project containing survey and design.</param>
        /// <param name="Data">Haul data to scrub.</param>
        /// <param name="CancellationToken">Cancellation token.</param>
        /// <returns>A task that completes when the initial frame commit has started.</returns>
        public async Task AttachAsync
            (
            ProjectDocument Project,
            HaulData Data,
            CancellationToken CancellationToken = default
            )
        {
            lock (_Lock)
            {
                ResetInternal();
                _Data = Data;
                RouteStepCount = Data.RouteStepCount;
                RouteScrubIndex = 0;
                _LiveRouteScrubIndex = 0;
            }

            HaulBuilder.AgdField Field = await Task.Run(
                () =>
                {
                    HaulBuilder.AgdField ParsedField = BuildAgdField(Project);
                    HaulBuilder.Grid Grid = Data.ResolveGrid(ParsedField);
                    Data.FieldBoundaryPngBytes = HaulScrubFrameBuilder.BuildFieldBoundaryPng(
                        Project,
                        Data,
                        ParsedField);
                    Data.FieldBoundaryBounds = HaulScrubFrameBuilder.ComputeGridBounds(Grid);

                    return ParsedField;
                },
                CancellationToken).ConfigureAwait(false);

            lock (_Lock)
            {
                if (_Data != Data)
                    return;

                _Field = Field;
            }

            CommitFrame(RouteScrubIndex, Immediate: true);
        }

        /// <summary>
        /// Attaches haul data synchronously for callers that already prepared the AGD field off-thread.
        /// </summary>
        /// <param name="Project">Project containing survey and design.</param>
        /// <param name="Data">Haul data to scrub.</param>
        public void Attach
            (
            ProjectDocument Project,
            HaulData Data
            )
        {
            _ = AttachAsync(Project, Data);
        }

        /// <summary>
        /// Clears haul scrub state when haul data is removed or replaced.
        /// </summary>
        public void Clear
            (
            )
        {
            lock (_Lock)
            {
                ResetInternal();
                RouteStepCount = 0;
                RouteScrubIndex = 0;
                _LiveRouteScrubIndex = 0;
            }

            RaiseFrameChanged(null);
            RaiseStateChanged();
        }

        /// <summary>
        /// Returns whether the controller is already attached to the supplied haul data.
        /// </summary>
        /// <param name="Data">Haul data to compare.</param>
        /// <returns>True when session state is ready to resume without re-attaching.</returns>
        public bool IsAttachedTo
            (
            HaulData? Data
            )
        {
            if (Data == null)
                return false;

            lock (_Lock)
            {
                return ReferenceEquals(_Data, Data) && _Field != null && RouteStepCount > 0;
            }
        }

        /// <summary>
        /// Pauses scrub workers when leaving the haul field content view without discarding cached frames.
        /// </summary>
        public void SuspendForFieldViewSwitch
            (
            )
        {
            lock (_Lock)
                _SliderInteractionActive = false;

            StopPartialFrameGeneration();
            CancelForegroundCompleteGeneration();
            StopPrefetchWorker();
            LogScrub("Suspend", $"index={RouteScrubIndex} cached={_Cache.CachedFrameCount}");
        }

        /// <summary>
        /// Pauses prefetch, partial sprites, and foreground generation while haul 3D is active.
        /// </summary>
        public void SuspendForThreeDView
            (
            )
        {
            lock (_Lock)
            {
                _SuspendedForThreeDView = true;
                _SliderInteractionActive = false;
            }

            StopPartialFrameGeneration();
            CancelForegroundCompleteGeneration();
            StopPrefetchWorker();
            LogScrub("Suspend3D", $"index={RouteScrubIndex} cached={_Cache.CachedFrameCount}");
        }

        /// <summary>
        /// Resumes background prefetch after returning from haul 3D to haul 2D.
        /// </summary>
        public void ResumeAfterTwoDView
            (
            )
        {
            lock (_Lock)
            {
                if (!_SuspendedForThreeDView)
                    return;

                _SuspendedForThreeDView = false;
                if (_Data == null || _Field == null || RouteStepCount <= 0)
                    return;
            }

            LogScrub("Resume2D", $"index={RouteScrubIndex} cached={_Cache.CachedFrameCount}");
            MaybeStartPrefetch();
        }

        /// <summary>
        /// Resumes scrub display and prefetch after returning to the haul field content view.
        /// </summary>
        public void ResumeAfterFieldViewSwitch
            (
            )
        {
            int Index;
            lock (_Lock)
            {
                if (_Data == null || _Field == null || RouteStepCount <= 0)
                    return;

                _SliderInteractionActive = false;
                int Maximum = Math.Max(0, RouteStepCount - 1);
                RouteScrubIndex = Math.Clamp(RouteScrubIndex, 0, Maximum);
                _LiveRouteScrubIndex = RouteScrubIndex;
                Index = RouteScrubIndex;
            }

            LogScrub("Resume", $"index={Index} cached={_Cache.CachedFrameCount}");
            CommitFrame(Index, Immediate: true);
            RaiseStateChanged();
        }

        /// <summary>
        /// Rebuilds the session field-boundary PNG using the attached haul grid.
        /// </summary>
        /// <param name="Project">Active project supplying survey context.</param>
        public void RefreshFieldBoundary
            (
            ProjectDocument Project
            )
        {
            lock (_Lock)
            {
                if (_Data == null || _Field == null)
                    return;

                HaulBuilder.Grid Grid = _Data.ResolveGrid(_Field);
                _Data.FieldBoundaryPngBytes = HaulScrubFrameBuilder.BuildFieldBoundaryPng(
                    Project,
                    _Data,
                    _Field);
                _Data.FieldBoundaryBounds = HaulScrubFrameBuilder.ComputeGridBounds(Grid);
            }
        }

        /// <summary>
        /// Updates the live route scrub index while the slider is dragged (§7.4.12).
        /// </summary>
        /// <param name="RouteScrubIndex">New live index.</param>
        public void NotifySliderDrag
            (
            int RouteScrubIndex
            )
        {
            if (IsSuspendedForThreeDView())
                return;

            lock (_Lock)
            {
                _SliderInteractionActive = true;
                _LiveRouteScrubIndex = Math.Clamp(RouteScrubIndex, 0, Math.Max(0, RouteStepCount - 1));
            }

            LogScrub("Drag", $"index={RouteScrubIndex} live={_LiveRouteScrubIndex}");
            CancelForegroundCompleteGeneration();
            RequestPartialFrameDuringScrub();
            RaiseStateChanged();
        }

        /// <summary>
        /// Cancels in-flight complete frame generation and dismisses its progress overlay during scrubbing.
        /// </summary>
        private void CancelForegroundCompleteGeneration
            (
            )
        {
            if (!_ForegroundGenerationActive)
                return;

            LogScrub("CompleteCancel", "reason=scrubDrag");
            _ForegroundCts?.Cancel();
            _ForegroundGenerationActive = false;
            RaiseForegroundGenerationCompleted();
        }

        /// <summary>
        /// Ends slider interaction and commits the live route scrub index (§7.4.17).
        /// </summary>
        public void NotifySliderReleased
            (
            )
        {
            EndSliderInteraction();
        }

        /// <summary>
        /// Commits the live scrub index and resumes prefetch after slider interaction ends.
        /// </summary>
        private void EndSliderInteraction
            (
            )
        {
            int Index;
            lock (_Lock)
            {
                if (!_SliderInteractionActive)
                {
                    LogPrefetch("SliderEndSkipped", "reason=notActive");
                    return;
                }

                _SliderInteractionActive = false;
                Index = _LiveRouteScrubIndex;
                RouteScrubIndex = Index;
            }

            LogScrub("Release", $"index={Index} cached={_Cache.IsCached(Index)} totalCached={_Cache.CachedFrameCount}");
            LogPrefetch("SliderEnd", $"index={Index} cached={_Cache.CachedFrameCount}");
            RelocatePrefetchToCurrentIndex("sliderRelease", Index);
            StopPartialFrameGeneration();
            CommitFrame(Index, Immediate: true);
            RaiseStateChanged();
        }

        /// <summary>
        /// Steps the route scrub index from the keyboard and commits immediately (§7.4.32).
        /// </summary>
        /// <param name="Delta">Frame delta; negative moves earlier, positive moves later.</param>
        public void StepRouteScrubIndex
            (
            int Delta
            )
        {
            if (IsSuspendedForThreeDView())
                return;

            int NewIndex;
            lock (_Lock)
            {
                _SliderInteractionActive = false;
                NewIndex = Math.Clamp(RouteScrubIndex + Delta, 0, Math.Max(0, RouteStepCount - 1));
                RouteScrubIndex = NewIndex;
                _LiveRouteScrubIndex = NewIndex;
            }

            StopPartialFrameGeneration();
            RelocatePrefetchToCurrentIndex("keyboardStep", NewIndex);
            LogPrefetch("KeyboardStep", $"index={NewIndex} delta={Delta} cached={_Cache.CachedFrameCount}");
            CommitFrame(NewIndex, Immediate: true);
            RaiseStateChanged();
        }

        /// <summary>
        /// Releases background worker resources.
        /// </summary>
        public void Dispose
            (
            )
        {
            StopPartialFrameGeneration();
            _ForegroundCts?.Cancel();
            _ForegroundCts?.Dispose();
            _PrefetchCts?.Cancel();
            _PrefetchCts?.Dispose();
        }

        /// <summary>
        /// Builds a parsed AGD field from the current project for grid reconstruction.
        /// </summary>
        private static HaulBuilder.AgdField BuildAgdField
            (
            ProjectDocument Project
            )
        {
            if (!Project.HasSurvey || !Project.HasDesign || Project.Survey == null || Project.DesignGrid == null)
                throw new InvalidOperationException("Survey and design are required for haul scrub rendering.");

            string TempAgd = Path.Combine(Path.GetTempPath(), $"aggrade-haul-scrub-{Guid.NewGuid():N}.agd");
            try
            {
                AgdService.Export(
                    TempAgd,
                    Project.Survey,
                    Project.DesignGrid,
                    Project.DesignParameters ?? new DesignParameters());
                return HaulBuilder.Agd.Parse(TempAgd);
            }
            finally
            {
                if (File.Exists(TempAgd))
                    File.Delete(TempAgd);
            }
        }

        /// <summary>
        /// Commits and displays the frame for the supplied route scrub index.
        /// </summary>
        private void CommitFrame
            (
            int Index,
            bool Immediate
            )
        {
            HaulData? Data;
            HaulBuilder.AgdField? Field;
            lock (_Lock)
            {
                Data = _Data;
                Field = _Field;
                RouteScrubIndex = Index;
                _LiveRouteScrubIndex = Index;
            }

            if (Data == null || Field == null || RouteStepCount <= 0)
            {
                RaiseFrameChanged(null);
                return;
            }

            if (IsSuspendedForThreeDView())
            {
                if (_Cache.TryGetFrame(Index, out HaulScrubFrame? CachedWhileSuspended))
                    RaiseFrameChanged(CachedWhileSuspended);

                return;
            }

            if (_Cache.TryGetFrame(Index, out HaulScrubFrame? Cached))
            {
                LogScrub("CompleteCacheHit", $"index={Index} pngKB={Cached!.PngBytes.Length / 1024}");
                RaiseFrameChanged(Cached);
                MaybeStartPrefetch();
                return;
            }

            StopPartialFrameGeneration();
            StopPrefetchWorker();
            _ForegroundGenerationActive = true;
            RaiseForegroundGenerationStarted(Index);
            _ForegroundCts?.Cancel();
            _ForegroundCts?.Dispose();
            _ForegroundCts = new CancellationTokenSource();
            CancellationToken Token = _ForegroundCts.Token;
            int TargetIndex = Index;
            LogScrub("CompleteGenStart", $"index={TargetIndex}");
            long ScheduledAt = Environment.TickCount64;

            Task.Run(
                () =>
                {
                    long QueueMs = Environment.TickCount64 - ScheduledAt;
                    long BuildStarted = Environment.TickCount64;
                    try
                    {
                        Thread.CurrentThread.Priority = ThreadPriority.Normal;
                        HaulScrubFrame Frame = HaulScrubFrameBuilder.BuildCompleteFrame(Data, Field, TargetIndex);
                        long BuildMs = Environment.TickCount64 - BuildStarted;
                        if (Token.IsCancellationRequested)
                        {
                            LogScrub(
                                "CompleteGenCancel",
                                $"index={TargetIndex} queueMs={QueueMs} buildMs={BuildMs}");
                            _Cache.StorePartialState(new HaulPartialFrameState { RouteScrubIndex = TargetIndex });
                            return;
                        }

                        _Cache.StoreFrame(Frame);
                        LogScrub(
                            "CompleteGenDone",
                            $"index={TargetIndex} queueMs={QueueMs} buildMs={BuildMs} pngKB={Frame.PngBytes.Length / 1024}");
                        RaiseFrameChanged(Frame);
                        RaiseStateChanged();
                    }
                    catch
                    {
                        // intent: foreground failures leave the prior frame visible until retry
                    }
                    finally
                    {
                        _ForegroundGenerationActive = false;
                        RaiseForegroundGenerationCompleted();
                        MaybeStartPrefetch();
                    }
                },
                Token);
        }

        /// <summary>
        /// Starts or continues background prefetch when idle (§7.4.23–§7.4.25).
        /// </summary>
        private void MaybeStartPrefetch
            (
            )
        {
            if (TryGetPrefetchPauseReason(out string PauseReason))
            {
                LogPrefetch("StartBlocked", PauseReason);
                return;
            }

            StopPrefetchWorker();
            _PrefetchCts = new CancellationTokenSource();
            CancellationToken Token = _PrefetchCts.Token;
            HaulData Data = _Data!;
            HaulBuilder.AgdField Field = _Field!;
            int CurrentIndex = RouteScrubIndex;
            int StepCount = RouteStepCount;

            RunLowPriorityBackgroundWorker(
                Token,
                () =>
                {
                    try
                    {
                        (List<int> Pending, string Mode) = BuildActivePrefetchQueue(CurrentIndex, StepCount);

                        string Head = Pending.Count > 0
                            ? string.Join(",", Pending.Take(5))
                            : "none";
                        LogPrefetch(
                            "WorkerStart",
                            $"index={CurrentIndex} mode={Mode} pending={Pending.Count} cached={_Cache.CachedFrameCount} head={Head}");

                        if (Pending.Count == 0)
                        {
                            MarkInitialSequentialPrefetchCompleteIfDone(StepCount);
                            MaybeRaisePrefetchStateChanged(Force: true);
                            LogPrefetch("WorkerDone", "reason=allCached");
                            return;
                        }

                        int BuiltCount = 0;
                        int FramesSinceBurstPause = 0;
                        bool SequentialMode = Mode == "sequential";
                        foreach (int Index in Pending)
                        {
                            if (Token.IsCancellationRequested)
                            {
                                LogPrefetch("WorkerStop", "reason=cancelled");
                                break;
                            }

                            if (TryGetPrefetchPauseReason(out string LoopPauseReason))
                            {
                                LogPrefetch("WorkerPause", LoopPauseReason);
                                break;
                            }

                            if (_Cache.IsCached(Index))
                                continue;

                            long BuildStarted = Environment.TickCount64;
                            HaulScrubFrame Frame = HaulScrubFrameBuilder.BuildCompleteFrame(Data, Field, Index);
                            long BuildMs = Environment.TickCount64 - BuildStarted;
                            if (Token.IsCancellationRequested || TryGetPrefetchPauseReason(out _) || _Cache.IsCached(Index))
                                break;

                            _Cache.StoreFrame(Frame);
                            BuiltCount++;
                            LogPrefetch(
                                "FrameCached",
                                $"index={Index} ms={BuildMs} pngKB={Frame.PngBytes.Length / 1024} totalCached={_Cache.CachedFrameCount}");
                            MaybeRaisePrefetchStateChanged();

                            if (SequentialMode)
                            {
                                AdvanceSequentialPrefetchCursor(Index);
                                MarkInitialSequentialPrefetchCompleteIfDone(StepCount);
                            }

                            FramesSinceBurstPause++;
                            Thread.Sleep(PrefetchYieldMs);
                            if (FramesSinceBurstPause < PrefetchBurstFrameCount)
                                continue;

                            FramesSinceBurstPause = 0;
                            Thread.Sleep(PrefetchBurstPauseMs);
                        }

                        if (SequentialMode)
                            MarkInitialSequentialPrefetchCompleteIfDone(StepCount);

                        MaybeRaisePrefetchStateChanged(Force: true);

                        int Remaining = Pending.Count(Index => !_Cache.IsCached(Index));
                        LogPrefetch("WorkerBatchEnd", $"built={BuiltCount} remaining={Remaining}");

                        if (!Token.IsCancellationRequested &&
                            !TryGetPrefetchPauseReason(out _) &&
                            HasUncachedPrefetchWork(CurrentIndex, StepCount))
                        {
                            LogPrefetch("WorkerRestart", $"delayMs={PrefetchBetweenBatchMs}");
                            Thread.Sleep(PrefetchBetweenBatchMs);
                            Task.Run(MaybeStartPrefetch);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        LogPrefetch("WorkerStop", "reason=operationCanceled");
                    }
                });
        }

        /// <summary>
        /// Builds the ordered prefetch queue for the current controller mode (§7.4.24–§7.4.25).
        /// </summary>
        private (List<int> Queue, string Mode) BuildActivePrefetchQueue
            (
            int CurrentIndex,
            int StepCount
            )
        {
            lock (_Lock)
            {
                if (_PreferBidirectionalFromCurrent)
                {
                    List<int> Relocated = BuildBidirectionalPrefetchOrder(_BidirectionalAnchorIndex, StepCount)
                        .Where(Index => !_Cache.IsCached(Index))
                        .ToList();
                    if (Relocated.Count > 0)
                        return (Relocated, "bidirectionalRelocated");

                    _PreferBidirectionalFromCurrent = false;
                    LogPrefetch(
                        "BidirectionalAnchorDone",
                        $"anchor={_BidirectionalAnchorIndex} resumeSequential={_DeferredSequentialNextIndex}");
                }

                if (!_InitialPrefetchComplete)
                {
                    List<int> Sequential = new List<int>();
                    for (int Index = Math.Max(1, _DeferredSequentialNextIndex); Index < StepCount; Index++)
                    {
                        if (!_Cache.IsCached(Index))
                            Sequential.Add(Index);
                    }

                    if (Sequential.Count > 0)
                        return (Sequential, "sequential");

                    _InitialPrefetchComplete = true;
                }

                List<int> Bidirectional = BuildBidirectionalPrefetchOrder(CurrentIndex, StepCount)
                    .Where(Index => !_Cache.IsCached(Index))
                    .ToList();
                return (Bidirectional, "bidirectional");
            }
        }

        /// <summary>
        /// Returns whether any prefetch work remains for the current controller mode.
        /// </summary>
        private bool HasUncachedPrefetchWork
            (
            int CurrentIndex,
            int StepCount
            )
        {
            return BuildActivePrefetchQueue(CurrentIndex, StepCount).Queue.Count > 0;
        }

        /// <summary>
        /// Advances the §7.4.24 sequential prefetch bookmark after a frame is cached.
        /// </summary>
        private void AdvanceSequentialPrefetchCursor
            (
            int CachedIndex
            )
        {
            lock (_Lock)
            {
                _SequentialPrefetchCursor = Math.Max(_SequentialPrefetchCursor, CachedIndex + 1);
                _DeferredSequentialNextIndex = _SequentialPrefetchCursor;
            }
        }

        /// <summary>
        /// Starts a long-running background worker at below-normal thread priority (§7.4.23).
        /// </summary>
        /// <param name="Token">Cancellation token.</param>
        /// <param name="Work">Worker body.</param>
        private static void RunLowPriorityBackgroundWorker
            (
            CancellationToken Token,
            Action Work
            )
        {
            Task.Factory.StartNew(
                () =>
                {
                    Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
                    try
                    {
                        Work();
                    }
                    finally
                    {
                        Thread.CurrentThread.Priority = ThreadPriority.Normal;
                    }
                },
                Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        /// <summary>
        /// Raises cache tick-mark updates during prefetch without flooding the UI thread.
        /// </summary>
        /// <param name="Force">When true, always raise immediately.</param>
        private void MaybeRaisePrefetchStateChanged
            (
            bool Force = false
            )
        {
            bool ShouldRaise = false;
            lock (_Lock)
            {
                _PrefetchFramesSinceStateRaise++;
                long Now = Environment.TickCount64;
                if (Force ||
                    _PrefetchFramesSinceStateRaise >= PrefetchStateRaiseFrameBatch ||
                    Now - _LastPrefetchStateRaiseTicks >= PrefetchStateRaiseIntervalMs)
                {
                    _PrefetchFramesSinceStateRaise = 0;
                    _LastPrefetchStateRaiseTicks = Now;
                    ShouldRaise = true;
                }
            }

            if (ShouldRaise)
                RaiseStateChanged();
        }

        /// <summary>
        /// Marks initial sequential prefetch complete once every later index is cached.
        /// </summary>
        private void MarkInitialSequentialPrefetchCompleteIfDone
            (
            int StepCount
            )
        {
            for (int Index = 1; Index < StepCount; Index++)
            {
                if (!_Cache.IsCached(Index))
                    return;
            }

            lock (_Lock)
            {
                _InitialPrefetchComplete = true;
            }
        }

        /// <summary>
        /// Returns whether the prefetch worker must pause per §7.4.23 and §4.7.6.
        /// </summary>
        private bool ShouldPausePrefetch
            (
            )
        {
            return TryGetPrefetchPauseReason(out _);
        }

        /// <summary>
        /// Returns whether prefetch is paused and a short reason string for diagnostics.
        /// </summary>
        private bool TryGetPrefetchPauseReason
            (
            out string Reason
            )
        {
            lock (_Lock)
            {
                if (_SuspendedForThreeDView)
                {
                    Reason = "threeDView";
                    return true;
                }

                if (_SliderInteractionActive)
                {
                    Reason = "sliderActive";
                    return true;
                }

                if (_Data == null || _Field == null || RouteStepCount <= 0)
                {
                    Reason = "noData";
                    return true;
                }
            }

            if (_ForegroundGenerationActive)
            {
                Reason = "foregroundActive";
                return true;
            }

            Reason = string.Empty;
            return false;
        }

        /// <summary>
        /// Stores sequential prefetch progress and relocates prefetch to the current index (§7.4.17).
        /// </summary>
        private void RelocatePrefetchToCurrentIndex
            (
            string Trigger,
            int AnchorIndex
            )
        {
            int DeferredSequential;
            int Cursor;
            lock (_Lock)
            {
                if (!_InitialPrefetchComplete)
                    _DeferredSequentialNextIndex = _SequentialPrefetchCursor;

                _PreferBidirectionalFromCurrent = true;
                _BidirectionalAnchorIndex = AnchorIndex;
                DeferredSequential = _DeferredSequentialNextIndex;
                Cursor = _SequentialPrefetchCursor;
            }

            LogPrefetch(
                "PrefetchRelocate",
                $"trigger={Trigger} anchor={AnchorIndex} deferredSequential={DeferredSequential} cursor={Cursor}");
        }

        /// <summary>
        /// Writes one prefetch diagnostic line when a sink is attached.
        /// </summary>
        private void LogPrefetch
            (
            string Phase,
            string Detail
            )
        {
            PrefetchDiagnosticSink?.Invoke(Phase, Detail);
        }

        /// <summary>
        /// Writes one scrub diagnostic line when a sink is attached.
        /// </summary>
        private void LogScrub
            (
            string Phase,
            string Detail
            )
        {
            ScrubDiagnosticSink?.Invoke(Phase, Detail);
        }

        /// <summary>
        /// Cancels any active prefetch worker.
        /// </summary>
        private void StopPrefetchWorker
            (
            )
        {
            _PrefetchCts?.Cancel();
            _PrefetchCts?.Dispose();
            _PrefetchCts = null;
        }

        /// <summary>
        /// Returns the route scrub index shown in the route position label and statistics panel.
        /// </summary>
        private int GetDisplayedRouteScrubIndex
            (
            )
        {
            lock (_Lock)
            {
                return _SliderInteractionActive ? _LiveRouteScrubIndex : RouteScrubIndex;
            }
        }

        /// <summary>
        /// Builds bidirectional prefetch order from the current scrub index.
        /// </summary>
        private static IEnumerable<int> BuildBidirectionalPrefetchOrder
            (
            int CurrentIndex,
            int StepCount
            )
        {
            for (int Offset = 1; Offset < StepCount; Offset++)
            {
                int Earlier = CurrentIndex - Offset;
                if (Earlier >= 0)
                    yield return Earlier;

                int Later = CurrentIndex + Offset;
                if (Later < StepCount)
                    yield return Later;
            }
        }

        /// <summary>
        /// Starts the partial-frame pump when idle during slider drag.
        /// </summary>
        private void RequestPartialFrameDuringScrub
            (
            )
        {
            lock (_Lock)
            {
                if (!_SliderInteractionActive || _Data == null || _Field == null || RouteStepCount <= 0)
                    return;

                if (_PartialPumpRunning)
                    return;
            }

            StartPartialFramePump();
        }

        /// <summary>
        /// Starts a single background pump that builds and displays partial route sprites until caught up.
        /// </summary>
        private void StartPartialFramePump
            (
            )
        {
            HaulData Data;
            HaulBuilder.AgdField Field;
            lock (_Lock)
            {
                if (_SuspendedForThreeDView ||
                    !_SliderInteractionActive ||
                    _Data == null ||
                    _Field == null ||
                    RouteStepCount <= 0 ||
                    _PartialPumpRunning)
                    return;

                Data = _Data;
                Field = _Field;
            }

            StopPartialFrameGeneration();
            _PartialPumpRunning = true;
            _PartialFrameCts = new CancellationTokenSource();
            CancellationToken Token = _PartialFrameCts.Token;
            LogScrub(
                "PartialPumpStart",
                $"live={_LiveRouteScrubIndex} displayed={_LastDisplayedPartialIndex}");

            Task.Run(() => RunPartialFramePump(Data, Field, Token), Token);
        }

        /// <summary>
        /// Builds partial route sprites in a loop until the live scrub index is displayed or scrubbing ends.
        /// Throttles to one displayed frame every 50 ms; stale builds retry immediately without cancelling in-flight work.
        /// </summary>
        private void RunPartialFramePump
            (
            HaulData Data,
            HaulBuilder.AgdField Field,
            CancellationToken Token
            )
        {
            try
            {
                while (!Token.IsCancellationRequested)
                {
                    int TargetIndex;
                    lock (_Lock)
                    {
                        if (!_SliderInteractionActive)
                            break;

                        TargetIndex = _LiveRouteScrubIndex;
                    }

                    if (TargetIndex == _LastDisplayedPartialIndex)
                        break;

                    long Now = Environment.TickCount64;
                    long Elapsed = Now - _LastPartialDisplayTicks;
                    if (_LastPartialDisplayTicks > 0 && Elapsed < ScrubPartialIntervalMs)
                    {
                        int WaitMs = (int)(ScrubPartialIntervalMs - Elapsed);
                        LogScrub("PartialThrottle", $"index={TargetIndex} waitMs={WaitMs}");
                        if (Token.WaitHandle.WaitOne(WaitMs))
                            break;

                        continue;
                    }

                    long BuildStarted = Environment.TickCount64;
                    LogScrub("PartialBuild", $"index={TargetIndex}");
                    HaulScrubFrame Frame;
                    try
                    {
                        Frame = HaulScrubFrameBuilder.BuildRouteSprite(Data, Field, TargetIndex);
                    }
                    catch
                    {
                        break;
                    }

                    long BuildMs = Environment.TickCount64 - BuildStarted;
                    if (Token.IsCancellationRequested)
                    {
                        LogScrub("PartialCancel", $"index={TargetIndex} buildMs={BuildMs}");
                        break;
                    }

                    int LiveIndex;
                    lock (_Lock)
                    {
                        if (!_SliderInteractionActive)
                            break;

                        LiveIndex = _LiveRouteScrubIndex;
                    }

                    if (LiveIndex != TargetIndex)
                    {
                        LogScrub(
                            "PartialLag",
                            $"built={TargetIndex} live={LiveIndex} buildMs={BuildMs}");
                    }

                    LogScrub(
                        "PartialReady",
                        $"index={TargetIndex} buildMs={BuildMs} pngKB={Frame.PngBytes.Length / 1024}");
                    RaiseFrameChanged(Frame);
                    _LastDisplayedPartialIndex = TargetIndex;
                    _LastPartialDisplayTicks = Environment.TickCount64;

                    if (LiveIndex == TargetIndex)
                        break;
                }
            }
            finally
            {
                _PartialPumpRunning = false;
                LogScrub(
                    "PartialPumpStop",
                    $"live={_LiveRouteScrubIndex} displayed={_LastDisplayedPartialIndex}");
            }
        }

        /// <summary>
        /// Cancels any in-flight partial route sprite generation.
        /// </summary>
        private void StopPartialFrameGeneration
            (
            )
        {
            _PartialFrameCts?.Cancel();
            _PartialFrameCts?.Dispose();
            _PartialFrameCts = null;
            _PartialPumpRunning = false;
        }

        /// <summary>
        /// Resets mutable session state without raising UI events.
        /// </summary>
        private void ResetInternal
            (
            )
        {
            StopPartialFrameGeneration();
            _ForegroundCts?.Cancel();
            _ForegroundCts?.Dispose();
            _ForegroundCts = null;
            StopPrefetchWorker();
            _Cache.Clear();
            _Data = null;
            _Field = null;
            _InitialPrefetchComplete = false;
            _DeferredSequentialNextIndex = 1;
            _SequentialPrefetchCursor = 1;
            _PreferBidirectionalFromCurrent = false;
            _BidirectionalAnchorIndex = 0;
            _ForegroundGenerationActive = false;
            _SliderInteractionActive = false;
            _SuspendedForThreeDView = false;
            _PrefetchFramesSinceStateRaise = 0;
            _LastPrefetchStateRaiseTicks = 0;
            _LastDisplayedPartialIndex = -1;
            _LastPartialDisplayTicks = 0;
        }

        /// <summary>
        /// Returns whether haul scrub background workers are paused for haul 3D view.
        /// </summary>
        private bool IsSuspendedForThreeDView
            (
            )
        {
            lock (_Lock)
            {
                return _SuspendedForThreeDView;
            }
        }

        /// <summary>
        /// Raises <see cref="FrameChanged"/> for UI subscribers.
        /// </summary>
        private void RaiseFrameChanged
            (
            HaulScrubFrame? Frame
            )
        {
            FrameChanged?.Invoke(Frame);
        }

        /// <summary>
        /// Raises <see cref="StateChanged"/> for UI subscribers.
        /// </summary>
        private void RaiseStateChanged
            (
            )
        {
            StateChanged?.Invoke();
        }

        /// <summary>
        /// Raises <see cref="ForegroundGenerationStarted"/> for UI progress overlays.
        /// </summary>
        /// <param name="RouteScrubIndex">Route scrub index being generated.</param>
        private void RaiseForegroundGenerationStarted
            (
            int RouteScrubIndex
            )
        {
            ForegroundGenerationStarted?.Invoke(RouteScrubIndex);
        }

        /// <summary>
        /// Raises <see cref="ForegroundGenerationCompleted"/> for UI progress overlays.
        /// </summary>
        private void RaiseForegroundGenerationCompleted
            (
            )
        {
            ForegroundGenerationCompleted?.Invoke();
        }
    }
}
