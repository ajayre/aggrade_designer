namespace AgGradeDesigner.Core.Rendering
{
    /// <summary>
    /// Tracks a non-decreasing 0–100 progress value for UI overlays.
    /// </summary>
    public sealed class MonotonicProgressTracker
    {
        /// <summary>Last progress value accepted by <see cref="Advance"/>.</summary>
        public int HighWaterMark { get; private set; }

        /// <summary>Resets the high-water mark for a new operation.</summary>
        /// <param name="StartPercent">Starting progress percent.</param>
        public void Reset
            (
            int StartPercent = 0
            )
        {
            HighWaterMark = Math.Clamp(StartPercent, 0, 100);
        }

        /// <summary>
        /// Returns the new high-water mark after applying <paramref name="Percent"/>.
        /// </summary>
        /// <param name="Percent">Requested progress percent.</param>
        /// <returns>Non-decreasing progress in [0, 100].</returns>
        public int Advance
            (
            int Percent
            )
        {
            int Clamped = Math.Clamp(Percent, 0, 100);
            HighWaterMark = Math.Max(HighWaterMark, Clamped);
            return HighWaterMark;
        }
    }

    /// <summary>
    /// Maps local 0–100 progress into a sub-range of a parent progress reporter.
    /// </summary>
    static public class ProgressMapper
    {
        /// <summary>
        /// Reports a percentage within an inclusive segment of the parent range.
        /// </summary>
        static public void ReportSegment
            (
            IProgress<int>? Target,
            int SegmentMin,
            int SegmentMax,
            int SegmentPercent
            )
        {
            if (Target == null)
                return;

            int Clamped = Math.Clamp(SegmentPercent, 0, 100);
            int Value = SegmentMin + (int)Math.Round(Clamped * (SegmentMax - SegmentMin) / 100.0);
            Target.Report(Math.Clamp(Value, SegmentMin, SegmentMax));
        }

        /// <summary>
        /// Returns a child reporter that maps its own 0–100 values into a parent sub-range.
        /// </summary>
        static public IProgress<int>? Subrange
            (
            IProgress<int>? Target,
            int SegmentMin,
            int SegmentMax
            )
        {
            if (Target == null)
                return null;

            return new Progress<int>(Value => ReportSegment(Target, SegmentMin, SegmentMax, Value));
        }
    }
}
