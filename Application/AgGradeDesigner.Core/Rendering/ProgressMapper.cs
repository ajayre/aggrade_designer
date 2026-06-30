namespace AgGradeDesigner.Core.Rendering
{
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
