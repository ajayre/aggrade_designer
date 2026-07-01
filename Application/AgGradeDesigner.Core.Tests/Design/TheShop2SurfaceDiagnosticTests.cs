using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Survey;
using AgGradeDesigner.Core.Tests.TestFixtures;
using SkiaSharp;

namespace AgGradeDesigner.Core.Tests.Design
{
    /// <summary>
    /// Regression tests for surface quality on the Shop 2 AGD fixture.
    /// </summary>
    public sealed class TheShop2SurfaceDiagnosticTests
    {
        static private string Shop2Path => Path.Combine(TestDataPaths.TestDataDirectory, "TheShop2_2ft.agd");

        static private FieldSurfaceSampler BuildSurface()
        {
            AgdImportResult Result = AgdService.Import(Shop2Path);
            ProjectDocument Project = new ProjectDocument
            {
                Survey = Result.Survey,
                DesignParameters = Result.Parameters,
                DesignGrid = Result.Grid,
                SurveySourceFormat = SurveySourceFormat.Agd
            };
            FieldSurfaceSampler Surface = new FieldSurfaceSampler();
            Surface.BuildFromProject(Project, Resolution: 256);
            return Surface;
        }

        /// <summary>
        /// Flood-fills from the raster border across INVALID cells to identify all "exterior"
        /// invalid cells (those connected to the outside).  Any remaining invalid cell is an
        /// interior hole.  Returns each interior-hole cell as (X, Y).
        /// </summary>
        static private List<(int X, int Y)> FindInteriorHoles(FieldSurfaceSampler Surface)
        {
            int W = Surface.SampleWidth;
            int H = Surface.SampleHeight;
            bool[] Exterior = new bool[W * H];
            Queue<int> Q = new Queue<int>();

            // Seed flood fill from every border cell that is invalid.
            for (int Y = 0; Y < H; Y++)
            {
                for (int X = 0; X < W; X++)
                {
                    bool IsBorder = (X == 0) || (X == W - 1) || (Y == 0) || (Y == H - 1);
                    int Idx = Y * W + X;
                    if (IsBorder && !Surface.Valid[Idx] && !Exterior[Idx])
                    {
                        Exterior[Idx] = true;
                        Q.Enqueue(Idx);
                    }
                }
            }

            int[] FDx = { -1, 0, 1, -1, 1, -1, 0, 1 };
            int[] FDy = { -1, -1, -1, 0, 0, 1, 1, 1 };
            while (Q.Count > 0)
            {
                int Cur = Q.Dequeue();
                int CY = Cur / W;
                int CX = Cur % W;
                for (int D = 0; D < 8; D++)
                {
                    int NX = CX + FDx[D];
                    int NY = CY + FDy[D];
                    if ((NX < 0) || (NX >= W) || (NY < 0) || (NY >= H)) continue;
                    int NIdx = NY * W + NX;
                    if (!Surface.Valid[NIdx] && !Exterior[NIdx])
                    {
                        Exterior[NIdx] = true;
                        Q.Enqueue(NIdx);
                    }
                }
            }

            // Any cell that is still invalid and NOT exterior is an interior hole.
            List<(int X, int Y)> Holes = new();
            for (int Y = 0; Y < H; Y++)
                for (int X = 0; X < W; X++)
                {
                    int Idx = Y * W + X;
                    if (!Surface.Valid[Idx] && !Exterior[Idx])
                        Holes.Add((X, Y));
                }
            return Holes;
        }

        /// <summary>
        /// Confirms the test file is actually found (guards against silent skip).
        /// </summary>
        [Fact]
        public void TestFile_IsFound()
        {
            Assert.True(File.Exists(Shop2Path), $"Test file not found: {Shop2Path}");
        }

        /// <summary>
        /// Verifies the existing surface on Shop 2 has enough real-world relief to be visible at true scale.
        /// </summary>
        [Fact]
        public void ExistingSurface_HasMeaningfulRelief()
        {
            if (!File.Exists(Shop2Path))
                return;

            FieldSurfaceSampler Surface = BuildSurface();
            Surface.ComputeScalarRange(FieldContentView.Existing, out double VMin, out double VMax);
            double Range = VMax - VMin;

            Assert.True(Range >= 0.3,
                $"Expected at least 0.3 m existing relief on Shop 2, got {Range:F3} m ({VMin:F3}–{VMax:F3}).");
        }

        /// <summary>
        /// Verifies that after BuildFromProject there are no interior holes in the Valid mask.
        /// A hole is an invalid cell that is fully enclosed by valid cells (not reachable from
        /// the raster border by walking through invalid cells).
        /// </summary>
        [Fact]
        public void Surface_HasNoInteriorHoles()
        {
            if (!File.Exists(Shop2Path))
                return;

            FieldSurfaceSampler Surface = BuildSurface();
            List<(int X, int Y)> Holes = FindInteriorHoles(Surface);

            if (Holes.Count > 0)
            {
                // Report first 20 hole positions to help diagnose.
                string Sample = string.Join(", ", Holes.Take(20).Select(H => $"({H.X},{H.Y})"));
                Assert.Fail($"Found {Holes.Count} interior holes in Valid mask. First 20: {Sample}");
            }
        }

        /// <summary>
        /// Builds the 2D colormap bitmap for each view and verifies that no transparent (alpha=0)
        /// pixels appear inside the field's valid region.  This catches rendering holes that are
        /// independent of the Valid mask (e.g. bilinear NaN propagation, colorscale bugs).
        /// </summary>
        [Fact]
        public void ColormapBitmap_HasNoTransparentInteriorPixels()
        {
            if (!File.Exists(Shop2Path))
                return;

            FieldSurfaceSampler Surface = BuildSurface();

            foreach (FieldContentView View in new[] { FieldContentView.Existing, FieldContentView.Proposed })
            {
                double VMin = View == FieldContentView.Existing ? Surface.MinElevation : Surface.MinElevation;
                double VMax = View == FieldContentView.Existing ? Surface.MaxElevation : Surface.MaxElevation;

                using SKBitmap Bmp = PlanViewColorMapBuilder.Build(
                    Surface, View, VMin, VMax, ApplyHillshade: false,
                    SupersampleFactor: 1);  // 1x for speed: 1 pixel per sample cell

                int W = Bmp.Width;
                int H = Bmp.Height;

                // Flood-fill from border TRANSPARENT pixels to find all exterior transparent pixels.
                bool[] Exterior = new bool[W * H];
                Queue<int> Q = new Queue<int>();
                for (int Y = 0; Y < H; Y++)
                {
                    for (int X = 0; X < W; X++)
                    {
                        bool IsBorder = (X == 0) || (X == W - 1) || (Y == 0) || (Y == H - 1);
                        int Idx = Y * W + X;
                        if (IsBorder && Bmp.GetPixel(X, Y).Alpha == 0 && !Exterior[Idx])
                        {
                            Exterior[Idx] = true;
                            Q.Enqueue(Idx);
                        }
                    }
                }

                int[] FDx = { -1, 0, 1, -1, 1, -1, 0, 1 };
                int[] FDy = { -1, -1, -1, 0, 0, 1, 1, 1 };
                while (Q.Count > 0)
                {
                    int Cur = Q.Dequeue();
                    int CY = Cur / W;
                    int CX = Cur % W;
                    for (int D = 0; D < 8; D++)
                    {
                        int NX = CX + FDx[D];
                        int NY = CY + FDy[D];
                        if ((NX < 0) || (NX >= W) || (NY < 0) || (NY >= H)) continue;
                        int NIdx = NY * W + NX;
                        if (Bmp.GetPixel(NX, NY).Alpha == 0 && !Exterior[NIdx])
                        {
                            Exterior[NIdx] = true;
                            Q.Enqueue(NIdx);
                        }
                    }
                }

                // Any transparent pixel not reachable from the border is an interior hole.
                List<(int X, int Y)> Holes = new();
                for (int Y = 0; Y < H; Y++)
                    for (int X = 0; X < W; X++)
                    {
                        int Idx = Y * W + X;
                        if (Bmp.GetPixel(X, Y).Alpha == 0 && !Exterior[Idx])
                            Holes.Add((X, Y));
                    }

                if (Holes.Count > 0)
                {
                    string Sample = string.Join(", ", Holes.Take(20).Select(H => $"({H.X},{H.Y})"));
                    Assert.Fail($"{View}: {Holes.Count} interior transparent pixels in colormap. First 20: {Sample}");
                }
            }
        }

        /// <summary>
        /// Flood-fills the valid region from the largest connected component and verifies every
        /// other isolated valid island is smaller than MAX_ISLAND_CELLS cells.
        /// Isolated islands create disconnected terrain patches that look like glitches in 3D.
        /// </summary>
        [Fact]
        public void ValidRegion_HasNoIsolatedIslands()
        {
            if (!File.Exists(Shop2Path))
                return;

            FieldSurfaceSampler Surface = BuildSurface();
            int W = Surface.SampleWidth;
            int H = Surface.SampleHeight;

            // Find all connected components of the VALID region using 4-connectivity.
            int[] ComponentId = new int[W * H];
            Array.Fill(ComponentId, -1);
            List<int> ComponentSizes = new();

            for (int StartY = 0; StartY < H; StartY++)
            {
                for (int StartX = 0; StartX < W; StartX++)
                {
                    int StartIdx = StartY * W + StartX;
                    if (!Surface.Valid[StartIdx] || ComponentId[StartIdx] >= 0)
                        continue;

                    // BFS to find entire connected component.
                    int CompId = ComponentSizes.Count;
                    ComponentSizes.Add(0);
                    Queue<int> Q = new();
                    Q.Enqueue(StartIdx);
                    ComponentId[StartIdx] = CompId;

                    while (Q.Count > 0)
                    {
                        int Cur = Q.Dequeue();
                        ComponentSizes[CompId]++;
                        int CY = Cur / W;
                        int CX = Cur % W;
                        foreach ((int Dx, int Dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
                        {
                            int NX = CX + Dx, NY = CY + Dy;
                            if ((NX < 0) || (NX >= W) || (NY < 0) || (NY >= H)) continue;
                            int NIdx = NY * W + NX;
                            if (Surface.Valid[NIdx] && ComponentId[NIdx] < 0)
                            {
                                ComponentId[NIdx] = CompId;
                                Q.Enqueue(NIdx);
                            }
                        }
                    }
                }
            }

            int MaxSize = ComponentSizes.Max();
            const int MAX_ISLAND_CELLS = 50; // islands larger than this are visually disturbing
            List<string> Problems = new();
            for (int C = 0; C < ComponentSizes.Count; C++)
            {
                if (ComponentSizes[C] == MaxSize) continue;
                if (ComponentSizes[C] > MAX_ISLAND_CELLS)
                    Problems.Add($"component {C}: {ComponentSizes[C]} cells");
            }

            if (Problems.Count > 0)
                Assert.Fail($"Valid region has {ComponentSizes.Count - 1} isolated islands. " +
                            $"Islands > {MAX_ISLAND_CELLS} cells: {string.Join(", ", Problems)}");
        }

        /// <summary>
        /// Verifies that proposed-surface elevation values at the eastern and NW fringe do not
        /// deviate from the field average by more than a loose bound (catches runaway extrapolation).
        /// </summary>
        [Fact]
        public void ProposedSurface_FringeCells_HaveReasonableValues()
        {
            if (!File.Exists(Shop2Path))
                return;

            FieldSurfaceSampler Surface = BuildSurface();

            // Compute mean proposed elevation from all valid cells.
            double Sum = 0;
            int Count = 0;
            for (int I = 0; I < Surface.Valid.Length; I++)
            {
                if (!Surface.Valid[I]) continue;
                Sum += Surface.GetScalarAtIndex(FieldContentView.Proposed, I);
                Count++;
            }
            double Mean = Sum / Math.Max(1, Count);

            // Check fringe cells (adjacent to any invalid cell) specifically.
            double MaxFringeDev = 0;
            string? WorstCell = null;
            int W = Surface.SampleWidth;
            int H = Surface.SampleHeight;

            for (int Y = 0; Y < H; Y++)
            {
                for (int X = 0; X < W; X++)
                {
                    int Idx = Y * W + X;
                    if (!Surface.Valid[Idx]) continue;

                    bool IsFringe = false;
                    foreach ((int Dx, int Dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
                    {
                        int NX = X + Dx, NY = Y + Dy;
                        if ((NX < 0) || (NX >= W) || (NY < 0) || (NY >= H) || !Surface.Valid[NY * W + NX])
                        {
                            IsFringe = true;
                            break;
                        }
                    }
                    if (!IsFringe) continue;

                    double Val = Surface.GetScalarAtIndex(FieldContentView.Proposed, Idx);
                    double Dev = Math.Abs(Val - Mean);
                    if (Dev > MaxFringeDev)
                    {
                        MaxFringeDev = Dev;
                        WorstCell = $"({X},{Y}) val={Val:F4} mean={Mean:F4}";
                    }
                }
            }

            Assert.True(MaxFringeDev <= 0.5,
                $"Proposed fringe cell deviates {MaxFringeDev:F3} m from field mean. Worst: {WorstCell}");
        }

        /// <summary>
        /// Verifies that existing-surface neighbour deltas are within a reasonable bound.
        /// </summary>
        [Fact]
        public void ExistingSurface_HasNoSteepNeighbourDeltas()
        {
            if (!File.Exists(Shop2Path))
                return;

            FieldSurfaceSampler Surface = BuildSurface();
            double MaxDelta = 0;
            string? WorstLocation = null;
            int W = Surface.SampleWidth;
            int H = Surface.SampleHeight;

            for (int Y = 1; Y < H - 1; Y++)
            {
                for (int X = 1; X < W - 1; X++)
                {
                    int Idx = Y * W + X;
                    if (!Surface.Valid[Idx]) continue;
                    double Centre = Surface.GetScalarAtIndex(FieldContentView.Existing, Idx);
                    foreach ((int Dx, int Dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
                    {
                        int NIdx = (Y + Dy) * W + (X + Dx);
                        if (!Surface.Valid[NIdx]) continue;
                        double Delta = Math.Abs(Centre - Surface.GetScalarAtIndex(FieldContentView.Existing, NIdx));
                        if (Delta > MaxDelta)
                        {
                            MaxDelta = Delta;
                            WorstLocation = $"({X},{Y})→({X + Dx},{Y + Dy})";
                        }
                    }
                }
            }

            // Existing terrain may have real relief, but individual cell-to-cell deltas > 0.5m
            // in a 2-ft AGD grid are extremely unlikely to be real terrain and indicate fringe artefacts.
            Assert.True(MaxDelta <= 0.5,
                $"Existing surface neighbour delta {MaxDelta:F3} m at {WorstLocation} exceeds 0.5 m.");
        }

        /// <summary>
        /// Simulates DrawMesh3D's Step=1 quad iteration and counts quads skipped because a
        /// corner is invalid whose quad centre lies inside the field boundary.  Such skips
        /// create visible interior holes in the 3D topo mesh.
        /// </summary>
        [Fact]
        public void MeshStep1_HasNoSkippedInteriorQuads()
        {
            if (!File.Exists(Shop2Path))
                return;

            FieldSurfaceSampler Surface = BuildSurface();

            const int Step = 1;  // mirrors FieldRenderer.DrawMesh3D with MESH_STEP_THRESHOLD raised
            int W = Surface.SampleWidth;
            int H = Surface.SampleHeight;

            double LatSpan = Surface.MaxLat - Surface.MinLat;
            double LonSpan = Surface.MaxLon - Surface.MinLon;

            int InteriorSkips = 0;
            List<string> Examples = new();

            for (int Sy = 0; Sy < H - Step; Sy += Step)
            {
                for (int Sx = 0; Sx < W - Step; Sx += Step)
                {
                    int Idx00 = Sy * W + Sx;
                    int Idx10 = Sy * W + Sx + Step;
                    int Idx11 = (Sy + Step) * W + Sx + Step;
                    int Idx01 = (Sy + Step) * W + Sx;

                    if (Surface.Valid[Idx00] && Surface.Valid[Idx10] &&
                        Surface.Valid[Idx11] && Surface.Valid[Idx01])
                        continue;   // quad will be rendered — no skip

                    // Quad is skipped.  Check whether its centre is inside the field boundary.
                    double CentLat = Surface.MaxLat - LatSpan * (Sy + Step * 0.5) / (H - 1);
                    double CentLon = Surface.MinLon + LonSpan * (Sx + Step * 0.5) / (W - 1);

                    if (Surface.BoundaryLatLon == null) continue;
                    if (!AgGradeDesigner.Core.Geo.BoundaryMask.IsInsideBoundaryLatLon(
                            CentLat, CentLon, Surface.BoundaryLatLon))
                        continue;   // skipped quad is outside field — expected

                    InteriorSkips++;
                    if (Examples.Count < 10)
                    {
                        bool V00 = Surface.Valid[Idx00], V10 = Surface.Valid[Idx10],
                             V11 = Surface.Valid[Idx11], V01 = Surface.Valid[Idx01];
                        Examples.Add($"quad({Sx},{Sy}) valid=({V00},{V10},{V11},{V01}) centre=({CentLat:F6},{CentLon:F6})");
                    }
                }
            }

            if (InteriorSkips > 0)
                Assert.Fail($"DrawMesh3D Step=2 skips {InteriorSkips} quads inside field boundary. " +
                            $"First examples:\n{string.Join("\n", Examples)}");
        }

        /// <summary>
        /// Saves full-resolution colormap PNGs for Existing and Proposed to the system Temp
        /// folder so they can be opened and visually inspected.  Always "passes" — the output
        /// file paths are included in the failure message for easy access.
        /// </summary>
        [Fact]
        public void SaveColormapPngs_ForVisualInspection()
        {
            if (!File.Exists(Shop2Path))
                return;

            FieldSurfaceSampler Surface = BuildSurface();
            string TempDir = Path.GetTempPath();

            foreach (FieldContentView View in new[] { FieldContentView.Existing, FieldContentView.Proposed })
            {
                using SKBitmap Bmp = PlanViewColorMapBuilder.Build(
                    Surface, View, Surface.MinElevation, Surface.MaxElevation,
                    ApplyHillshade: true, SupersampleFactor: PlanViewColorMapBuilder.FullSupersampleFactor);

                // Encode raw RGBA to PNG via SkiaSharp.
                using SKImage Img = SKImage.FromBitmap(Bmp);
                using SKData Data = Img.Encode(SKEncodedImageFormat.Png, 100);
                string OutPath = Path.Combine(TempDir, $"TheShop2_{View}.png");
                File.WriteAllBytes(OutPath, Data.ToArray());
                Console.WriteLine($"[SaveColormapPngs] Saved {View} colormap → {OutPath}");
            }

            // Also dump a text map of Valid cells (1=valid, 0=invalid) at 1:8 scale.
            int Scale = 8;
            int TW = (Surface.SampleWidth + Scale - 1) / Scale;
            int TH = (Surface.SampleHeight + Scale - 1) / Scale;
            System.Text.StringBuilder Sb = new();
            for (int Ty = 0; Ty < TH; Ty++)
            {
                for (int Tx = 0; Tx < TW; Tx++)
                {
                    int Sx = Tx * Scale;
                    int Sy = Ty * Scale;
                    int Idx = Math.Min(Sy, Surface.SampleHeight - 1) * Surface.SampleWidth +
                              Math.Min(Sx, Surface.SampleWidth - 1);
                    Sb.Append(Surface.Valid[Idx] ? '.' : ' ');
                }
                Sb.AppendLine();
            }
            string MapPath = Path.Combine(TempDir, "TheShop2_ValidMask.txt");
            File.WriteAllText(MapPath, Sb.ToString());
            Console.WriteLine($"[SaveColormapPngs] Saved valid mask map → {MapPath}");
        }

        /// <summary>
        /// Verifies the proposed surface has no steep neighbour-to-neighbour elevation jumps.
        /// </summary>
        [Fact]
        public void ProposedSurface_HasNoSteepBoundaryFringe()
        {
            if (!File.Exists(Shop2Path))
                return;

            FieldSurfaceSampler Surface = BuildSurface();

            int ValidCount = Surface.Valid.Count(V => V);
            Assert.True(ValidCount > 5000,
                $"Expected a contiguous surface, got only {ValidCount} valid samples.");

            double MaxDelta = 0;
            string? WorstLocation = null;
            for (int Y = 1; Y < Surface.SampleHeight - 1; Y++)
            {
                for (int X = 1; X < Surface.SampleWidth - 1; X++)
                {
                    int Idx = Y * Surface.SampleWidth + X;
                    if (!Surface.Valid[Idx]) continue;

                    double Centre = Surface.GetScalarAtIndex(FieldContentView.Proposed, Idx);
                    foreach ((int Dx, int Dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
                    {
                        int NIdx = (Y + Dy) * Surface.SampleWidth + (X + Dx);
                        if (!Surface.Valid[NIdx]) continue;
                        double Delta = Math.Abs(Centre - Surface.GetScalarAtIndex(FieldContentView.Proposed, NIdx));
                        if (Delta > MaxDelta)
                        {
                            MaxDelta = Delta;
                            WorstLocation = $"({X},{Y})→({X + Dx},{Y + Dy})";
                        }
                    }
                }
            }

            Assert.True(MaxDelta <= 0.15,
                $"Proposed surface neighbour delta {MaxDelta:F3} m at {WorstLocation} exceeds 0.15 m.");
        }
    }
}
