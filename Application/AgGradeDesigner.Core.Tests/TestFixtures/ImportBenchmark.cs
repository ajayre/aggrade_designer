using System.Diagnostics;

namespace AgGradeDesigner.Core.Tests.TestFixtures
{
    /// <summary>
    /// Measures elapsed wall-clock time for import operations with warmup.
    /// </summary>

    static internal class ImportBenchmark
    {
        /// <summary>
        /// Runs an action several times after warmup and returns the median elapsed milliseconds.
        /// </summary>
        /// <param name="Action">Work to measure.</param>
        /// <param name="WarmupRuns">Discarded runs to JIT and populate caches.</param>
        /// <param name="MeasuredRuns">Timed runs used for the median.</param>
        /// <returns>Median elapsed milliseconds across measured runs.</returns>
        static public double MedianMilliseconds
            (
            Action Action,
            int WarmupRuns = 2,
            int MeasuredRuns = 5
            )
        {
            for (int Index = 0; Index < WarmupRuns; Index++)
                Action();

            double[] Samples = new double[MeasuredRuns];
            for (int Index = 0; Index < MeasuredRuns; Index++)
            {
                Stopwatch Watch = Stopwatch.StartNew();
                Action();
                Watch.Stop();
                Samples[Index] = Watch.Elapsed.TotalMilliseconds;
            }

            Array.Sort(Samples);
            return Samples[Samples.Length / 2];
        }
    }
}
