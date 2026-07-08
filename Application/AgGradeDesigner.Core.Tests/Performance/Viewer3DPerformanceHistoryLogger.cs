using System.Globalization;
using System.Text;

using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Performance
{
    /// <summary>
    /// Appends passing 3D performance measurements to <see cref="HistoryFileName"/> for trend tracking.
    /// </summary>
    internal static class Viewer3DPerformanceHistoryLogger
    {
        /// <summary>Markdown history file name under the test project Performance folder.</summary>
        private const string HistoryFileName = "Viewer3DPerformanceHistory.md";

        /// <summary>Suite label for <see cref="FieldViewer3DPerformanceGateTests"/>.</summary>
        private const string VIEWER_GATE_SUITE = "Viewer3DPerformanceGate";

        /// <summary>Suite label for <see cref="FieldRenderer3DPerformanceTests"/>.</summary>
        private const string FIELD_RENDERER_SUITE = "FieldRenderer3DPerformance";

        /// <summary>Placeholder for metrics that do not apply to a given test.</summary>
        private const string NOT_APPLICABLE = "—";

        /// <summary>Synchronizes append access to the shared history file.</summary>
        private static readonly object Sync = new object();

        /// <summary>
        /// Records a passing viewer-control orbit gate result.
        /// </summary>
        /// <param name="TestName">Fully qualified scenario name.</param>
        /// <param name="Metrics">Measured orbit metrics.</param>
        /// <param name="MedianBudgetMs">Median frame budget in milliseconds.</param>
        /// <param name="MaxBudgetMs">Maximum frame budget in milliseconds.</param>
        /// <param name="Notes">Optional free-form detail.</param>
        public static void RecordViewerGateOrbitPass
            (
            string TestName,
            FieldViewer3DOrbitMetrics Metrics,
            double MedianBudgetMs,
            double MaxBudgetMs,
            string? Notes = null
            )
        {
            AppendRow(
                VIEWER_GATE_SUITE,
                TestName,
                FormatNumber(Metrics.MedianFrameMs),
                FormatNumber(Metrics.MaxFrameMs),
                FormatNumber(Metrics.TotalMs),
                Metrics.GeometryCacheBuildCount.ToString(CultureInfo.InvariantCulture),
                Metrics.HaulOverlayStampCount.ToString(CultureInfo.InvariantCulture),
                Metrics.HaulCommittedSurfaceReuseCount.ToString(CultureInfo.InvariantCulture),
                FormatNumber(MedianBudgetMs),
                FormatNumber(MaxBudgetMs),
                BuildNotes(Notes, $"steps={Metrics.StepCount}"));
        }

        /// <summary>
        /// Records a passing vertical-exaggeration rebuild gate result.
        /// </summary>
        /// <param name="TestName">Fully qualified scenario name.</param>
        /// <param name="FrameMs">Measured rebuild time in milliseconds.</param>
        /// <param name="GeometryRebuilds">Geometry cache rebuild count.</param>
        /// <param name="FrameBudgetMs">Frame rebuild budget in milliseconds.</param>
        /// <param name="GeometryRebuildBudget">Maximum allowed geometry rebuilds.</param>
        public static void RecordViewerGateExaggerationPass
            (
            string TestName,
            double FrameMs,
            int GeometryRebuilds,
            double FrameBudgetMs,
            int GeometryRebuildBudget
            )
        {
            AppendRow(
                VIEWER_GATE_SUITE,
                TestName,
                FormatNumber(FrameMs),
                NOT_APPLICABLE,
                NOT_APPLICABLE,
                GeometryRebuilds.ToString(CultureInfo.InvariantCulture),
                NOT_APPLICABLE,
                NOT_APPLICABLE,
                FormatNumber(FrameBudgetMs),
                NOT_APPLICABLE,
                $"geoRebuildBudget={GeometryRebuildBudget}");
        }

        /// <summary>
        /// Records a passing renderer-level frame timing result.
        /// </summary>
        /// <param name="TestName">Fully qualified scenario name.</param>
        /// <param name="MedianMs">Measured median time in milliseconds.</param>
        /// <param name="BudgetMs">Budget in milliseconds.</param>
        /// <param name="Notes">Optional free-form detail.</param>
        public static void RecordRendererTimingPass
            (
            string TestName,
            double MedianMs,
            double BudgetMs,
            string? Notes = null
            )
        {
            AppendRow(
                FIELD_RENDERER_SUITE,
                TestName,
                FormatNumber(MedianMs),
                NOT_APPLICABLE,
                NOT_APPLICABLE,
                NOT_APPLICABLE,
                NOT_APPLICABLE,
                NOT_APPLICABLE,
                FormatNumber(BudgetMs),
                NOT_APPLICABLE,
                Notes);
        }

        /// <summary>
        /// Records a passing renderer-level comparison result with two measured medians.
        /// </summary>
        /// <param name="TestName">Fully qualified scenario name.</param>
        /// <param name="PrimaryMedianMs">Primary measured median in milliseconds.</param>
        /// <param name="SecondaryMedianMs">Secondary measured median in milliseconds.</param>
        /// <param name="Notes">Comparison detail.</param>
        public static void RecordRendererComparisonPass
            (
            string TestName,
            double PrimaryMedianMs,
            double SecondaryMedianMs,
            string Notes
            )
        {
            AppendRow(
                FIELD_RENDERER_SUITE,
                TestName,
                FormatNumber(PrimaryMedianMs),
                FormatNumber(SecondaryMedianMs),
                NOT_APPLICABLE,
                NOT_APPLICABLE,
                NOT_APPLICABLE,
                NOT_APPLICABLE,
                NOT_APPLICABLE,
                NOT_APPLICABLE,
                Notes);
        }

        /// <summary>
        /// Appends one result row to the shared markdown history file.
        /// </summary>
        private static void AppendRow
            (
            string Suite,
            string TestName,
            string MedianMs,
            string MaxMs,
            string TotalMs,
            string GeoRebuilds,
            string HaulStamps,
            string HaulSurfaceReuses,
            string BudgetMedianMs,
            string BudgetMaxMs,
            string? Notes
            )
        {
            string HistoryPath = ResolveHistoryPath();
            string TimestampUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            string EscapedNotes = EscapeCell(Notes ?? string.Empty);

            StringBuilder Row = new StringBuilder();
            Row.Append("| ");
            Row.Append(TimestampUtc);
            Row.Append(" | ");
            Row.Append(EscapeCell(Suite));
            Row.Append(" | ");
            Row.Append(EscapeCell(TestName));
            Row.Append(" | ");
            Row.Append(MedianMs);
            Row.Append(" | ");
            Row.Append(MaxMs);
            Row.Append(" | ");
            Row.Append(TotalMs);
            Row.Append(" | ");
            Row.Append(GeoRebuilds);
            Row.Append(" | ");
            Row.Append(HaulStamps);
            Row.Append(" | ");
            Row.Append(HaulSurfaceReuses);
            Row.Append(" | ");
            Row.Append(BudgetMedianMs);
            Row.Append(" | ");
            Row.Append(BudgetMaxMs);
            Row.Append(" | ");
            Row.Append(EscapedNotes);
            Row.AppendLine(" |");

            lock (Sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(HistoryPath)!);
                if (!File.Exists(HistoryPath))
                {
                    File.WriteAllText(
                        HistoryPath,
                        "# 3D Performance Test History\n\n"
                        + "Append-only log of **passing** 3D performance regression runs.\n\n"
                        + "| Timestamp (UTC) | Suite | Test | Median ms | Max ms | Total ms | Geo rebuilds | Haul stamps | Haul surface reuses | Budget median ms | Budget max ms | Notes |\n"
                        + "|-----------------|-------|------|-----------|--------|----------|--------------|-------------|---------------------|------------------|---------------|-------|\n");
                }

                File.AppendAllText(HistoryPath, Row.ToString(), Encoding.UTF8);
            }
        }

        /// <summary>
        /// Resolves the markdown history path in the test project source tree.
        /// </summary>
        /// <returns>Absolute path to <see cref="HistoryFileName"/>.</returns>
        private static string ResolveHistoryPath()
        {
            DirectoryInfo? Dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (Dir != null)
            {
                if (File.Exists(Path.Combine(Dir.FullName, "AgGradeDesigner.Core.Tests.csproj")))
                    return Path.Combine(Dir.FullName, "Performance", HistoryFileName);

                Dir = Dir.Parent;
            }

            return Path.Combine(AppContext.BaseDirectory, HistoryFileName);
        }

        /// <summary>
        /// Formats one millisecond measurement for markdown table cells.
        /// </summary>
        /// <param name="Value">Measurement in milliseconds.</param>
        /// <returns>Formatted value.</returns>
        private static string FormatNumber(double Value)
        {
            return Value.ToString("0.0", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Escapes pipe characters so markdown table cells stay well-formed.
        /// </summary>
        /// <param name="Value">Raw cell text.</param>
        /// <returns>Escaped cell text.</returns>
        private static string EscapeCell(string Value)
        {
            return Value.Replace("|", "/", StringComparison.Ordinal);
        }

        /// <summary>
        /// Joins optional note fragments into one cell value.
        /// </summary>
        /// <param name="Notes">Primary notes text.</param>
        /// <param name="Suffix">Additional detail.</param>
        /// <returns>Combined notes string.</returns>
        private static string? BuildNotes(string? Notes, string Suffix)
        {
            if (string.IsNullOrWhiteSpace(Notes))
                return Suffix;

            return $"{Notes}; {Suffix}";
        }
    }
}
