# 3D Performance Test History

Append-only log of **passing** 3D performance regression runs. Each row is recorded automatically when a test completes successfully.

Use this file to spot gradual slowdowns against the fixed budgets in the test harness.

| Timestamp (UTC) | Suite | Test | Median ms | Max ms | Total ms | Geo rebuilds | Haul stamps | Haul surface reuses | Budget median ms | Budget max ms | Notes |
|-----------------|-------|------|-----------|--------|----------|--------------|-------------|---------------------|------------------|---------------|-------|
| 2026-07-08 21:53:58 | FieldRenderer3DPerformance | FakeField01_Draw3D_RepeatedOrbitFrames_StayWithinBudget | 1804.3 | — | — | — | — | — | 4000.0 | — | eight-frame orbit sweep |
| 2026-07-08 21:54:05 | FieldRenderer3DPerformance | FakeField01_Draw3D_NoSatellite_CompletesWithinBudget | 173.3 | — | — | — | — | — | 480.0 | — |  |
| 2026-07-08 21:54:13 | FieldRenderer3DPerformance | FakeField01_Monolithic3D_SatelliteEnabled_UncachedTiles_CompletesWithinBudget | 102.0 | 100.7 | — | — | — | — | — | — | satellite loading budget=480.0ms; slowdownFactor=1.25 |
| 2026-07-08 21:54:56 | FieldRenderer3DPerformance | TheShop2_Draw3D_NoSatellite_CompletesWithinBudget | 264.8 | — | — | — | — | — | 650.0 | — |  |
| 2026-07-08 21:55:05 | FieldRenderer3DPerformance | FakeField01_Draw3D_ContoursOff_IsFasterThanContoursOn | 185.0 | 175.6 | — | — | — | — | — | — | medianMs contours-on vs contours-off |
| 2026-07-08 21:55:16 | Viewer3DPerformanceGate | FakeField01_ExistingThreeD_ExaggerationChange_RebuildsWithoutGeometryInvalidation | 4894.6 | — | — | 0 | — | — | 8000.0 | — | geoRebuildBudget=0 |
| 2026-07-08 21:58:06 | Viewer3DPerformanceGate | FakeField01_ExistingThreeD_OrbitSequence_StaysWithinBudgetWithoutGeometryRebuilds | 4215.8 | 4566.2 | 67577.7 | 0 | 0 | 0 | 5000.0 | 5300.0 | totalBudget=80000.0ms; steps=16 |
| 2026-07-08 22:00:59 | FieldRenderer3DPerformance | FakeField01_Draw3D_NoSatellite_CompletesWithinBudget | 224.7 | — | — | — | — | — | 480.0 | — |  |
| 2026-07-09 20:01:42 | Viewer3DPerformanceGate | TheShop2_HaulThreeD_OrbitAfterPin_ReusesCommittedSurfaceWithoutRestamping | 1360.7 | 1525.3 | 21976.5 | 0 | 0 | 16 | 1600.0 | 2500.0 | haulBuildBudget=0; haulReuseMin=8; steps=16 |
| 2026-07-09 20:30:56 | Viewer3DPerformanceGate | TheShop2_HaulThreeD_OrbitAfterPin_ReusesCommittedSurfaceWithoutRestamping | 1442.8 | 1739.5 | 23655.4 | 0 | 0 | 16 | 2500.0 | 4000.0 | haulBuildBudget=0; haulReuseMin=8; steps=16 |
| 2026-07-09 21:23:38 | Viewer3DPerformanceGate | TheShop2_HaulThreeD_OrbitAfterPin_ReusesCommittedSurfaceWithoutRestamping | 1722.5 | 1836.7 | 27559.4 | 0 | 0 | 16 | 2500.0 | 4000.0 | haulBuildBudget=0; haulReuseMin=8; steps=16 |
| 2026-07-09 23:16:48 | Viewer3DPerformanceGate | FakeField01_ExistingThreeD_ExaggerationChange_RebuildsWithoutGeometryInvalidation | 4358.9 | — | — | 0 | — | — | 8000.0 | — | geoRebuildBudget=0 |
| 2026-07-09 23:18:09 | Viewer3DPerformanceGate | TheShop2_HaulThreeD_OrbitAfterPin_ReusesCommittedSurfaceWithoutRestamping | 1453.6 | 1537.9 | 19731.2 | 0 | 0 | 16 | 2500.0 | 4000.0 | haulBuildBudget=0; haulReuseMin=8; steps=16 |
| 2026-07-09 23:19:30 | Viewer3DPerformanceGate | FakeField01_ExistingThreeD_OrbitSequence_StaysWithinBudgetWithoutGeometryRebuilds | 4535.2 | 5253.2 | 74556.5 | 0 | 0 | 0 | 5000.0 | 5300.0 | totalBudget=80000.0ms; steps=16 |
| 2026-07-23 05:24:38 | Viewer3DPerformanceGate | TheShop2_HaulThreeD_OrbitAfterPin_ReusesCommittedSurfaceWithoutRestamping | 1574.7 | 2211.8 | 26231.6 | 0 | 0 | 16 | 2500.0 | 4000.0 | haulBuildBudget=0; haulReuseMin=8; steps=16 |
