using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Tests.TestFixtures
{
    /// <summary>
    /// Configures a headless AGD import handoff scenario for field comparison tests.
    /// </summary>
    internal sealed class ImportHandoffScenario
    {
        /// <summary>Imported project under test.</summary>
        public required ProjectDocument Project { get; init; }

        /// <summary>Human-readable field label for diagnostics.</summary>
        public required string FieldName { get; init; }

        /// <summary>Headless viewer width in pixels.</summary>
        public int ViewerWidth { get; init; } = ImportHandoffHarness.DefaultViewportWidth;

        /// <summary>Headless viewer height in pixels.</summary>
        public int ViewerHeight { get; init; } = ImportHandoffHarness.DefaultViewportHeight;

        /// <summary>Viewport width used for import camera fit, when set.</summary>
        public int? FitViewportWidth { get; init; }

        /// <summary>Viewport height used for import camera fit, when set.</summary>
        public int? FitViewportHeight { get; init; }
    }
}
