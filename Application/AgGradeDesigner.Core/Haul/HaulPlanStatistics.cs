using System.Globalization;

using AgGradeDesigner.Core.Units;

namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Plan-level haul statistics derived from route-ordered cut/fill pairs.
    /// </summary>
    public sealed class HaulPlanSummary
    {
        /// <summary>Total field cut volume in BCY after all routes complete.</summary>
        public double TotalCutBcy { get; init; }

        /// <summary>Total field fill volume in BCY after all routes complete.</summary>
        public double TotalFillBcy { get; init; }

        /// <summary>Total estimated plan time in seconds.</summary>
        public double TotalTimeSeconds { get; init; }

        /// <summary>Number of haul routes in the plan.</summary>
        public int RouteCount { get; init; }
    }

    /// <summary>
    /// Route-scoped haul statistics for one scrub index.
    /// </summary>
    public sealed class HaulRouteStatistics
    {
        /// <summary>Zero-based route scrub index.</summary>
        public int RouteIndex { get; init; }

        /// <summary>Cumulative time in seconds before this route starts.</summary>
        public double TimeBeforeSeconds { get; init; }

        /// <summary>Cumulative cut BCY completed before this route starts.</summary>
        public double CutBeforeBcy { get; init; }

        /// <summary>Cumulative fill BCY completed before this route starts.</summary>
        public double FillBeforeBcy { get; init; }

        /// <summary>Load moved on this route in BCY.</summary>
        public double LoadBcy { get; init; }

        /// <summary>Estimated time for this route in seconds.</summary>
        public double RouteTimeSeconds { get; init; }

        /// <summary>Fraction of total plan cut completed before this route (0–1).</summary>
        public double CutCompleteFraction { get; init; }
    }

    /// <summary>
    /// Resolves and formats haul plan statistics from persisted or live haul data.
    /// </summary>
    public static class HaulPlanStatistics
    {
        /// <summary>Decimal places used when formatting haul volume labels.</summary>
        private const int VOLUME_DISPLAY_DECIMALS = 1;

        /// <summary>Decimal places used when formatting haul time labels in hours.</summary>
        private const int TIME_DISPLAY_DECIMALS = 2;

        /// <summary>Multiplier applied when converting a cut-complete fraction to percent display.</summary>
        private const double FRACTION_TO_PERCENT = 100.0;

        /// <summary>
        /// Builds plan totals from route-ordered cut/fill pairs.
        /// </summary>
        /// <param name="Pairs">Route-ordered cut/fill pairs.</param>
        /// <param name="GuidanceTotalTimeSeconds">
        /// Optional guidance-track total time that replaces the sum of pair route times when provided.
        /// </param>
        /// <returns>Plan summary, or null when no routes are present.</returns>
        public static HaulPlanSummary? BuildPlanSummary
            (
            IReadOnlyList<HaulCutFillPairDto> Pairs,
            double? GuidanceTotalTimeSeconds = null
            )
        {
            if (Pairs.Count == 0)
                return null;

            HaulCutFillPairDto LastPair = Pairs[Pairs.Count - 1];
            double TotalTimeSeconds = 0.0;
            foreach (HaulCutFillPairDto Pair in Pairs)
                TotalTimeSeconds += Pair.RouteTimeSeconds;

            // intent: prefer realistic guidance-track time over coarse per-route time sums
            if (GuidanceTotalTimeSeconds.HasValue)
                TotalTimeSeconds = GuidanceTotalTimeSeconds.Value;

            return new HaulPlanSummary
            {
                TotalCutBcy = LastPair.TotalCutBcyBefore + LastPair.LoadBcy,
                TotalFillBcy = LastPair.TotalFillBcyBefore + LastPair.LoadBcy,
                TotalTimeSeconds = TotalTimeSeconds,
                RouteCount = Pairs.Count
            };
        }

        /// <summary>
        /// Builds route-scoped statistics for the supplied scrub index.
        /// </summary>
        /// <param name="Pairs">Route-ordered cut/fill pairs.</param>
        /// <param name="RouteScrubIndex">Zero-based route scrub index.</param>
        /// <param name="Summary">Plan summary used for cut-complete percentage.</param>
        /// <returns>Route statistics, or null when the index is out of range.</returns>
        public static HaulRouteStatistics? BuildRouteStatistics
            (
            IReadOnlyList<HaulCutFillPairDto> Pairs,
            int RouteScrubIndex,
            HaulPlanSummary Summary
            )
        {
            if (Pairs.Count == 0 || Summary.RouteCount <= 0)
                return null;

            int Index = Math.Clamp(RouteScrubIndex, 0, Pairs.Count - 1);
            HaulCutFillPairDto Pair = Pairs[Index];

            double TimeBeforeSeconds = 0.0;
            for (int Step = 0; Step < Index; Step++)
                TimeBeforeSeconds += Pairs[Step].RouteTimeSeconds;

            double CutCompleteFraction = Summary.TotalCutBcy > 0.0
                ? Pair.TotalCutBcyBefore / Summary.TotalCutBcy
                : 0.0;

            return new HaulRouteStatistics
            {
                RouteIndex = Index,
                TimeBeforeSeconds = TimeBeforeSeconds,
                CutBeforeBcy = Pair.TotalCutBcyBefore,
                FillBeforeBcy = Pair.TotalFillBcyBefore,
                LoadBcy = Pair.LoadBcy,
                RouteTimeSeconds = Pair.RouteTimeSeconds,
                CutCompleteFraction = Math.Clamp(CutCompleteFraction, 0.0, 1.0)
            };
        }

        /// <summary>
        /// Formats a bank volume in BCY for haul plan labels.
        /// </summary>
        /// <param name="VolumeBcy">Volume in bank cubic yards.</param>
        /// <param name="System">Active display unit system.</param>
        /// <returns>Formatted quantity with unit suffix.</returns>
        public static string FormatBankVolume
            (
            double VolumeBcy,
            DisplayUnitSystem System
            )
        {
            double DisplayValue = HaulDisplayUnits.BankVolumeBcyToDisplay(VolumeBcy, System);
            string Unit = System == DisplayUnitSystem.Metric ? "m³" : "CY";
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:N" + VOLUME_DISPLAY_DECIMALS + "} {1}",
                DisplayValue,
                Unit);
        }

        /// <summary>
        /// Formats a duration in seconds as hours for haul plan labels.
        /// </summary>
        /// <param name="TimeSeconds">Duration in seconds.</param>
        /// <returns>Formatted hours label.</returns>
        public static string FormatHours
            (
            double TimeSeconds
            )
        {
            double Hours = TimeSeconds / 3600.0;
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:N" + TIME_DISPLAY_DECIMALS + "} h",
                Hours);
        }

        /// <summary>
        /// Formats a cut-complete fraction as a percentage label.
        /// </summary>
        /// <param name="CutCompleteFraction">Fraction complete (0–1).</param>
        /// <returns>Formatted percent label.</returns>
        public static string FormatCutCompletePercent
            (
            double CutCompleteFraction
            )
        {
            double Percent = Math.Clamp(CutCompleteFraction, 0.0, 1.0) * FRACTION_TO_PERCENT;
            return string.Format(CultureInfo.InvariantCulture, "{0:N1}%", Percent);
        }
    }
}
