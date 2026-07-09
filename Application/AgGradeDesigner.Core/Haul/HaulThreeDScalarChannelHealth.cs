using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;

namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Counts how the haul-leveled scalar channel compares to survey Existing on a surface sampler.
    /// </summary>
    public readonly struct HaulThreeDScalarChannelHealth
    {
        /// <summary>True when <see cref="FieldSurfaceSampler.HaulLeveledIsBuilt"/> was set on the analyzed sampler.</summary>
        public bool HaulLeveledIsBuilt { get; init; }

        /// <summary>Number of valid raster samples on the sampler.</summary>
        public int ValidSampleCount { get; init; }

        /// <summary>Valid samples with a finite haul-leveled elevation.</summary>
        public int FiniteHaulSampleCount { get; init; }

        /// <summary>Valid samples where haul-leveled elevation is NaN.</summary>
        public int NanHaulSampleCount { get; init; }

        /// <summary>Valid samples where haul-leveled equals survey Existing (possible leak).</summary>
        public int MatchesExistingCount { get; init; }

        /// <summary>Valid samples where haul-leveled differs from survey Existing.</summary>
        public int DiffersFromExistingCount { get; init; }
    }

    /// <summary>
    /// Analyzes haul 3D scalar channels for survey Existing contamination and missing haul samples.
    /// </summary>
    public static class HaulThreeDScalarChannelHealthAnalyzer
    {
        /// <summary>Epsilon for comparing elevations in metres.</summary>
        private const double ElevationMatchEpsilonM = 0.001;

        /// <summary>
        /// Summarizes haul channel health on a surface sampler.
        /// </summary>
        /// <param name="Sampler">Surface sampler under test.</param>
        /// <returns>Counts used by haul 3D diagnostics logging.</returns>
        public static HaulThreeDScalarChannelHealth Analyze
            (
            FieldSurfaceSampler Sampler
            )
        {
            if (Sampler.SampleWidth <= 0 || Sampler.Existing.Length == 0)
            {
                return new HaulThreeDScalarChannelHealth
                {
                    HaulLeveledIsBuilt = Sampler.HaulLeveledIsBuilt,
                };
            }

            int ValidCount = 0;
            int FiniteHaulCount = 0;
            int NanHaulCount = 0;
            int MatchesExisting = 0;
            int DiffersFromExisting = 0;
            bool[] Valid = Sampler.Valid;
            double[] Existing = Sampler.Existing;
            double[] HaulLeveled = Sampler.HaulLeveled;
            int Length = Math.Min(Valid.Length, Math.Min(Existing.Length, HaulLeveled.Length));

            for (int I = 0; I < Length; I++)
            {
                if (!Valid[I])
                    continue;

                ValidCount++;
                if (!Sampler.HaulLeveledIsBuilt)
                {
                    NanHaulCount++;
                    continue;
                }

                double HaulValue = HaulLeveled[I];
                if (double.IsNaN(HaulValue) || double.IsInfinity(HaulValue))
                {
                    NanHaulCount++;
                    continue;
                }

                FiniteHaulCount++;
                double ExistingValue = Existing[I];
                if (double.IsNaN(ExistingValue) || Math.Abs(HaulValue - ExistingValue) > ElevationMatchEpsilonM)
                    DiffersFromExisting++;
                else
                    MatchesExisting++;
            }

            return new HaulThreeDScalarChannelHealth
            {
                HaulLeveledIsBuilt = Sampler.HaulLeveledIsBuilt,
                ValidSampleCount = ValidCount,
                FiniteHaulSampleCount = FiniteHaulCount,
                NanHaulSampleCount = NanHaulCount,
                MatchesExistingCount = MatchesExisting,
                DiffersFromExistingCount = DiffersFromExisting,
            };
        }

        /// <summary>
        /// Formats health counts as a single grep-friendly log line.
        /// </summary>
        /// <param name="Health">Analyzed channel health.</param>
        /// <returns>Space-separated key=value pairs.</returns>
        public static string FormatSummary
            (
            HaulThreeDScalarChannelHealth Health
            )
        {
            return string.Join(
                " ",
                $"built={Health.HaulLeveledIsBuilt}",
                $"valid={Health.ValidSampleCount}",
                $"finiteHaul={Health.FiniteHaulSampleCount}",
                $"nanHaul={Health.NanHaulSampleCount}",
                $"matchesExisting={Health.MatchesExistingCount}",
                $"differsExisting={Health.DiffersFromExistingCount}");
        }

        /// <summary>
        /// Serializes health counts for JSONL tool traces.
        /// </summary>
        /// <param name="Health">Analyzed channel health.</param>
        /// <param name="Phase">Diagnostic phase identifier.</param>
        /// <param name="RouteScrubIndex">Pinned haul route scrub index, or -1 when unset.</param>
        /// <returns>Single-line JSON object.</returns>
        public static string FormatJsonLine
            (
            HaulThreeDScalarChannelHealth Health,
            string Phase,
            int RouteScrubIndex
            )
        {
            return "{" +
                   $"\"kind\":\"haul3d_scalar_health\",\"phase\":\"{Phase}\",\"routeScrubIndex\":{RouteScrubIndex}," +
                   $"\"built\":{(Health.HaulLeveledIsBuilt ? "true" : "false")}," +
                   $"\"valid\":{Health.ValidSampleCount},\"finiteHaul\":{Health.FiniteHaulSampleCount}," +
                   $"\"nanHaul\":{Health.NanHaulSampleCount},\"matchesExisting\":{Health.MatchesExistingCount}," +
                   $"\"differsExisting\":{Health.DiffersFromExistingCount}}}";
        }
    }
}
