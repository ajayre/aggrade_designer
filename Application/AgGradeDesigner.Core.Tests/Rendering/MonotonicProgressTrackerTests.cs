using AgGradeDesigner.Core.Rendering;

namespace AgGradeDesigner.Core.Tests.Rendering
{
    /// <summary>
    /// Guards non-decreasing overlay progress used during import and prepare.
    /// </summary>
    public sealed class MonotonicProgressTrackerTests
    {
        /// <summary>
        /// Simulates nested subrange reporters reporting backward within one session.
        /// </summary>
        [Fact]
        public void Advance_NeverDecreasesHighWaterMark
            (
            )
        {
            MonotonicProgressTracker Tracker = new MonotonicProgressTracker();
            Tracker.Reset(18);

            int[] Reported = { 18, 25, 40, 55, 63, 55, 40, 70, 72, 90, 88, 99, 100 };
            int Previous = Tracker.HighWaterMark;
            foreach (int Percent in Reported)
            {
                int Monotonic = Tracker.Advance(Percent);
                Assert.True(Monotonic >= Previous, $"Progress regressed from {Previous} to {Monotonic}.");
                Previous = Monotonic;
            }

            Assert.Equal(100, Tracker.HighWaterMark);
        }

        /// <summary>
        /// Reset starts a new monotonic session at the requested percent.
        /// </summary>
        [Fact]
        public void Reset_StartsNewSessionAtStartPercent
            (
            )
        {
            MonotonicProgressTracker Tracker = new MonotonicProgressTracker();
            Tracker.Reset(0);
            Tracker.Advance(72);
            Tracker.Reset(18);

            Assert.Equal(18, Tracker.HighWaterMark);
            Assert.Equal(18, Tracker.Advance(0));
            Assert.Equal(45, Tracker.Advance(45));
        }
    }
}
