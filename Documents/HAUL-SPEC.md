# Haul Builder Specification **[0.0.1]**

**[0.0.2]** Every normative paragraph carries a bold marker **`[<S>.<U>.<P>]`** where **`<S>`** is the section, **`<U>`** is the subsection (**`0`** for body text before the first **`###`**), and **`<P>`** is the 1-based paragraph index within that group.

**[0.0.3]** This document specifies integration of the **Haul Builder** library into **AgGrade Designer**: how the application loads, configures, and consumes the vendored **`HaulBuilder`** assembly to produce haul-planning results from field **design** data.

**[0.0.4]** - **Project name:** AgGrade Designer — Haul Builder
**[0.0.5]** - **Document type:** Application-layer integration specification (skeleton)
**[0.0.6]** - **Language / implementation:** C# (.NET 8), Avalonia UI, MVVM — application integration source **shall** follow **ANDY-CSHARPCODINGSTYLEGUIDE.md**; the vendored **`HaulBuilder`** assembly is a managed library consumed by reference
**[0.0.7]** - **Target platforms:** Windows, Linux, macOS (same as **AGGRADE-DESIGNER-SPEC-1.0.md**)
**[0.0.8]** - **Scope:** Integration of the **Haul Builder** pipeline into **AgGrade Designer** — loading the reference library, mapping **project** / **design** data to **`HaulBuilder`** inputs, running haul analysis, presenting results in a new **Haul** field content view (**§7**), exposing a **Haul** view-selection button (**§7.2**), persisting **haul data** in **`.agdesign`** project files (**§7.5**), and user-configurable **Haul** defaults in the **Settings** dialog (**§7.6**). Detailed haul generation workflow, API wire layout for **haul data** persistence, **Phase 2** **Haul** overlays (**§7.4.38**), and extended test obligations **may** be added in later revisions of this document.
**[0.0.9]** - **Relationship to baseline:** **AGGRADE-DESIGNER-SPEC-1.0.md** remains the authoritative contract for the core application (viewer, survey, design, project files, **AgGrade Designer §7** UI chrome). This document **extends** the product with **Haul Builder** integration and **extends** the **`.agdesign`** project schema (**AgGrade Designer Appendix A**) with embedded **haul data** (**§7.5**, **Appendix A §A.2**). It **shall not** change baseline survey or design export wire formats (**AgGrade Designer §9**, **Appendix A** survey and design sections) unless a future revision of either document explicitly says otherwise.
**[0.0.10]** - **Relationship to AI features:** **AI-FEATURES-SPEC.md** defines the existing **Settings** dialog layout, **user settings file** persistence (**AI Features §4.17**, **§5.6**), **OK** / **Cancel** session semantics (**AI Features §5.7**), and global **Units** preference (**AI Features §4.18**). This document **shall** add a **Haul** settings section using the same patterns; **Haul** settings **shall not** be stored in **`.agdesign`** project files.
**[0.0.11]** - **Assumed knowledge:** Readers **shall** already know **AGGRADE-DESIGNER-SPEC-1.0.md** terminology (**§6**), project model (**§4**, **`ProjectDocument`**), design model (**§5**, **`DesignGrid`**, **`DesignParameters`**), UI layout (**§7**), and **AI-FEATURES-SPEC.md** **Settings** / **Units** behaviour (**AI Features §4.17**–**§4.18**, **§5.7**). In this document, bare **§N** refers to **this document**; **AgGrade Designer §N** or **AGGRADE-DESIGNER-SPEC-1.0.md §N** refers to the baseline spec; **AI Features §N** refers to **AI-FEATURES-SPEC.md**.
**[0.0.12]** - **Related documents:** **ANDY-SPECSTYLEGUIDE.md** (authoring rules), **ANDY-CSHARPCODINGSTYLEGUIDE.md** (C# source style), **AGGRADE-DESIGNER-SPEC-1.0.md** (baseline application specification), **AI-FEATURES-SPEC.md** (**Settings** dialog and **user settings file**). Informative haul-algorithm reference: **`AgGrade/AI Haul Algorithm III/LAND-LEVELING-SPEC-2.md`** (cited by the baseline spec for AGD and cut/fill semantics).

**[0.0.13]** **Version axes:**

**[0.0.14]** 1. **Specification document** version (this markdown file; semver in **§0.1**).
**[0.0.15]** 2. **Application** version (shipping product semver; distinct from this document).
**[0.0.16]** 3. **Haul Builder** library build (the vendored **`HaulBuilder.dll`**; distinct from this document).

**[0.0.17]** Incrementing one version axis **does not** imply incrementing another.

**[0.0.18]** **Shipping artifacts (reference library):**

**[0.0.19]** The reference **Haul Builder** managed assembly and its dependencies **shall** be vendored under **`Application/DLLs/`**:

**[0.0.20]** - **`Application/DLLs/HaulBuilder.dll`** — managed **Haul Builder** library (assembly name **`HaulBuilder`**; namespace **`HaulBuilder`**).
**[0.0.21]** - **`Application/DLLs/HaulBuilder.xml`** — XML documentation for the public **`HaulBuilder`** API. Until normative API text is written in this document, integrators **shall** treat **`HaulBuilder.xml`** as the authoritative catalog of exported types and members.
**[0.0.22]** - **`Application/DLLs/haul_kdtree.dll`** — native dependency used by **Haul Builder** for spatial indexing (**`HaulBuilder.NativeKd`**, **`HaulBuilder.CellKdIndex`**).

**[0.0.23]** **Phased delivery:**

**[0.0.24]** 1. **Phase 1** — **Haul** field content view and button (**§7.1**–**§7.3**), top-down **2D** **bitmap** presentation with pan/zoom and **route scrub** (**§7.4**), **Generate haul** command, **Haul settings** **Phase 1** controls (**§7.6.38**), **haul data** project persistence (**§7.5**).
**[0.0.25]** 2. **Phase 2** — **Haul** settings **Advanced** expander (**§7.6.34**), **Flow arrow spacing** (**§7.6.36**), extended **Haul** overlays and **DISPLAY** decoration rules (**§7.4.38**).

**[0.0.26]** **Phase 1** **shall not** be withheld until **Phase 2** features exist. Later phases **shall not** remove **Phase 1** settings without a spec revision.

### 0.1 Specification revision history

**[0.1.1]** | Version | Date | Change |
|---|---|---|
| **0.1.0** | 2026-07-05 | Initial skeleton: metadata, baseline relationship, and **`Application/DLLs/`** shipping artifact locations. |
| **0.2.0** | 2026-07-05 | **Haul** field content view (**§7.1**), **Haul** view button placement and enablement (**§7.2**), automatic switch on haul generation (**§7.3**). |
| **0.3.0** | 2026-07-05 | **Haul data** **`.agdesign`** persistence on **Save** / **Open** (**§7.5**, **Appendix A §A.2**); **Haul** view enabled after project load. |
| **0.4.0** | 2026-07-05 | **Settings** dialog **Haul** section (**§7.6**); display units per global **Units** preference; **`GridSizeFt`** derived from blade width (**§6.2**). |
| **0.5.0** | 2026-07-05 | Complete **Haul settings** catalog (**§7.6**), storage model (**§5.1**), phased delivery (**§0.0.23**), **user settings file** schema (**Appendix B**). |
| **0.6.0** | 2026-07-05 | **Haul** view rendering (**§7.4**): top-down **2D** only, **bitmap**-based; no **3D**, **Vulkan**, or **Skia** haul presentation. |
| **0.7.0** | 2026-07-05 | **Haul** pan/zoom (**§7.4.4**); vertical **route scrub** slider (**§7.4.6**–**§7.4.9**); route scrubbing terminology (**§4.4**). |
| **0.7.1** | 2026-07-05 | Restore design goal **§2.0.7** (bitmap **2D**); reorder **§4** terminology; reserve **§7.4.10** for **Phase 2** overlays. |
| **0.7.2** | 2026-07-05 | **Route position label** above **route scrub** slider (**§4.4.4**, **§7.4.7**); **Phase 2** overlays reserved at **§7.4.11**. |
| **0.8.0** | 2026-07-05 | **Haul route symbology** — cut (**red**), fill (**blue**), GPS track (**§4.5**, **§7.4.6**–**§7.4.9**); renumber route scrub control (**§7.4.10**–**§7.4.14**); **Phase 2** overlays at **§7.4.15**. |
| **0.9.0** | 2026-07-05 | **Cut/fill map** as **Haul** view base layer; base map updates on route **scrub** (**§4.6**, **§7.4.5**, **§7.4.12**). |
| **0.10.0** | 2026-07-05 | Initial **Haul** view after haul generation: **Cut/fill map** (**2D**) plus **first** haul route (**§7.3.4**, **§7.4.13**). |
| **0.11.0** | 2026-07-05 | **Haul scrub frame** commit on **500 ms** scrub pause or pointer release; session **frame cache** and bidirectional background prefetch (**§4.7**, **§7.4.15**–**§7.4.20**). |
| **0.12.0** | 2026-07-05 | Parallel **foreground** frame generation; **partial frame** preemption/resume; initial idle sequential prefetch (**§4.7.5**–**§4.7.7**, **§7.4.19**–**§7.4.25**). |
| **0.13.0** | 2026-07-05 | **Frame cache tick marks** beside **route scrub** slider — **grey** / **green** cache status (**§4.7.8**, **§7.4.27**–**§7.4.29**). |
| **0.14.0** | 2026-07-05 | Discrete **route scrub** slider with frame snap; **Left** / **Right** arrow and **Ctrl**+arrow keyboard scrub (**§7.4.31**–**§7.4.37**). |

**[0.1.2]** This document follows [semantic versioning](https://semver.org/): **MAJOR** = incompatible normative change, **MINOR** = backward-compatible addition, **PATCH** = clarification.

---

## 1. Purpose

**[1.0.1]** **AgGrade Designer** **shall** integrate the vendored **Haul Builder** library (**§0.0.18**–**§0.0.22**) so the user **may** configure default haul parameters in the **Settings** dialog (**§7.6**), run haul-planning analysis against the active field **design**, inspect structured **haul data** in a dedicated **Haul** field content view (**§7.1**), switch to that view using the **Haul** button (**§7.2**) or automatically when haul data is first generated (**§7.3**), and **save** and **reload** **haul data** as part of **`.agdesign`** project files (**§7.5**).

**[1.0.2]** Obligations in this document use **shall**, **should**, **may** as defined in **ANDY-SPECSTYLEGUIDE.md §6**.

---

## 2. Design goals

**[2.0.1]** The implementation **shall provide**:

**[2.0.2]** 1. A fourth field content view — **Haul** (**§7.1**) — alongside **Existing**, **Proposed**, and **Cut/fill map** (**AgGrade Designer §2.4**), for inspecting **haul data** after a **Haul Builder** run.
**[2.0.3]** 2. A **Haul** view-selection button placed directly below the **Cut / Fill** button (**§7.2**), enabled only when **haul data** is present.
**[2.0.4]** 3. Automatic activation of the **Haul** view when the user generates **haul data** (**§7.3**), initially showing the **Cut/fill map** (**2D**) with the **first** haul route (**§7.3.4**).
**[2.0.5]** 4. **Haul data** embedded in **`.agdesign`** project files on **Save** and restored on **Open**, with the **Haul** view enabled when haul data is present (**§7.5**).
**[2.0.6]** 5. A **Haul** section in the application **Settings** dialog for user defaults mapped to **`HaulBuilder.HaulBuilderOptions`**, persisted in the **user settings file** (**§7.6**), with labels and inputs shown in **imperial** or **metric** units according to the global **Units** preference (**AI Features §4.18**).
**[2.0.7]** 6. A top-down **2D** **bitmap** **Haul** presentation in the **field viewer** (**§7.4**), with the **cut/fill map** as the base layer and each haul route drawn using **cut**, **fill**, and **GPS track** symbology (**§4.5**–**§4.6**, **§7.4.5**–**§7.4.9**), distinct from the **Vulkan** / **Skia** surface renderers used for **Existing**, **Proposed**, and **Cut/fill map**.
**[2.0.8]** 7. **2D** pan and zoom plus a discrete **route scrub** slider with frame snap, **route position label** (**§4.4.4**), **frame cache tick marks**, keyboard step scrubbing (**§7.4.31**–**§7.4.37**), parallel **foreground** **Haul scrub frame** generation on **scrub frame commit**, resumable **partial frame** checkpoints, and background prefetch (**§7.4.4**, **§7.4.10**–**§7.4.37**).

---

## 3. Non-goals

**[3.0.1]** The following are **explicitly out of scope** for **Haul settings** and **shall not** appear in the **Settings** dialog **Haul** section:

**[3.0.2]** 1. **Grid size** / **`GridSizeFt`** — derived from **blade width** at run time (**§6.2**).
**[3.0.3]** 2. **Cut/fill ratio** — taken from the project **design** (**§6.1.2**).
**[3.0.4]** 3. CLI file outputs — **`OutputPngPath`**, **`SequenceCsvPath`**, **`PairsCsvPath`**, **`GuidanceCsvPath`**, animation frame directories, pivot PNG/GIF paths, and related **`*Path`** / **`*DirPath`** members (**§6.1.4**).
**[3.0.5]** 4. **`ExcludePolygonSpecs`** — exclusion geometry **shall** come from project **tool data** (**TOOLS-SPEC.md**) or future project-owned obstacles, not user settings.
**[3.0.6]** 5. **`PixelsPerBin`** and other PNG-export-only options — the in-app **Haul** viewer **shall not** depend on export pixel density settings (**§7.4.3**).

### 3.1 Haul view non-goals

**[3.1.1]** The following are **explicitly out of scope** for the **Haul** field content view:

**[3.1.2]** 1. A **3D** or perspective **Haul** presentation — **Haul** **shall not** be rendered in **3D** orbit mode (**§7.1.3**, **§7.4.1**).
**[3.1.3]** 2. **Vulkan** or **Skia** mesh/vector rendering for **Haul** — the **Haul** view **shall not** use **`VulkanFieldRenderer`**, **`FieldRenderer`** relief meshes, or other **Skia**-based surface draw paths used for topo views (**§7.4.2**).
**[3.1.4]** 3. Vertical exaggeration, orbit camera, or relief shading for **Haul** — **Haul** is a flat plan-view **bitmap** only (**§7.4.1**).
**[3.1.5]** 4. **Existing** or **Proposed** topo, **satellite map**, or an empty field background as the **Haul** view base — the **Haul** view **shall** use the **cut/fill map** base layer only (**§7.4.5**).

## 4. Terminology

### 4.1 Haul field content view

**[4.1.1]** **Haul** is a **field content view** — the same class of control as **Existing**, **Proposed**, and **Cut/fill map** (**AgGrade Designer §2.4**). When **Haul** is active, the **field viewer** (**AgGrade Designer §2**) **shall** present **haul data** (**§4.2**) as a top-down **2D** **bitmap** (**§7.4**): a **cut/fill map** base layer (**§4.6**, **§7.4.5**) with **haul route symbology** overlaid (**§4.5**), not as existing topology, proposed topology, or a **3D** surface.

**[4.1.2]** The **VIEW** section label for this view **shall** be **Haul** (**§7.2.1**).

**[4.1.3]** **Haul bitmap** — the raster image (or layered rasters) displayed in the **Haul** view, composed of the **haul cut/fill base map** (**§4.6**) under **haul route symbology** (**§4.5**, **§7.4.6**–**§7.4.9**) and drawn with **bitmap** APIs. It **shall not** be produced by the **Vulkan** or **Skia** topo render pipeline. The displayed **Haul bitmap** **shall** reflect the current **committed** or **cached** **Haul scrub frame** (**§4.7**, **§7.4.15**–**§7.4.20**).

### 4.2 Haul data

**[4.2.1]** **Haul data** is the structured in-memory result of a successful **Haul Builder** pipeline run (**§6**) for the active project. **Haul data** **shall** include whatever typed outputs the integration layer retains from **`HaulBuilder.HaulBuilderResult`** and related render payloads. When present, **haul data** **shall** be persisted in the **`.agdesign`** project file (**§7.5**, **Appendix A §A.2**); detailed wire layout **shall** be specified in **Appendix A §A.2**.

**[4.2.2]** The project **has haul data** when **haul data** is present and valid for display. When the project **has no haul data**, the **Haul** view button **shall** be **disabled** (**§7.2.3**) and the **Haul** field content view **shall not** be selectable.

### 4.3 Haul settings

**[4.3.1]** **Haul settings** are the user’s default **`HaulBuilder.HaulBuilderOptions`** values edited in the **Settings** dialog **Haul** section (**§7.6**). **Haul settings** **shall** persist in the **user settings file** (**AI Features §4.17**), **shall not** be stored in **`.agdesign`** project files, and **shall** apply whenever the user **generates haul data** until changed and saved again.

**[4.3.2]** **Grid spacing** is **not** a **Haul setting**. On each haul run the application **shall** set **`HaulBuilderOptions.GridSizeFt`** from the configured **blade width** (**§6.2**, **§7.6.10**).

### 4.4 Route scrubbing

**[4.4.1]** **Scrub** (verb) — to move forward or backward through the ordered **haul route sequence** and inspect haul progression step by step, using the **route scrub** slider (**§7.4.31**), keyboard shortcuts (**§7.4.32**), or both. **Scrubbing** has the same meaning as in video editing: the user selects a discrete frame position along a sequential axis. The **route scrub index** and **route position label** **shall** follow the active control; the **Haul scrub frame** (**§4.7**) **shall** be **committed** per **§7.4.15** (pointer drag) or **§7.4.32** (keyboard).

**[4.4.2]** **Haul route sequence** — the ordered haul steps derived from **haul data**, in route execution order (from **`HaulBuilder.HaulBuilderResult`** — for example route-ordered **`Pass`** entries from **`PassResult`** / **`Route`**, or **`CutFillPairs`** sorted by **`Order`**). The sequence **shall** have a defined step count **`N ≥ 1`** used to bound the **route scrub** slider (**§7.4.10**–**§7.4.14**, **§7.4.31**).

**[4.4.3]** **Route scrub index** — the zero-based integer **`k`** where **`0 ≤ k < N`** selecting which prefix of the **haul route sequence** is visible in the **Haul bitmap**. **`k = 0`** **shall** show the **Cut/fill map** as in the **Cut/fill map** field content view in **2D** (**AgGrade Designer §2.4**) with the **first** haul route drawn (**§7.3.4**, **§7.4.13**); the **haul cut/fill base map** (**§4.6**) **shall** match that **Cut/fill map** presentation with **no** cumulative haul adjustments applied yet. **`k = N − 1`** **shall** show the fully completed haul plan after all route steps. For **`1 ≤ k < N − 1`**, the **haul cut/fill base map** **shall** reflect cumulative haul effects through step **`k`**, and the **Haul bitmap** **shall** show haul routes through step **`k`** inclusive.

**[4.4.4]** **Route position label** — the read-only text shown **directly above** the **route scrub** slider (**§7.4.10**, **§7.4.11**) reporting the user-visible position in the **haul route sequence**. It **shall** use the format **`{current}/{total}`** (for example **`56/2451`**): **`current`** is the **1-based** route step number **`k + 1`**, and **`total`** is **`N`**. The label **shall** update whenever the **route scrub index** changes (**§7.4.12**, **§7.4.32**).

### 4.5 Haul route symbology

**[4.5.1]** **Haul route symbology** — the three visual layers used to present each haul route on the **Haul bitmap** (**§7.4.6**–**§7.4.9**):

**[4.5.2]** 1. **Cut sections** — portions of the route where material is **cut** (**§7.4.7**).
**[4.5.3]** 2. **Fill sections** — portions of the route where material is **filled** (**§7.4.8**).
**[4.5.4]** 3. **GPS track** — the machine path polyline and vertex markers for the route (**§7.4.9**).

### 4.6 Haul cut/fill base map

**[4.6.1]** **Haul cut/fill base map** — the bottom layer of a **Haul scrub frame** (**§4.7**): a **cut/fill map** showing cut and fill depths for the active project **design** after applying cumulative haul effects through the frame’s **route scrub index** (**§4.4.3**). At **`k = 0`**, it **shall** match the **Cut/fill map** field content view in **2D** (**AgGrade Designer §2.4**) with **no** haul adjustments. It **shall** use the same cut/fill depth presentation as that view for all **`k`**. It **shall** be generated and displayed only as part of a **committed** or **cached** **Haul scrub frame** (**§7.4.15**–**§7.4.25**).

### 4.7 Haul scrub frame cache

**[4.7.1]** **Haul scrub frame** — the complete **Haul bitmap** presentation for one **route scrub index** **`k`**: the **haul cut/fill base map** (**§4.6**) for **`k`** composited with **haul route symbology** (**§4.5**) for haul routes through step **`k`** inclusive (**§4.4.3**).

**[4.7.2]** **Haul scrub frame cache** — a session-scoped, in-memory store mapping **route scrub index** **`k`** to a completed **Haul scrub frame** (**§5.1.2**). The cache **shall** be cleared when **haul data** is replaced or cleared (**§7.3.3**). Clearing the cache **shall** reset all **frame cache tick marks** to **grey** (**§7.4.29**).

**[4.7.3]** **Scrub frame commit** — the moment the application **shall** generate (if not cached) and display the **Haul scrub frame** for the current **route scrub index**, per **§7.4.15**–**§7.4.20**.

**[4.7.4]** **Scrub pause** — while the user is dragging the **route scrub** slider, the **route scrub index** remains unchanged for more than **`500 ms`** (**§7.4.16**). A **scrub pause** **shall** trigger **scrub frame commit** even if the pointer button remains pressed.

**[4.7.5]** **Partial frame generation state** — a resumable checkpoint for an in-progress **Haul scrub frame** at index **`k`** that records whatever simulation and raster work has completed so far. **Partial frame generation state** **shall** be stored when **foreground frame generation** (**§4.7.7**) for **`k`** is **preempted** per **§7.4.20** and **may** be resumed later by the **foreground** or **prefetch** workers.

**[4.7.6]** **Slider interaction** — the user is **interacting with the slider** when the pointer button is **down** on the **route scrub** slider **or** the **route scrub index** has changed within the current drag without yet reaching **scrub frame commit** (**§7.4.15**). The **prefetch worker** (**§7.4.23**) **shall not** run while the user is **interacting with the slider**.

**[4.7.7]** **Foreground frame generation** — **Haul scrub frame** generation started by **scrub frame commit** (**§7.4.19**). **Foreground frame generation** **shall** take precedence over **prefetch worker** activity (**§7.4.23**).

**[4.7.8]** **Frame cache tick mark** — a small **horizontal** tick displayed **next to** the **route scrub** slider at the position corresponding to one **route scrub index** **`k`**. It **shall** indicate whether that **Haul scrub frame** is present in the **Haul scrub frame cache** (**§7.4.27**–**§7.4.29**).

---

## 5. High-level architecture

### 5.1 Configuration and data ownership (normative)

**[5.1.1]** Haul-related state **shall** be split across three stores:

**[5.1.2]** | Store | Contents | Examples |
|---|---|---|
| **User settings file** | Per-user **Haul** defaults (**§7.6**, **Appendix B**) | Scraper capacity, speeds, pivot lat/lon |
| **Project file** ( **`.agdesign`** ) | Field **survey**, **design**, and computed **haul data** (**§7.5**) | **`Haul`** element, **`Design/Parameters`** |
| **Run-time integration** | Ephemeral paths, in-memory inputs, and **Haul** viewer caches | In-memory AGD equivalent, **`Haul scrub frame cache`** (**§4.7.2**), **`Partial frame generation state`** (**§4.7.5**) |

**[5.1.3]** On **Generate haul**, the application **shall** merge **Haul settings** + **`ProjectDocument`** + derived values (**§6.1**, **§6.2**) into **`HaulBuilderOptions`**, run **`HaulBuilderEngine.Run()`**, and store **`HaulBuilderResult`** as **haul data** in the project (**§4.2**).

**[5.1.4]** Values that originate in the project **design** (**Cut/fill ratio**, optional **Main slope direction** for **`HeadingDeg`**) **shall** be read from **`ProjectDocument`** at run time. The **Haul** settings UI **may** show them as read-only informational text when a design is open; they **shall not** be editable in **Haul settings**.

---

## 6. Haul Builder library integration

### 6.1 Options mapping (normative)

**[6.1.1]** When the user **generates haul data**, the application **shall** construct a **`HaulBuilder.HaulBuilderOptions`** instance from (1) the active **`ProjectDocument`** (survey, design, boundary), (2) the persisted **Haul settings** (**§7.6**), and (3) derived values defined in **§6.2**. The application **shall** invoke **`HaulBuilder.HaulBuilderEngine.Run()`** and retain the returned **`HaulBuilder.HaulBuilderResult`** as **haul data** (**§4.2**).

**[6.1.2]** **`HaulBuilderOptions.CutFillRatio`** **shall** be taken from the project **design** **Cut/fill ratio** (**AgGrade Designer §5.2.2**). It **shall not** appear in the **Haul** settings UI.

**[6.1.3]** When **Use design main slope direction** is enabled (**§7.6.21**), **`HaulBuilderOptions.HeadingDeg`** **shall** be taken from the project design **Main slope direction** (**AgGrade Designer §5.2.2**). When disabled, **`HeadingDeg`** **shall** come from the user-entered heading in **Haul settings** (**§7.6.21**).

**[6.1.4]** File-path and export-only **`HaulBuilderOptions`** members (**`AgdPath`**, **`*Path`**, **`*DirPath`**, animation outputs) **shall** be supplied by the integration layer from in-memory project data and temporary or internal buffers; they **shall not** appear in **Haul settings**.

### 6.2 Grid spacing (normative)

**[6.2.1]** **`HaulBuilderOptions.GridSizeFt`** **shall not** be user-configurable. On every haul run the application **shall** set **`GridSizeFt`** equal to the configured **blade width** expressed in **feet**, converted from **`HaulBuilderOptions.BladeWidthMm`** (**§7.6.9**).

**[6.2.2]** There **shall** be **no** **Grid size** (or equivalent) control in the **Haul** settings UI.

---

## 7. User interface

**[7.0.1]** This section specifies **Haul Builder** user-interface obligations that extend the baseline **field viewer** and **VIEW** controls (**AgGrade Designer §2.4**, **AgGrade Designer §7.4.8**–**AgGrade Designer §7.4.12**). Bare **§N** in this section refers to **this document**.

### 7.1 Haul field content view (normative)

**[7.1.1]** **AgGrade Designer** **shall** add **Haul** as a fourth **field content view** selectable from the **VIEW** button stack (**AgGrade Designer §2.4.2**).

**[7.1.2]** When **Haul** is the active field content view and the project **has haul data**, the **field viewer** **shall** render that **haul data** as a top-down **2D** **bitmap** per **§7.4**.

**[7.1.3]** When the project **has haul data**, the user **shall** be able to switch among **Existing**, **Proposed**, **Cut/fill map**, and **Haul** using the **VIEW** buttons, subject to each button’s enablement rules. Exactly one field content view **shall** be active at a time (**AgGrade Designer §2.4.2**).

**[7.1.4]** **Haul** is a **2D-only** field content view. When **Haul** is active, the **field viewer** **shall** be in **2D** view mode (**AgGrade Designer §2.0.2**). The application **shall** switch to **2D** automatically when **Haul** becomes active (**§7.2.4**, **§7.3.2**, **§7.5.4**). While **Haul** is active, the **2D / 3D** toggle **shall** be **disabled** so **Haul** is **never** shown in **3D** mode. The right sidebar **shall** show the **HAUL ROUTE** **route scrub** slider and **route position label** (**§7.4.10**, **§7.4.11**) instead of the **vertical exaggeration** control (**AgGrade Designer §2.3.1**).

**[7.1.5]** When the project **has no haul data**, the **Haul** field content view **shall not** be active. If **haul data** is cleared while **Haul** is active, the active field content view **shall** fall back according to **AgGrade Designer §2.4.10** default-view rules for the current project state.

### 7.2 Haul view button (normative)

**[7.2.1]** The application **shall** expose a **Haul** button in the **VIEW** section (**AgGrade Designer §7.4.8**) with the visible label **Haul**.

**[7.2.2]** The **Haul** button **shall** be placed **directly below** the **Cut / Fill** button (**AgGrade Designer §7.4.12**) in the **VIEW** button stack.

**[7.2.3]** The **Haul** button **shall** be **enabled** only when the project **has haul data** (**§4.2.2**). When **haul data** is absent, the button **shall** be **disabled**.

**[7.2.4]** Clicking the **Haul** button while it is **enabled** **shall** activate the **Haul** field content view (**§7.1**), switch the **field viewer** to **2D** mode (**§7.1.4**), deactivate the other **VIEW** buttons, and apply the same active (highlighted) button styling used for **Existing**, **Proposed**, and **Cut / Fill** (**AgGrade Designer §7.4.10**–**§7.4.12**).

### 7.3 Automatic view switch on haul generation (normative)

**[7.3.1]** When the user **generates haul data** — that is, when a **Haul Builder** run completes successfully and the application first retains **haul data** for the active project, or replaces prior **haul data** with a new successful result — the active field content view **shall** switch to **Haul** automatically.

**[7.3.2]** The automatic switch in **§7.3.1** **shall** occur without requiring the user to click the **Haul** button. The **Haul** button **shall** become **enabled** and **shall** show the active highlight when the switch completes. The **field viewer** **shall** switch to **2D** view mode when **Haul** becomes active (**§7.1.4**). The initial **Haul** presentation **shall** follow **§7.3.4**.

**[7.3.3]** Re-running haul generation while **haul data** is already present **shall** refresh **haul data**, **shall** clear the **Haul scrub frame cache** and reset **frame cache tick marks** to **grey** (**§4.7.2**, **§7.4.29**), and **shall** switch to (or remain on) the **Haul** field content view per **§7.3.1**, with the initial **Haul** presentation per **§7.3.4**.

**[7.3.4]** **Initial haul view after generation (normative)** — when a **Haul Builder** run completes successfully and the application activates the **Haul** field content view per **§7.3.1** or **§7.3.3**, the **Haul bitmap** **shall** show:

**[7.3.5]** 1. The **haul cut/fill base map** (**§4.6**) matching the **Cut/fill map** field content view in **2D** (**AgGrade Designer §2.4**) — the same cut/fill depths the user would see with **Cut / Fill** selected and the **field viewer** in **2D** mode, before any haul route effects are applied (**§4.4.3**, **`k = 0`**).

**[7.3.6]** 2. The **first** haul route in the **haul route sequence** (**§4.4.2**) drawn with **haul route symbology** (**§4.5**, **§7.4.6**–**§7.4.9**).

**[7.3.7]** 3. The **route scrub** slider at the **first** step (**`k = 0`**) and the **route position label** reading **`1/N`** (**§7.4.11**, **§7.4.13**). After the **`k = 0`** **Haul scrub frame** is cached and the user is **not** **interacting with the slider** (**§4.7.6**), the **prefetch worker** **shall** begin **initial sequential prefetch** per **§7.4.24**.

### 7.4 Haul view rendering (normative)

**[7.4.1]** **Presentation model** — the **Haul** field content view **shall** be a **top-down** **2D** plan view aligned to the field boundary using the same north-up map model as **Existing** / **Proposed** **2D** mode (**AgGrade Designer §2.1.7**). There **shall** be **no** **3D** orbit, perspective, or relief-mesh presentation of **Haul** content (**§3.1.2**).

**[7.4.2]** **Bitmap renderer (normative)** — **Haul** **shall** be drawn with a **bitmap-based** pipeline: raster image data composited into the **field viewer** viewport (for example **`WriteableBitmap`**, **`RenderTargetBitmap`**, or equivalent Avalonia bitmap APIs). The **Haul** view **shall not** use **`VulkanFieldRenderer`**, **`FieldRenderer`** mesh draw paths, or **Skia** surface tessellation used for **Existing**, **Proposed**, or **Cut/fill map** (**§3.1.3**).

**[7.4.3]** **Bitmap source** — the displayed **Haul bitmap** **shall** be built from **haul data** retained after **`HaulBuilderEngine.Run()`** — including progressive cut/fill state for each **route scrub index** (**§4.4.3**, **§4.6**), any PNG or raster payloads in **`HaulBuilderResult`**, and/or application-generated rasters derived from **`HaulBuilderResult`** grid, flow, and route data. **`HaulBuilderOptions.PixelsPerBin`** **may** influence internal bitmap resolution during generation but **shall not** be a user-facing **Settings** control (**§3.0.6**).

**[7.4.4]** **Pan and zoom (normative)** — while **Haul** is active, the user **shall** be able to **pan** and **zoom** the **Haul bitmap** in the **field viewer** viewport using the same pointer and wheel interactions as topo **2D** views (**AgGrade Designer §2.1.2**, **§2.1.3**). Pan and zoom **shall** affect viewport framing only; they **shall not** change the **route scrub index** (**§4.4.3**). Vertical exaggeration (**AgGrade Designer §2.3.1**) **shall not** apply (**§3.1.4**).

**[7.4.5]** **Cut/fill base map (normative)** — the **Haul** view **shall** use the **cut/fill map** as its **base map**. The **haul cut/fill base map** (**§4.6**) **shall** be a **bitmap** layer showing cut and fill depths for the active project **design** adjusted by cumulative haul effects through the **route scrub index** of the displayed **Haul scrub frame**, using the same cut/fill depth presentation as the **Cut/fill map** field content view in **2D** (**AgGrade Designer §2.4**). At **`k = 0`**, the base layer **shall** be identical to that **Cut/fill map** view with **no** haul adjustments (**§7.3.5**). **Haul route symbology** (**§7.4.6**–**§7.4.9**) **shall** be composited **over** this base layer within each **Haul scrub frame**. The **cut/fill map** base and route overlays **shall** pan and zoom together (**§7.4.4**). **DISPLAY** toggles for topo relief (contours, hillshade, vertical exaggeration) **shall not** apply to **Haul** (**§3.1.4**). The **Haul** view **shall not** substitute **Existing** or **Proposed** topo, **satellite map**, or an empty field background for the **cut/fill map** base (**§3.1.5**).

### Route symbology

**[7.4.6]** **Route presentation (normative)** — each haul route drawn on a **Haul scrub frame** **shall** be shown as **three** composited symbology layers (**§4.5**) **over** the frame’s **haul cut/fill base map** (**§7.4.5**): **cut sections**, **fill sections**, and the **GPS track**. All three layers **shall** be derived from **haul data** for that route (for example cut/fill segment geometry and GPS vertices from **`HaulBuilder.HaulBuilderResult`** route outputs). A **Haul scrub frame** at index **`k`** **shall** include symbology for haul routes through step **`k`** inclusive (**§4.4.3**).

**[7.4.7]** **Cut sections (normative)** — **cut sections** of a haul route **shall** be marked **red** on the **Haul bitmap**.

**[7.4.8]** **Fill sections (normative)** — **fill sections** of a haul route **shall** be marked **blue** on the **Haul bitmap**.

**[7.4.9]** **GPS track (normative)** — the route **GPS track** **shall** be drawn as a **black** polyline connecting track vertices, with a **black** dot at each vertex. The polyline and each dot **shall** have a **white** outline so the track remains readable over **cut** (**red**) and **fill** (**blue**) symbology and over the **haul cut/fill base map** (**§7.4.5**).

### Route scrub control

**[7.4.10]** **Placement** — while **Haul** is the active field content view, the **right sidebar** (**AgGrade Designer §7.5**) **shall** show a **HAUL ROUTE** section containing a **vertical** **route scrub** slider on the right side of the main window chrome. **Directly above** the slider, the section **shall** show the **route position label** (**§4.4.4**, **§7.4.11**). **Next to** the slider, the section **shall** show one **frame cache tick mark** per **route scrub index** (**§7.4.27**). The slider **shall** use vertical orientation (bottom = earlier route steps, top = later route steps), **shall** be **discrete** with exactly **`N`** frame positions (**§7.4.31**), and **shall not** behave as a continuous or infinite control. The **vertical exaggeration** control **shall** remain hidden while **Haul** is active (**§7.1.4**).

**[7.4.11]** **Route position label (normative)** — the **route position label** **shall** display **`{current}/{total}`** where **`current = k + 1`** and **`total = N`** (**§4.4.2**, **§4.4.3**, **§4.4.4**). Examples: at the initial position after haul generation (**§7.3.4**, **§7.4.13**), **`k = 0`** with **`N = 2451`**, the label **shall** read **`1/2451`**; at **`k = 55`**, the label **shall** read **`56/2451`**; at **`k = N − 1`**, the label **shall** read **`N/N`** (for example **`2451/2451`**). The label **shall** update continuously while the user **scrubs** the slider and **shall** remain visible whenever the **route scrub** slider is enabled.

**[7.4.12]** **Pointer scrubbing behaviour (normative)** — while the user **clicks and drags** the **route scrub** slider, the **route scrub index** (**§4.4.3**) and **route position label** (**§7.4.11**) **shall** update only at **discrete frame positions** (**§7.4.31**); the thumb **shall** **snap** to the nearest frame detent and **shall not** rest between frames. The **Haul bitmap** **shall not** regenerate on every intermediate index during an active drag; instead, **Haul scrub frame** generation and display **shall** follow **§7.4.15**–**§7.4.20**. If the **Haul scrub frame cache** (**§4.7.2**) already contains a frame for the live **route scrub index**, the **Haul bitmap** **may** switch to that cached frame immediately during drag without waiting for **scrub frame commit**.

**[7.4.13]** **Initial scrub position (normative)** — after a successful **Haul Builder** run activates the **Haul** view (**§7.3.1**, **§7.3.3**, **§7.3.4**) or after **Open** restores **haul data** and activates **Haul** (**§7.5.4**), the **route scrub** slider **shall** default to the **first** step (**`k = 0`**) with the **route position label** reading **`1/N`**. The application **shall** **commit** and **cache** the **`k = 0`** **Haul scrub frame** per **§7.3.4**–**§7.3.6** and **§7.4.18**–**§7.4.19**. When the user returns to **Haul** by clicking the **Haul** button (**§7.2.4**) after selecting another field content view in the same session, the application **may** restore the last **route scrub index** and display its cached frame if present.

**[7.4.14]** **Step count** — when **haul data** contains no routable steps (**`N = 0`**), the **route scrub** slider **shall** be **disabled**, the **route position label** **shall** read **`0/0`**, and the **Haul bitmap** **shall** show the only available haul presentation.

**[7.4.15]** **Scrub frame commit (normative)** — while the user is dragging the **route scrub** slider, a new **Haul scrub frame** **shall** be generated and displayed only on **scrub frame commit** (**§4.7.3**). **Scrub frame commit** **shall** occur on the **first** of:

**[7.4.16]** 1. **Scrub pause** — the **route scrub index** is unchanged for more than **`500 ms`** while the drag is active (**§4.7.4**). The pointer button **may** still be pressed; release is **not** required.

**[7.4.17]** 2. **Pointer release** — the user releases the pointer button on the **route scrub** slider.

**[7.4.18]** On **scrub frame commit**, if the **Haul scrub frame cache** already contains a completed frame for the current **route scrub index** **`k`**, the application **shall** display that cached frame as the **Haul bitmap** without regenerating it. Otherwise the application **shall** start **foreground frame generation** for **`k`** per **§7.4.19**–**§7.4.20**.

**[7.4.19]** **Foreground frame generation (normative)** — **foreground frame generation** for **route scrub index** **`k`** **shall** build the **Haul scrub frame** (**§4.7.1**) using **parallel computation** (for example **`Parallel.For`**, task parallel library, or equivalent) over independent regions of the haul simulation and/or rasterization work. When generation completes, the application **shall** store the frame in the **Haul scrub frame cache**, **shall** set the **frame cache tick mark** for **`k`** to **green** (**§7.4.28**), and **shall** display the frame as the **Haul bitmap**. **Foreground frame generation** **shall** run on a **background worker thread** so the UI thread remains responsive; only bitmap upload, tick-mark updates, and display **shall** occur on the UI thread.

**[7.4.20]** **Preemption and partial resume (normative)** — if **foreground frame generation** for index **`k`** is **in progress** and the user **starts scrubbing again** (the **route scrub index** changes before that generation completes), the in-progress work **shall not** be discarded immediately. On the **next** **scrub frame commit** (**§7.4.15**–**§7.4.17**) for a **new** index **`k′`**:

**[7.4.21]** 1. The application **shall** **store** the **partial frame generation state** (**§4.7.5**) for the preempted index **`k`** so generation **may** be **resumed** later.

**[7.4.22]** 2. The application **shall** **start** **foreground frame generation** (**§7.4.19**) for the newly committed index **`k′`** (or display the cached frame if already present). Later **foreground** or **prefetch** work **shall** **resume** from stored **partial frame generation state** when generating that index again.

### Background frame prefetch

**[7.4.23]** **Prefetch worker (normative)** — when **no** **foreground frame generation** is in progress **and** the user is **not** **interacting with the slider** (**§4.7.6**), the application **shall** run a **prefetch worker** on a **background thread** that generates and caches **Haul scrub frames** for indices not yet present in the **Haul scrub frame cache** (**§4.7.2**). The **prefetch worker** **shall** pause or yield whenever **foreground frame generation** starts or the user **interacts with the slider**.

**[7.4.24]** **Initial sequential prefetch (normative)** — after haul generation completes, the **`k = 0`** **Haul scrub frame** is cached (**§7.3.7**), and the user is **not** **interacting with the slider**, the **prefetch worker** **shall** generate uncached frames in ascending route order: **`k = 1`** (route **2**), then **`k = 2`** (route **3**), then **`k = 3`**, and so on through **`N − 1`**, skipping indices already cached or with resumable **partial frame generation state** until that index is completed and cached. When a prefetched frame is cached, the corresponding **frame cache tick mark** **shall** turn **green** per **§7.4.28**.

**[7.4.25]** **Scrub idle prefetch (normative)** — when the user has **scrubbed** to a **route scrub index** other than the initial post-generation state and is **not** **interacting with the slider**, and **§7.4.24** does not apply, the **prefetch worker** **shall** generate uncached frames in bidirectional order from the current index **`k`**: **`k − 1`**, then **`k + 1`**, then **`k − 2`**, then **`k + 2`**, and so on, skipping out-of-range indices and indices already cached. Stored **partial frame generation state** **shall** be **resumed** before starting that index from scratch. When a prefetched frame is cached, the corresponding **frame cache tick mark** **shall** turn **green** per **§7.4.28**.

### Frame cache tick marks

**[7.4.27]** **Tick mark presentation (normative)** — for each **route scrub index** **`k`** where **`0 ≤ k < N`**, the **HAUL ROUTE** section **shall** display one **frame cache tick mark** (**§4.7.8**) **next to** the **route scrub** slider at the vertical position corresponding to step **`k`** (aligned with the slider thumb position for that index). Each tick **shall** be a small **horizontal** mark. A tick **shall** be **grey** when the **Haul scrub frame** for that index is **not** in the **Haul scrub frame cache**; a tick **shall** be **green** when the frame **is** cached. **Partial frame generation state** alone **shall not** turn a tick **green**.

**[7.4.28]** **Tick mark updates (normative)** — whenever a **Haul scrub frame** is stored in the **Haul scrub frame cache** (**§4.7.2**) — by **foreground frame generation** (**§7.4.19**), **prefetch worker** completion (**§7.4.23**–**§7.4.25**), or initial **`k = 0`** commit (**§7.4.13**) — the **frame cache tick mark** for that index **shall** update to **green** on the UI thread. Indices with no cached frame **shall** remain **grey**.

**[7.4.29]** **Tick mark reset (normative)** — when **haul data** is replaced or cleared (**§7.3.3**, **§4.7.2**), all **frame cache tick marks** **shall** reset to **grey** before new frames are generated for the new **haul route sequence**.

**[7.4.31]** **Discrete frame snap (normative)** — the **route scrub** slider **shall** have exactly **`N`** detents, one per **route scrub index** **`k`** where **`0 ≤ k < N`**. Each detent **shall** align with the corresponding **frame cache tick mark** (**§7.4.27**). The slider **shall** **snap** the thumb to the nearest detent during pointer drag and on pointer release; intermediate positions between frames **shall not** be selectable or displayed as the active **route scrub index**.

**[7.4.32]** **Keyboard scrub (normative)** — while **Haul** is the active field content view and keyboard focus is in the **HAUL ROUTE** scrub control (or the **field viewer** when **Haul** is active, at the application’s discretion), the following **shall** step the **route scrub index** and **shall** trigger **scrub frame commit** (**§7.4.18**) immediately for the new index:

**[7.4.33]** 1. **Left arrow** — previous frame: **`k ← max(0, k − 1)`**.

**[7.4.34]** 2. **Right arrow** — next frame: **`k ← min(N − 1, k + 1)`**.

**[7.4.35]** 3. **Ctrl + Left arrow** — previous **10** frames: **`k ← max(0, k − 10)`**.

**[7.4.36]** 4. **Ctrl + Right arrow** — next **10** frames: **`k ← min(N − 1, k + 10)`**.

**[7.4.37]** On each keyboard step, the **route scrub** slider thumb **shall** **snap** to the detent for the new **`k`**, the **route position label** **shall** update, and the **Haul scrub frame** for **`k`** **shall** be displayed per **§7.4.18** (cached frame immediately, otherwise **foreground frame generation** per **§7.4.19**). Keyboard scrub **shall not** use the **500 ms** **scrub pause** rule (**§7.4.16**). Repeated key presses **shall** step cumulatively within the **`0 ≤ k < N`** bounds.

**[7.4.38]** *(Reserved — **Phase 2** normative rules for haul-flow arrow overlays and which **DISPLAY** toggles apply to **Haul** beyond the **cut/fill map** base (**§7.4.5**).)*

### 7.5 Project file persistence (normative)

**[7.5.1]** **Haul data** **shall** be **project-owned data** (**AgGrade Designer §4.2.1**). When the current project **has haul data**, **Save** and **Save As** (**AgGrade Designer §4.1.3**, **§4.1.7**) **shall** embed that **haul data** in the **`.agdesign`** file (**Appendix A §A.2**).

**[7.5.2]** When the project **has no haul data**, **Save** **shall not** write a **`Haul`** element. If the user clears **haul data** and then **Save**s, any **`Haul`** section previously stored in the project file **shall** be removed or replaced with an empty/absent haul payload so **Open** does not restore stale haul results.

**[7.5.3]** **Open** (**AgGrade Designer §4.1.2**, **§6.1.1**) **shall** restore embedded **haul data** when the file contains a valid **`Haul`** element (**Appendix A §A.2**). After a successful **Open**, the in-memory project **has haul data** (**§4.2.2**) **if and only if** the loaded file contained restorable **haul data**.

**[7.5.4]** After **Open** restores **haul data**, the **Haul** button **shall** be **enabled** (**§7.2.3**) and the active field content view **shall** be **Haul** (**§7.1**). The **field viewer** **shall** switch to **2D** view mode (**§7.1.4**). The **Haul** button **shall** show the active highlight. The initial **Haul** presentation **shall** follow **§7.4.13** (**`k = 0`**, **Cut/fill map** base, **first** haul route). This rule **shall** apply in addition to **AgGrade Designer §2.4.10** (case 4) whenever restored **haul data** is present.

**[7.5.5]** **Save** **shall** persist the active field content view in **`ViewerState/FieldContentView/@view`** (**AgGrade Designer Appendix A §A.5.2**) when **Haul** is active, using the value **`Haul`** (**Appendix A §A.3.1**). **Open** **shall** restore a saved **`Haul`** view only when **haul data** is also restored; if **`Haul`** is saved but haul payload is missing or invalid, **Open** **shall** follow **§7.5.4** when haul data is present, otherwise **AgGrade Designer §2.4.10**.

**[7.5.6]** Changes to **haul data** **shall** mark the project **dirty** (**AgGrade Designer §4.4.1**) until the next successful **Save**, consistent with other project-owned data.

### 7.6 Settings dialog — Haul section (normative)

**[7.6.1]** The application **shall** add a **Haul** category to the left navigation of the existing **Settings** dialog (**AI Features §5**), with visible title **Haul**. Selecting **Haul** **shall** show haul default controls in the dialog content pane. Content **shall** be grouped under the subsection headings **Equipment and pass limits**, **Field operation**, **Travel speeds and turning**, **Center pivot**, and (in **Phase 2**) **Advanced sequencing** and **Viewer**, using the same layout conventions as the **AI** and **Units** sections (green section title, label + control stacks, **Advanced** expander where noted).

**[7.6.2]** **Persistence and session behaviour** — **Haul settings** **shall** persist in the **user settings file** (**AI Features §4.17**, **§5.6**, **Appendix B**), **shall not** be written to **`.agdesign`** project files, and **shall** follow the same **OK** / **Cancel** session rules as **AI** settings (**AI Features §5.7**): **OK** writes all **Haul** fields to the **user settings file**; **Cancel** discards session edits.

**[7.6.3]** **Display units (normative)** — every numeric **Haul** setting label **shall** show the unit suffix appropriate to the user’s global **Units** preference (**Settings** > **Units**, **`DisplayUnitsSettings.System`**, **AI Features §4.18**). The application **shall** convert values between display units and the canonical units expected by **`HaulBuilder.HaulBuilderOptions`** when populating the dialog and when building options for a haul run (**§6.1**).

**[7.6.4]** | Quantity | Imperial display | Metric display | **`HaulBuilderOptions` property** |
|---|---|---|---|
| Scraper capacity | **LCY** | **m³** | **`ScraperCapacityLcy`** (convert via **`HaulBuilder.Constants.M3_TO_CY`**) |
| Blade width | **ft** | **m** | **`BladeWidthMm`** |
| Max depth per pass | **in** | **mm** | **`MaxDepthMm`** |
| Finishing tolerance | **in** | **mm** | **`ToleranceMm`** |
| Max drop hop | **ft** | **m** | **`MaxDropHopFt`** |
| Equipment width | **ft** | **m** | **`EquipmentWidthFt`** |
| Travel speeds | **mph** | **km/h** | **`LoadingSpeedMph`**, **`HaulSpeedMph`**, **`DumpingSpeedMph`**, **`ReturnSpeedMph`**, **`TurnSpeedMph`**, **`GradeSpeedMph`** |
| Turning circle | **ft** | **m** | **`TurningCircleFt`** |
| Pivot wheelbase / clearance | **ft** | **m** | **`PivotWheelbaseFt`**, **`PivotClearanceFt`** |
| Lat / lon | decimal **degrees** | decimal **degrees** | **`PivotCenter`** (**§7.6.28**) |

**[7.6.5]** Dimensionless and non-length fields (**swell factor**, **field efficiency**, **lane overlap**, **min load fill %**, **turn threshold** in **degrees**, **cut zones**, **seed**, toggles) **shall** use unitless or fixed-unit labels that do not change with **Units** preference.

### Equipment and pass limits

**[7.6.7]** The **Haul** section **shall** expose the following controls, mapped to **`HaulBuilderOptions`** with display units per **§7.6.4**:
**[7.6.8]** 1. **Scraper capacity** — **`ScraperCapacityLcy`**
**[7.6.9]** 2. **Swell factor** — **`SwellFactor`**
**[7.6.10]** 3. **Blade width** — **`BladeWidthMm`** (also drives **`GridSizeFt`** per **§6.2**)
**[7.6.11]** 4. **Max depth per pass** — **`MaxDepthMm`**
**[7.6.12]** 5. **Finishing tolerance** — **`ToleranceMm`**
**[7.6.13]** 6. **Max drop hop** — **`MaxDropHopFt`**
**[7.6.14]** 7. **Min load fill** — **`MinLoadFillPct`**
**[7.6.15]** 8. **Equipment width** — **`EquipmentWidthFt`**
**[7.6.16]** 9. **Lane overlap** — **`LaneOverlap`**

### Field operation

**[7.6.18]** The **Haul** section **shall** expose:
**[7.6.19]** 1. **Field efficiency** — **`FieldEfficiency`**
**[7.6.20]** 2. **Transport mode** — **`TransportMode`**: **Poisson** (`poisson`) or **Min-cost flow** (`mincostflow`)
**[7.6.21]** 3. **Balance mode** — **`BalanceMode`**: **Check** (`check`) or **Adjust** (`adjust`)
**[7.6.22]** 4. **Cut zones** — **`Zones`**
**[7.6.23]** 5. **Use design main slope direction** — when **enabled**, **`HeadingDeg`** **shall** come from the project design (**§6.1.3**); when **disabled**, **Heading** (**degrees**) **shall** be editable and mapped to **`HeadingDeg`**

### Travel speeds and turning

**[7.6.25]** The **Haul** section **shall** expose **`LoadingSpeedMph`**, **`HaulSpeedMph`**, **`DumpingSpeedMph`**, **`ReturnSpeedMph`**, **`TurnSpeedMph`**, **`GradeSpeedMph`**, **`AccelTimeS`**, **`DecelTimeS`**, **`TurningCircleFt`**, and **`TurnThresholdDeg`** with display units per **§7.6.4**.

### Center pivot

**[7.6.27]** The **Haul** section **shall** expose **Enable pivot analysis** — toggle mapped to **`Pivot`**. When **on**, it **shall** also expose:

**[7.6.28]** 1. **Pivot latitude** and **Pivot longitude** — decimal-degree text fields mapped to **`PivotCenter`** in **`lat,lon`** form (**WGS-84**, same convention as survey points, **AgGrade Designer §A.3.3**)
**[7.6.29]** 2. **Pivot wheelbase** — **`PivotWheelbaseFt`**
**[7.6.30]** 3. **Pivot clearance** — **`PivotClearanceFt`**
**[7.6.31]** 4. **Pivot move cost** — **`PivotMoveCost`**

**[7.6.32]** When **`Pivot`** is **off**, pivot latitude/longitude and pivot geometry fields **may** be hidden or disabled; persisted values **shall** be retained in the **user settings file**.

### Advanced sequencing (Phase 2)

**[7.6.34]** **Phase 2** **shall** add an **Advanced** expander grouping: **Flow-aligned strips** (invert **`NoFlowAligned`**), **`HeadingStrength`**, **`StripBias`**, **Direction-aware sequencing** (invert **`NoDirectionAware`**), **Grade-return passes** (invert **`NoGradeReturn`**), **Route smoothing** (invert **`NoRouteSmoothing`**), **Knot re-profiling** (invert **`NoKnotRepair`**), **`ReorderWindow`**, and **`Seed`**.

### Viewer (Phase 2)

**[7.6.36]** **Phase 2** **shall** expose **Flow arrow spacing** — integer mapped to **`ArrowStride`** (grid-cell subsampling for haul-flow arrows in the **Haul** view, **§7.4.38**). This **shall not** affect **`PixelsPerBin`** or other export-only render settings (**§3.0.6**).

### Phase 1 minimum control set

**[7.6.38]** **Phase 1** **shall** ship **§7.6.8**–**§7.6.31** (equipment, field operation, travel speeds, center pivot with lat/lon). **Phase 1** **shall not** require **§7.6.34** or **§7.6.36**.

**[7.6.39]** **Excluded controls** — the **Haul** settings UI **shall not** include **Grid size** or **`GridSizeFt`** (**§6.2**, **§3.0.2**). It **shall not** include an editable **Cut/fill ratio** (**§6.1.2**, **§3.0.3**). It **shall not** include CLI export paths, animation outputs, **`PixelsPerBin`**, or **`ExcludePolygonSpecs`** (**§3.0.4**–**§3.0.6**, **§6.1.4**).

**[7.6.40]** Default values for all **Haul settings** **shall** match **`HaulBuilder.Constants.DEFAULT_*`** (and constructor defaults on **`HaulBuilderOptions`**) unless this document explicitly specifies otherwise.

**[7.6.41]** Changing **Haul settings** and clicking **OK** **shall not** by itself mark the open project dirty (**AgGrade Designer §4.4.1**) or regenerate **haul data**. The user **shall** run **Generate haul** again to apply new defaults.

---

## 8. Automated testing

**[8.0.1]** Unit tests in **`AgGradeDesigner.Core.Tests`** **shall** cover at least:

**[8.0.2]** 1. **Haul settings** round-trip through the **user settings file** (**Appendix B**) independently of any open project.
**[8.0.3]** 2. Saving a **`ProjectDocument`** to **`.agdesign`** **shall not** embed **Haul settings** (only **haul data** when present, **§7.5**).
**[8.0.4]** 3. **`GridSizeFt`** derivation from **blade width** (**§6.2**).
**[8.0.5]** 4. Display-unit conversion for at least one imperial-labelled and one metric-labelled **Haul** field (**§7.6.3**–**§7.6.4**).
**[8.0.6]** 5. **`HaulBuilderOptions`** mapping: **Cut/fill ratio** from design (**§6.1.2**), **HeadingDeg** from design when **Use design main slope direction** is enabled (**§6.1.3**).
**[8.0.7]** 6. **Haul** view activation forces **2D** mode and uses **bitmap** presentation only — **no** **`VulkanFieldRenderer`** / **`FieldRenderer`** mesh path when **Haul** is active (**§7.1.4**, **§7.4.2**).
**[8.0.8]** 7. **Route scrub** — pointer drag on the discrete **route scrub** slider updates the **route scrub index** and **route position label** (**§7.4.11**) at frame detents; **Haul scrub frame** display follows **scrub frame commit** (**§7.4.15**–**§7.4.20**) without changing pan/zoom framing (**§7.4.4**, **§7.4.12**).
**[8.0.9]** 8. **Haul route symbology** — each **Haul scrub frame** includes **red** cut sections, **blue** fill sections, and a **black** GPS track with **white**-outlined dots (**§7.4.6**–**§7.4.9**), composited over the **haul cut/fill base map** (**§7.4.5**).
**[8.0.10]** 9. **Scrub frame commit** — **scrub pause** (**500 ms**) or pointer release triggers **foreground frame generation** (if needed), caching, and display for the selected **route scrub index** (**§7.4.15**–**§7.4.20**).
**[8.0.11]** 10. **Initial haul view after generation** — successful haul generation activates **Haul** with **`k = 0`**, **Cut/fill map** base matching **Cut/fill map** **2D** view, **first** haul route visible, **route position label** **`1/N`**, and cached **`k = 0`** frame (**§7.3.4**, **§7.4.13**).
**[8.0.12]** 11. **Foreground parallel generation** — committed frames are built with parallel computation on a background thread (**§7.4.19**).
**[8.0.13]** 12. **Partial frame preemption** — re-scrubbing before the current frame completes stores **partial frame generation state** on the next commit and starts the newly committed frame (**§7.4.20**–**§7.4.22**).
**[8.0.14]** 13. **Background prefetch** — when idle and not **interacting with the slider**, the prefetch worker runs **initial sequential prefetch** (**§7.4.24**) after **`k = 0`** is cached, otherwise **scrub idle prefetch** (**§7.4.25**).
**[8.0.15]** 14. **Frame cache tick marks** — **grey** when uncached, **green** when cached; all **grey** after haul re-run clears the cache (**§7.4.27**–**§7.4.29**).
**[8.0.16]** 15. **Discrete slider and keyboard scrub** — slider snaps to **`N`** frame detents; **Left** / **Right** arrow step one frame; **Ctrl**+arrow steps **10** frames, each triggering immediate **scrub frame commit** (**§7.4.31**–**§7.4.37**).

---

## 9. Summary

**[9.0.1]** *(Reserved — to be written in a later revision.)*

---

## Appendix A — `.agdesign` haul data extension (normative)

**[A.0.1]** This appendix **extends** **AgGrade Designer Appendix A** with embedded **haul data** for **`.agdesign`** project files. It **shall** be read together with the baseline appendix (**AgGrade Designer §A.1**–**§A.6**). Element names and optional fields **may** evolve in later **MINOR** revisions of this document; incompatible changes **shall** increment **`schemaVersion`** on the root **`AgDesignProject`** element per the baseline rules.

### A.1 Document structure extension

**[A.1.1]** When the project **has haul data** (**§4.2.2**), a **`Haul`** child element **shall** be written on **Save** (**§7.5.1**). When absent, the project has no stored haul results.

**[A.1.2]** **`Haul`** **shall** appear after **`Design`** and before **`ViewerState`** when present, extending the baseline child order (**AgGrade Designer §A.1.3**):

```text
Metadata, Survey, Design, Haul, ViewerState
```

### A.2 Haul element

**[A.2.1]** *(Reserved — normative XML layout for serialized **haul data** to be written in a later revision. Implementations **shall** round-trip sufficient payload to restore **haul data** and render the **Haul** field content view after **Open** (**§7.5.3**).)*

### A.3 ViewerState extension

**[A.3.1]** **`ViewerState/FieldContentView/@view`** (**AgGrade Designer §A.5.3**) **shall** accept **`Haul`** in addition to **`Existing`**, **`Proposed`**, and **`CutFill`**.

**[A.3.2]** On **Open**, a saved **`Haul`** value **shall** be honoured only when restored **haul data** is present (**§7.5.5**). When restored **haul data** is present, **Open** **shall** activate the **Haul** field content view per **§7.5.4** regardless of other baseline default-view rules (**AgGrade Designer §2.4.10** case 4).

---

## Appendix B — User settings `Haul` section (normative)

**[B.0.1]** This appendix defines how **Haul settings** (**§7.6**) are stored in the per-user **user settings file** alongside **`AiSettings`** and **`DisplayUnitsSettings`** (**AI Features §4.17**). **Haul settings** **shall not** appear in **`.agdesign`** project files.

**[B.0.2]** The application **shall** add a **`Haul`** object to **`UserSettingsDocument`** (or equivalent root document) containing one persisted field for each **Phase 1** control in **§7.6.7**–**§7.6.31** and, when **Phase 2** ships, each control in **§7.6.34** and **§7.6.36**. Field names **should** mirror **`HaulBuilderOptions`** property names; values **shall** be stored in the canonical units **`HaulBuilderOptions`** expects (not display units).

**[B.0.3]** **`PivotCenter`** **shall** be persisted as separate **`PivotLatitudeDeg`** and **`PivotLongitudeDeg`** numeric fields (or equivalent) in the **user settings file** and composed into **`lat,lon`** when building **`HaulBuilderOptions`**.

**[B.0.4]** **`UseDesignMainSlopeDirection`** (boolean) **shall** be persisted separately from **`HeadingDeg`**. When **true** at run time, **`HeadingDeg`** **shall** be ignored in favour of the project design (**§6.1.3**).

**[B.0.5]** Increment **`UserSettingsDocument.SchemaVersion`** when **`Haul`** is first added. Loaders **shall** supply **`HaulBuilder.Constants.DEFAULT_*`** defaults for missing **`Haul`** fields.
