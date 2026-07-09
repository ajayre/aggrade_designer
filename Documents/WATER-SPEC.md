# Water Specification **[0.0.1]**

**[0.0.2]** Every normative paragraph carries a bold marker **`[<S>.<U>.<P>]`** where **`<S>`** is the section, **`<U>`** is the subsection (**`0`** for body text before the first **`###`**), and **`<P>`** is the 1-based paragraph index within that group.

**[0.0.3]** This document specifies the **Water** feature in **AgGrade Designer**: project-owned **water layer** data, a **Water** command in the left sidebar **PROJECT** section that opens the **Water dialog**, and a **Water** **DISPLAY** decoration toggle in the right sidebar.

**[0.0.4]** - **Project name:** AgGrade Designer — Water
**[0.0.5]** - **Document type:** Application-layer feature specification (skeleton)
**[0.0.6]** - **Language / implementation:** C# (.NET 8), Avalonia UI, MVVM — source **shall** follow **ANDY-CSHARPCODINGSTYLEGUIDE.md**
**[0.0.7]** - **Target platforms:** Windows, Linux, macOS (same as **AGGRADE-DESIGNER-SPEC-1.0.md**)
**[0.0.8]** - **Scope:** **Surface water layer** calculation from **Water settings**, **water layer** definition and persistence, **Water dialog** with **Water settings** (**§7.1**), **PROJECT** sidebar **Water** button (**§7.2**), **DISPLAY** **Water** toggle (**§7.3**), **water layer** rendering as a field-viewer **decoration** in **2D** and **3D** on all field content views (**§7.4**), and **`.agdesign`** persistence for **Water settings** and **existing surface** / **proposed surface** **water layer** data (**§7.5**, **Appendix A**). Extended **Water dialog** editing workflows and extended test obligations **may** be added in later revisions of this document.
**[0.0.9]** - **Relationship to baseline:** **AGGRADE-DESIGNER-SPEC-1.0.md** remains the authoritative contract for the core application (viewer, survey, design, project files, **AgGrade Designer §7** UI chrome). This document **extends** the product with **Water** data and UI. It **extends** the right sidebar **DISPLAY** toggle list (**AgGrade Designer §7.5.1**–**§7.5.2**) with a **Water** decoration toggle (**§7.3**). It **extends** the left sidebar **PROJECT** section (**AgGrade Designer §7.4.2**–**§7.4.5**) with a **Water** dialog-launch button (**§7.2**). It **shall not** change baseline survey or design export wire formats (**AgGrade Designer §9**, **AgGrade Designer Appendix A** survey and design sections) unless a future revision of either document explicitly says otherwise.
**[0.0.10]** - **Relationship to tools:** **TOOLS-SPEC.md** defines map-editing **tool data** (**Tools §4**), which is session-scoped and **shall not** be stored in project files. **Water layer** data **shall** be **project-owned** (**§4.2**, **§7.5**) and is distinct from **tool data** and **tool polygons** (**Tools §3.4**).
**[0.0.11]** - **Relationship to haul:** **HAUL-SPEC.md** defines the **Haul** field content view and haul-specific overlays. **Water** is a **DISPLAY** decoration composited over the active field content view, including **Haul** (**§7.4.2**). **Water** is **not** a separate field content view and **shall not** appear in the **VIEW** button stack (**AgGrade Designer §7.4.8**–**§7.4.12**, **Haul §7.2**).
**[0.0.12]** - **Relationship to AI features:** **AI-FEATURES-SPEC.md** defines the **Settings** dialog layout, **user settings file** persistence (**AI Features §4.17**, **AI Features §5.6**), **OK** / **Cancel** session semantics (**AI Features §5.7**), and global **Units** preference (**AI Features §4.18**). **Water settings** **shall** be edited in the **Water dialog** (**§7.1**), **shall** persist in **`.agdesign`** project files (**Appendix A**), and **shall not** appear in the **Settings** dialog or the **user settings file**.
**[0.0.13]** - **Assumed knowledge:** Readers **shall** already know **AGGRADE-DESIGNER-SPEC-1.0.md** terminology (**AgGrade Designer §6**), project model (**AgGrade Designer §4**, **`ProjectDocument`**), design model (**AgGrade Designer §5**), field viewer and **DISPLAY** decorations (**AgGrade Designer §2.4.12**, **AgGrade Designer §7.5**), and UI layout (**AgGrade Designer §7.4**). In this document, bare **§N** refers to **this document**; **AgGrade Designer §N** or **AGGRADE-DESIGNER-SPEC-1.0.md §N** refers to the baseline spec; **Tools §N** refers to **TOOLS-SPEC.md**; **Haul §N** refers to **HAUL-SPEC.md**; **AI Features §N** refers to **AI-FEATURES-SPEC.md**.
**[0.0.14]** - **Related documents:** **ANDY-SPECSTYLEGUIDE.md** (authoring rules), **ANDY-CSHARPCODINGSTYLEGUIDE.md** (C# source style), **AGGRADE-DESIGNER-SPEC-1.0.md** (baseline application specification), **AI-FEATURES-SPEC.md** (**Settings** dialog and **user settings file**), **TOOLS-SPEC.md** (map-editing tools), **HAUL-SPEC.md** (haul integration). **AgGrade** **surface water layer** reference: **`Application/Data/FlowMapGenerator.cs`** (**§8**).

**[0.0.15]** - **AgGrade reference tree:** **`C:\Users\andy\OneDrive\Documents\AgGrade\`** (paths in **§8** are relative to this folder unless absolute paths are given).

**[0.0.16]** **Version axes:**

**[0.0.17]** 1. **Specification document** version (this markdown file; semver in **§0.1**).
**[0.0.18]** 2. **Application** version (shipping product semver; distinct from this document).

**[0.0.19]** Incrementing one version axis **does not** imply incrementing another.

**[0.0.20]** **Phased delivery:**

**[0.0.21]** 1. **Phase 1** — **Water** **DISPLAY** toggle (**§7.3**), **surface water layer** calculation on **Water dialog** **OK** (**§6**, **§7.1.11**), **PROJECT** **Water** button and **Water dialog** with **Water settings** (**§7.1**, **§7.2**), **water layer** viewer decoration in **2D** and **3D** on all field content views (**§7.4**), **`.agdesign`** persistence (**§7.5**).
**[0.0.22]** 2. **Phase 2** — *(Reserved — extended **Water dialog** editing workflows, import/export, and additional **DISPLAY** options to be specified in a later revision.)*

**[0.0.23]** **Phase 1** **shall not** be withheld until **Phase 2** features exist.

### 0.1 Specification revision history

**[0.1.1]** | Version | Date | Change |
|---|---|---|
| **0.1.0** | 2026-07-08 | Initial skeleton: metadata, **Water** **DISPLAY** toggle enablement, **PROJECT** **Water** button, **Water dialog** entry point. |
| **0.2.0** | 2026-07-08 | **Settings** dialog **Water** section (**§7.6**): **Ponding rainfall**, **Ponding curve number**; **user settings file** schema (**Appendix B**). |
| **0.3.0** | 2026-07-08 | **Surface water layer** calculation on **Settings** **OK** (**§6**, **§7.6.8**); auto-enable **Water** toggle; **2D** / **3D** rendering; all field content views including **Haul** (**§7.4**). |
| **0.4.0** | 2026-07-08 | Per-view **basis surface** mapping for **surface water layer** calculation (**§4.4**, **§6.2**, **§7.4.4**). |
| **0.5.0** | 2026-07-08 | **Water layer** retention while **basis surface** unchanged (**§6.3**); discard **Haul** **water layer** when **pinned haul surface** is discarded. |
| **0.6.0** | 2026-07-08 | Per-active-view **Water** toggle enablement; view-switch **off**/**disabled** when cache missing (**§7.3.9**); **Settings** **OK** calculates active view **basis surface** only (**§6.2.6**). |
| **0.7.0** | 2026-07-08 | **AgGrade** reference implementation (**§8**): **`Application/Data/FlowMapGenerator.cs`**. |
| **0.8.0** | 2026-07-08 | **3D** **water layer** terrain draping and camera-view clipping (**§4.1.4**–**§4.1.5**, **§7.4.6**–**§7.4.9**); unit-test obligations (**§9.0.1**, **§9.0.22**–**§9.0.24**). |
| **0.9.0** | 2026-07-08 | **Water settings** moved from **Settings** dialog to **Water dialog** (**§7.1**); calculation trigger is **Water dialog** **OK** (**§6**, **§7.1.11**). |
| **0.10.0** | 2026-07-08 | **Water layer** z-order below **benchmarks**, above **contours** (**§7.4.10**); **existing** / **proposed** **water layer** persistence in **`.agdesign`** with **BASE64** binary blobs (**§7.5**, **Appendix A**). |
| **0.11.0** | 2026-07-08 | **Water settings** persisted in **`.agdesign`** project files (**Appendix A**); removed **Appendix B** user-settings schema. |
| **0.12.0** | 2026-07-08 | **Proposed surface** change discards **water layer** and turns **Water** toggle **off**/**disabled** when active (**§6.3.7**, **§7.3.13**). |
| **0.13.0** | 2026-07-08 | Removed redundant **§6.0.3** project requirement; renumbered **§6.0.4**–**§6.0.6**. |
| **0.14.0** | 2026-07-08 | **Viewport progress bar** during **surface water layer calculation**; calculation **shall not** block the **UI thread** (**§6.0.6**, **§7.1.17**). |
| **0.15.0** | 2026-07-08 | **`ElevationTypes.Initial`** excluded from **AgGrade Designer** context; **basis surface** → **`Current`** / **`Target`** mapping (**§8.1.3**, **§8.1.12**). |
| **0.16.0** | 2026-07-08 | Consistency pass: fixed incorrect internal and external cross-references; aligned **§5.1.3**, **§6.0.4**, and **§6.2.6** with async calculation and **§5.1.2** storage split. |
| **0.17.0** | 2026-07-08 | Removed **displayable water feature** requirement; any **basis surface** **may** have a valid **cached water layer result**, including zero ponding (**§4.2**, **§4.5.3**, **§7.3**). |
| **0.18.0** | 2026-07-08 | **Haul** **water layer** uses **haul cut/fill surface** at the active **route scrub index** (**Haul §4.6**), not **pinned haul surface** (**§4.4.4**, **§6.2**, **§6.3.6**, **§7.3.16**, **§7.4.4**). |

**[0.1.2]** This document follows [semantic versioning](https://semver.org/): **MAJOR** = incompatible normative change, **MINOR** = backward-compatible addition, **PATCH** = clarification.

---

## 1. Purpose

**[1.0.1]** **AgGrade Designer** **shall** expose a **Water dialog** from the **PROJECT** sidebar (**§7.1**, **§7.2**) where the user configures per-project **Water settings**, **shall** compute a **surface water layer** for the active project when the user clicks **OK** on that dialog (**§6**, **§7.1.11**), **shall** persist **Water settings** and **existing surface** / **proposed surface** **water layer** data in **`.agdesign`** project files (**§7.5**, **Appendix A**), and **shall** show or hide that layer in the **field viewer** using a **Water** **DISPLAY** toggle in **2D** or **3D** on any field content view (**§7.3**, **§7.4**).

**[1.0.2]** Obligations in this document use **shall**, **should**, **may** as defined in **ANDY-SPECSTYLEGUIDE.md §6**.

---

## 2. Design goals

**[2.0.1]** The implementation **shall provide**:

**[2.0.2]** 1. A **Water** **DISPLAY** decoration toggle in the right sidebar **DISPLAY** panel (**§7.3**) that shows or hides the **water layer** on the active field content view.

**[2.0.3]** 2. A **Water** **DISPLAY** toggle that is **enabled** only when the **active** field content view **has a water layer to show** (**§4.2.2**, **§7.3.3**).

**[2.0.4]** 3. A **Water** button in the left sidebar **PROJECT** section (**§7.2**) with a leading icon (**TBD**), centred label **Water**, and a trailing right-arrow affordance matching **Survey**, **Design**, and **Haul** dialog-launch buttons (**AgGrade Designer §7.4.3**–**§7.4.4**).

**[2.0.5]** 4. Opening the **Water dialog** when the user clicks the **Water** button (**§7.1**, **§7.2.8**).

**[2.0.6]** 5. **Water layer** rendering as a **decoration** composited over the active field content view in **2D** or **3D** when the **Water** toggle is **on** (**§7.4**), on **Existing**, **Proposed**, **Cut/fill map**, and **Haul** views. In **3D** orbit view, the **water layer** **shall** be **draped** on the **basis surface** terrain with **camera-view clipping** for portions obscured by closer terrain (**§7.4.6**).

**[2.0.7]** 6. **Water settings** — **Ponding rainfall** and **Ponding curve number** controls in the **Water dialog** (**§7.1**), persisted in the active **`.agdesign`** project file (**Appendix A**), with **Ponding rainfall** shown in **mm** or **in** according to the global **Units** preference (**AI Features §4.18**). The **Settings** dialog **shall not** expose **Water** settings.

**[2.0.8]** 7. **Surface water layer** calculation when the user clicks **OK** on the **Water dialog** (**§6**, **§7.1.11**), followed by automatic **Water** toggle **on** (when **off**) and display of the computed layer when calculation succeeds.

**[2.0.9]** 8. View-specific **surface water layer** results derived from the correct **basis surface** for each field content view (**§4.4**, **§6.2**, **§7.4.4**).

**[2.0.10]** 9. **Cached water layer** results retained while the associated **basis surface** and **Water settings** are unchanged, so the **Water** toggle **may** display stored results immediately without recalculation (**§6.3**, **§7.3.5**).

**[2.0.11]** 10. **Water** toggle turns **off** and **disabled** when the user switches to a field content view whose **basis surface** has no valid **cached water layer result**; **Existing** and **Cut/fill map** **shall** share one cache (**§7.3.9**).

**[2.0.12]** 11. **Water layer** draw order **below benchmarks** and **above contours** in **2D** and **3D** (**§7.4.10**).

**[2.0.13]** 12. **Water settings** and **existing surface** / **proposed surface** **cached water layer results** persisted in **`.agdesign`** project files on **Save** and restored on **Open** (**§7.5**, **Appendix A**).

**[2.0.14]** 13. A **field viewer** **viewport progress bar** while **surface water layer calculation** runs, without blocking the **UI thread** (**§6.0.6**, **§7.1.17**).

---

## 3. Non-goals

**[3.0.1]** The following are **explicitly out of scope** for **Phase 1** and **shall not** be implied by this skeleton:

**[3.0.2]** 1. A separate **Water** field content view in the **VIEW** button stack — **Water** is a **DISPLAY** decoration only (**§7.4.2**).

**[3.0.3]** 2. Storing **water layer** geometry in **tool data** or exposing it only through session-scoped **TOOLS** overlays (**Tools §4.2.3**).

**[3.0.4]** 3. Final **Water** button icon artwork — the icon **shall** remain **TBD** until specified in a later revision (**§7.2.2**).

---

## 4. Terminology

### 4.1 Water layer

**[4.1.1]** **Water layer** is project-owned map data representing **surface water** areas on the field (for example ponded water from rainfall excess, retention areas, or irrigation boundaries). The **water layer** **shall** be rendered as a **decoration** in the **field viewer** when the **Water** **DISPLAY** toggle is **on** (**§7.3**, **§7.4**).

**[4.1.2]** **Surface water layer** — the computed **water layer** result produced by ponding analysis using a **basis surface** (**§4.4**), **Water settings** (**§4.3**), and the active project. A successful **surface water layer** calculation **shall** populate **water layer data** on the active project (**§4.2.1**).

**[4.1.3]** The internal binary representation and coordinate frame of the **water layer** **shall** be defined in **Appendix A**. Symbology and opacity **may** be specified in a later revision (**§7.4.15**).

**[4.1.4]** **Terrain draping** — in **3D** orbit view, **water layer** geometry **shall** be drawn at the elevation of the active **basis surface** (**§4.4**) at each map coordinate, so the layer follows the terrain rather than a flat plane (**§7.4.6**).

**[4.1.5]** **Camera-view clipping** — in **3D** orbit view, portions of the **water layer** that lie behind closer **basis surface** terrain from the current camera **shall** be clipped and **shall not** be drawn (**§7.4.6**).

### 4.2 Water layer presence

**[4.2.1]** The project **has water layer data** when the active **`ProjectDocument`** contains at least one valid **cached water layer result** for the **existing surface** or **proposed surface** (**§4.5.1**, **§7.5**).

**[4.2.2]** The active field content view **has a water layer to show** when a valid **cached water layer result** exists for that view’s **basis surface** (**§6.2**, **§4.5.1**). The **Water** **DISPLAY** toggle **shall** be **enabled** only when the **active** field content view **has a water layer to show** (**§7.3.3**). A valid cache **shall** qualify even when the computed ponding result is uniformly dry (zero depth everywhere).

**[4.2.3]** Other field content views **may** hold separate valid **cached water layer results** for their own **basis surfaces** while the **active** view **has no water layer to show** — for example after the user calculates water on **Proposed** then switches to **Existing** before **existing surface** water has been calculated (**§7.3.9**).

### 4.3 Water settings

**[4.3.1]** **Water settings** are the project’s **ponding** parameters edited in the **Water dialog** (**§7.1**). **Water settings** **shall** persist in **`.agdesign`** project files (**Appendix A**), **shall not** be stored in the **user settings file**, and **shall** apply whenever the application computes or regenerates **water layer** data for that project until changed and saved again.

**[4.3.2]** **Ponding rainfall** — the design-storm rainfall depth used for ponding analysis, expressed in display units as an **unsigned integer** (**§7.1.8**). The canonical stored value **shall** be **`PondingRainfallMm`** in **millimetres** with default **`50`**.

**[4.3.3]** **Ponding curve number** — the unitless **curve number** used for ponding analysis, expressed as an **unsigned integer** (**§7.1.9**). The canonical stored value **shall** be **`PondingCurveNumber`** with default **`85`**.

### 4.4 Basis surface

**[4.4.1]** **Basis surface** — the elevation/topography field used as input to **surface water layer** calculation for a given field content view (**§6.2**). Each view **shall** use exactly one **basis surface** when deriving or presenting its **water layer** (**§7.4.4**).

**[4.4.2]** **Existing surface** — the project **existing topology** / as-surveyed field surface (**AgGrade Designer §2.4.3**).

**[4.4.3]** **Proposed surface** — the project **proposed topology** / design target surface (**AgGrade Designer §2.4.8**).

**[4.4.4]** **Haul cut/fill surface** — the leveled elevation field underlying the **haul cut/fill base map** (**Haul §4.6**) for one **route scrub index** **`k`** in the **Haul** field content view. While **Haul** is active, the **basis surface** **shall** be the **haul cut/fill surface** for the **active route scrub index** of the **committed** or **cached** **Haul scrub frame** (**Haul §4.4.3**, **Haul §4.7**). When **Haul** is active but no **Haul scrub frame** is available for the current index (for example before the first **scrub frame commit**), the **basis surface** **shall** be undefined and **surface water layer calculation** **shall not** run until a frame exists (**§6.2.5**, **§6.2.6**).

### 4.5 Cached water layer result

**[4.5.1]** **Cached water layer result** — a stored **surface water layer** payload bound to exactly one **basis surface** snapshot and the **Water settings** values used to compute it (**§6.3.2**). A **cached water layer result** **shall** remain valid and **may** be displayed immediately (**§7.3.5**) until invalidated per **§6.3**.

**[4.5.2]** **Basis surface unchanged** — the elevation/topography data for a **basis surface** (**§4.4**) is **unchanged** when no operation has modified the underlying surface since the **cached water layer result** was produced. Examples: survey import/replace invalidates the **existing surface**; design create/replace or **AI**-driven proposed-surface replacement invalidates the **proposed surface**; **haul data** replacement or clearing invalidates all **Haul cut/fill surface** caches (**§6.3.6**, **§6.3.7**).

**[4.5.3]** **Zero ponding (normative)** — any **basis surface** for which **surface water layer calculation** can run (**§6.2**) **may** produce a **cached water layer result**. The application **shall not** require non-zero ponding depth, a minimum ponded area, or any other “displayable feature” threshold for cache validity, toggle enablement, or persistence. An all-dry result **shall** be stored, **may** be saved in **`.agdesign`**, and **shall** enable the **Water** toggle when that **basis surface** is active (**§4.2.2**).

---

## 5. High-level architecture

### 5.1 Configuration and data ownership (normative)

**[5.1.1]** Water-related state **shall** be split across two stores:

**[5.1.2]** | Store | Contents | Examples |
|---|---|---|
| **Project file** ( **`.agdesign`** ) | **Water settings** and persisted **cached water layer results** for the **existing surface** and **proposed surface** (**§7.5**, **Appendix A**) | **`<Water>`** attributes, **`<Existing>`**, **`<Proposed>`** |
| **Session memory** | **Haul cut/fill surface** **cached water layer results** (keyed by **route scrub index** **`k`**) and other non-persisted runtime caches (**§6.2.5**, **§6.3.6**, **§7.5.3**) | **Haul** view water |

**[5.1.3]** On **Water dialog** **OK** (**§7.1.11**), the application **shall** write **Water settings** to the active **`ProjectDocument`**, **shall** start **surface water layer** calculation (**§6**) **asynchronously** for the **basis surface** of the **active field content view** (**§6.2.6**) when that cache entry is missing or **stale** (**§6.3.3**, **§7.1.14**), and **shall** mark the project **dirty** (**AgGrade Designer §4.4.1**) when **Water settings** or **water layer** payloads change. Valid **cached water layer results** for other **basis surfaces** **shall** be retained and **shall not** be recomputed on that **OK** unless **stale** per **§6.3.3**.

---

## 6. Surface water layer calculation (normative)

**[6.0.1]** **Surface water layer calculation** is the ponding analysis that derives **water layer data** from the active project and **Water settings** (**§4.3**).

**[6.0.2]** **Trigger (normative)** — the application **shall** run **surface water layer calculation** when the user clicks **OK** on the **Water dialog** after **Water settings** pass validation (**§7.1.10**) and the **Water dialog** **OK** sequence in **§7.1.11** runs. **Cancel** **shall not** run calculation.

**[6.0.3]** **Inputs (normative)** — calculation **shall** use at minimum **`PondingRainfallMm`** and **`PondingCurveNumber`** from the active project’s **Water settings** (**§4.3**, **Appendix A**) together with the **basis surface** of the **active field content view** (**§6.2.6**). Implementations **shall** treat **AgGrade** **`Application/Data/FlowMapGenerator.cs`** and its **ponding** call chain (**§8.1**) as the compatibility oracle when this section is silent on an edge case. *(Reserved — full normative algorithm mapping to be specified in a later revision.)*

**[6.0.4]** **Output (normative)** — on success, calculation **shall** store or update the **cached water layer result** for the computed **basis surface** in the appropriate store per **§5.1.2** (**`ProjectDocument`** / **`.agdesign`** for the **existing surface** and **proposed surface**; session memory for each **Haul cut/fill surface** identified by **route scrub index** **`k`**). On failure, the application **shall** retain the previous cache entry for that **basis surface** unchanged, **shall** report the failure to the user, and **shall not** enable or turn on the **Water** toggle unless the **active** field content view already **has a water layer to show** (**§4.2.2**, **§7.1.15**).

**[6.0.5]** **Completion behaviour (normative)** — when calculation completes successfully and the **active** field content view **has a water layer to show** (**§4.2.2**), the application **shall** follow **§7.1.15** (enable **Water** toggle, turn **on** if **off**, refresh viewer).

**[6.0.6]** **Threading (normative)** — **surface water layer calculation** **shall** run **off the UI thread**. The application **shall not** perform ponding analysis, flow routing, or other long-running **surface water layer** work synchronously on the **UI thread**. Progress and completion **shall** be marshalled back to the **UI thread** for overlay updates and **§7.1.15** / **§6.0.4** completion behaviour. The **UI** **shall** remain responsive to window chrome, sidebar controls, and other non-viewer interactions while calculation is in progress (**§7.1.17**).

### 6.1 Water layer data model

**[6.1.1]** **Project persistence scope (normative)** — **Water settings** and **cached water layer results** for the **existing surface** (**§4.4.2**) and **proposed surface** (**§4.4.3**) **shall** be stored in the active **`ProjectDocument`** and **shall** round-trip through **`.agdesign`** project files (**§7.5**, **Appendix A**). **Haul cut/fill surface** **cached water layer results** **shall not** be written to **`.agdesign`** in **Phase 1** (**§7.5.3**).

**[6.1.2]** *(Reserved — in-memory types, validation rules, cache-staleness tokens, and mapping from **Appendix A** payloads to render geometry to be specified in a later revision.)*

### 6.2 View basis surface mapping (normative)

**[6.2.1]** **Surface water layer** calculation **shall** derive ponding results from the **basis surface** assigned to each field content view. The mapping **shall** be:

**[6.2.2]** | Field content view | **Basis surface** |
|---|---|
| **Existing** | **Existing surface** (**§4.4.2**) |
| **Proposed** | **Proposed surface** (**§4.4.3**) |
| **Cut/fill map** | **Existing surface** (**§4.4.2**) |
| **Haul** | **Haul cut/fill surface** at the **active route scrub index** (**§4.4.4**) |

**[6.2.3]** **Existing** and **Cut/fill map** **shall** share one **cached water layer result** computed from the **existing surface**. A **surface water layer** calculation performed while **Existing** or **Cut/fill map** is the **active** field content view **shall** populate that shared cache and **shall** make the **water layer** available in **both** views (**§7.3.9**). The application **shall not** use the **Cut/fill map** depth-scalar presentation or cut/fill-adjusted display surface as the **Cut/fill map** **basis surface**.

**[6.2.4]** **Proposed** **shall** use a separate **surface water layer** result computed from the **proposed surface**.

**[6.2.5]** **Haul (normative)** — **Haul** **shall** use **cached water layer results** computed from the **haul cut/fill surface** (**§4.4.4**) for each **route scrub index** **`k`**. Each cache entry **shall** record **`k`**. While **Haul** is the **active** field content view, **surface water layer calculation** **shall** use the **haul cut/fill surface** for the **active route scrub index** of the current **committed** or **cached** **Haul scrub frame** (**Haul §4.7**). The application **may** retain valid caches for other indices **`k'`** **≠** **`k`** in session memory without recomputing them on **Water dialog** **OK** (**§6.2.6**). When the user **scrubs** to index **`k'`**, display and toggle enablement **shall** follow **§7.3.16**.

**[6.2.6]** **Water dialog OK scope (normative)** — on **Water dialog** **OK** (**§7.1.11**), the application **shall** start **surface water layer** calculation **asynchronously** (**§6.0.6**, **§7.1.14**) for the **basis surface** of the **active field content view** only — **not** for other **basis surfaces** whose caches are missing or **stale** but are not associated with the active view. When the active view is **Existing** or **Cut/fill map**, the target **basis surface** **shall** be the **existing surface** (**§6.2.3**). When the active view is **Proposed**, the target **shall** be the **proposed surface** (**§6.2.4**). When the active view is **Haul**, the target **shall** be the **haul cut/fill surface** for the **active route scrub index** when a **committed** or **cached** **Haul scrub frame** exists (**§4.4.4**, **§6.2.5**); when no frame is available, calculation **shall not** run. Calculation **shall** run when the target cache entry is missing or **stale** (**§6.3.3**); when a valid cache entry already exists for that index, **OK** **shall not** recompute it.

### 6.3 Water layer retention and invalidation (normative)

**[6.3.1]** **Retention rule (normative)** — each **cached water layer result** **shall** remain stored and **shall** be eligible for immediate display (**§7.3.5**) for as long as its associated **basis surface** is **unchanged** (**§4.5.2**) **and** the **`PondingRainfallMm`** and **`PondingCurveNumber`** recorded on the cache entry match the active project’s **Water settings** (**§4.3.1**, **Appendix A**). While valid, the application **shall not** rerun **surface water layer calculation** for that **basis surface** solely because the user toggles the **Water** decoration. Switching field content views **shall** follow **§7.3.9**; switching between **Existing** and **Cut/fill map** **shall not** by itself invalidate or hide a valid **existing surface** cache (**§6.2.3**).

**[6.3.2]** **Cache binding (normative)** — each **cached water layer result** **shall** record which **basis surface** it was computed from (**§6.2**) and the **`PondingRainfallMm`** and **`PondingCurveNumber`** values used (**Appendix A**). **Existing** and **Cut/fill map** **shall** share one **cached water layer result** bound to the **existing surface** (**§6.2.3**). **Haul cut/fill surface** cache entries **shall** additionally record the **route scrub index** **`k`** (**§6.2.5**).

**[6.3.3]** **Stale or missing cache (normative)** — a **cached water layer result** **shall** be recomputed on the next **Water dialog** **OK** (**§6.2.6**, **§7.1.14**) when that **basis surface** is the target of the **active** field content view and the cache entry is **missing** or **stale**. A cache entry **shall** be **stale** when either:

**[6.3.4]** 1. Its **basis surface** is no longer **unchanged** (**§4.5.2**), or
**[6.3.5]** 2. **`PondingRainfallMm`** or **`PondingCurveNumber`** on the project’s **Water settings** (**Appendix A**) differ from the values recorded on the cache entry.

**[6.3.6]** **Haul cut/fill cache discard (normative)** — when **haul data** is replaced or cleared, the application **shall** **discard** all **Haul cut/fill surface** **cached water layer results**. When the elevation field for a **haul cut/fill surface** at **route scrub index** **`k`** is no longer **unchanged** (**§4.5.2**), the application **shall** **discard** the cache entry bound to that **`k`**. The **Haul** **water layer** **shall not** be shown for index **`k`** until a valid **cached water layer result** is available for that **haul cut/fill surface** (**§7.4.4**).

**[6.3.7]** **Invalidation on surface change (normative)** — when the **existing surface** changes, the application **shall** **discard** the **cached water layer result** for the **existing surface** (**§6.2.3**) and **shall** remove the corresponding **`<Existing>`** payload from the active **`ProjectDocument`**. When the **proposed surface** changes — for example because the user replaces the design via **AI** or other design workflows — the application **shall** **discard** the **cached water layer result** for the **proposed surface** (**§6.2.4**) and **shall** remove the corresponding **`<Proposed>`** payload from the active **`ProjectDocument`**. Discarded entries **shall not** be displayed until recomputed. When a discard removes persisted **water layer** data, the project **shall** be marked **dirty** (**AgGrade Designer §4.4.1**). The application **shall** update **Water** toggle state per **§7.3.13**.

**[6.3.8]** **Open** / **Load** — **Open** **shall** restore **Water settings** and persisted **existing surface** and **proposed surface** **cached water layer results** from **`.agdesign`** when present (**§7.5**, **Appendix A**). When **`<Water>`** is absent, loaders **shall** apply default **Water settings** (**§7.1.8**–**§7.1.9**). Restored caches **shall** be treated as valid only if their **`<Existing>`** / **`<Proposed>`** element type still matches a loaded **basis surface**, and their recorded **`PondingRainfallMm`** and **`PondingCurveNumber`** still match the loaded surface data and restored **Water settings**; otherwise **shall** be **stale** (**§6.3.3**). **Haul cut/fill surface** caches **shall not** be restored from project files (**§7.5.3**).

---

## 7. User interface

**[7.0.1]** This section specifies **Water** user-interface obligations that extend the baseline **field viewer** and sidebar chrome (**AgGrade Designer §7.4**, **AgGrade Designer §7.5**). Bare **§N** in this section refers to **this document**.

### 7.1 Water dialog (normative)

**[7.1.1]** The application **shall** provide a modal **Water dialog** opened from the **PROJECT** **Water** button (**§7.2.8**).

**[7.1.2]** The **Water dialog** **shall** use the same general dialog chrome, **OK** / **Cancel** affordances, and theme styling as other product dialogs (for example the **design dialog** and **Haul dialog**).

**[7.1.3]** **Water settings location (normative)** — the **Water dialog** **shall** be the sole UI for editing **Water settings** (**§4.3**). The application **Settings** dialog **shall not** expose **Ponding rainfall**, **Ponding curve number**, or any other **Water settings** control.

**[7.1.4]** **Water settings controls (normative)** — the **Water dialog** **shall** expose **Ponding rainfall** and **Ponding curve number** using the same layout conventions as other product dialogs (green section title, label + control stacks).

**[7.1.5]** **Persistence and session behaviour** — **Water settings** **shall** be stored on the active **`ProjectDocument`** and **shall** round-trip through **`.agdesign`** project files (**Appendix A**). **Open** **shall** load **Water settings** from the project; **Save** **shall** write them back. The **Water dialog** **shall** populate its controls from the active project’s **Water settings** when opened. **Cancel** **shall** discard session edits without changing the project. **OK** **shall** follow **§7.1.11** (write settings to the project, close dialog, and start **surface water layer** calculation per **§6.2.6** when required).

**[7.1.6]** **Display units (normative)** — **Ponding rainfall** labels **shall** show the unit suffix appropriate to the user’s global **Units** preference (**Settings** > **Units**, **`DisplayUnitsSettings.System`**, **AI Features §4.18**). The application **shall** convert **Ponding rainfall** between display units and canonical **`PondingRainfallMm`** when populating the dialog and when reading **Water settings** for **water layer** computation. Canonical values **shall** be stored in the project file (**Appendix A**), **not** in display units.

**[7.1.7]** | Quantity | Imperial display | Metric display | Canonical field |
|---|---|---|---|
| **Ponding rainfall** | **in** (unsigned integer) | **mm** (unsigned integer) | **`PondingRainfallMm`** |
| **Ponding curve number** | *(unitless)* | *(unitless)* | **`PondingCurveNumber`** |

**[7.1.8]** **Ponding rainfall (normative)** — the **Water dialog** **shall** expose one **unsigned integer** input labelled **Ponding rainfall** with a unit suffix per **§7.1.6**–**§7.1.7**. The control **shall** accept only whole numbers **`≥ 0`**. The canonical default **shall** be **`PondingRainfallMm = 50`**. When **Units** is **Metric**, the dialog **shall** show **`50`**. When **Units** is **Imperial**, the dialog **shall** show the nearest whole **inch** equivalent of **`50 mm`** (**`2`** **in** at default).

**[7.1.9]** **Ponding curve number (normative)** — the **Water dialog** **shall** expose one **unsigned integer** input labelled **Ponding curve number** with **no** unit suffix (**§7.1.7**). The control **shall** accept only whole numbers **`≥ 0`**. The canonical default **shall** be **`PondingCurveNumber = 85`**.

**[7.1.10]** **Validation (normative)** — on **OK**, the application **shall** reject non-integer, negative, or fractional entries for either control and **shall** keep the dialog open with an appropriate validation message. Parsed values **shall** be stored as **`uint`** on the active **`ProjectDocument`** (**Appendix A**).

**[7.1.11]** **Water dialog OK — water layer calculation (normative)** — when the user clicks **OK** on the **Water dialog** and **Water settings** validation succeeds (**§7.1.10**), the application **shall**:

**[7.1.12]** 1. Write **Water settings** to the active **`ProjectDocument`** per **§7.1.5** and **Appendix A**.
**[7.1.13]** 2. Close the **Water dialog**.
**[7.1.14]** 3. When the target cache entry is missing or **stale** (**§6.3.3**), **shall** start **surface water layer calculation** (**§6**) **asynchronously** for the **basis surface** of the **active field content view** (**§6.2.6**), using the saved **Water settings** and the active project, and **shall** show **viewport progress** per **§7.1.17**. When the target cache entry is already valid, **shall not** run calculation and **shall not** show the progress overlay. **Cached water layer results** for other **basis surfaces** **shall** be left unchanged.

**[7.1.15]** 4. When **§7.1.14** step **3** runs and calculation completes successfully and the **active** field content view **has a water layer to show** (**§4.2.2**), **shall** enable the **Water** **DISPLAY** toggle if it was **disabled**, **shall** set the **Water** toggle to **on** if it is currently **off** (**§7.3.6**), and **shall** refresh the **field viewer** so the **water layer** is visible immediately in the current **2D** or **3D** mode and active field content view (**§7.4**).

**[7.1.16]** **Dirty state (normative)** — changes to **Water settings** or **water layer** data on **Water dialog** **OK** **shall** mark the open project **dirty** (**AgGrade Designer §4.4.1**) until the next successful **Save**.

**[7.1.17]** **Viewport progress during calculation (normative)** — when **surface water layer calculation** runs (**§6.0.2**, **§7.1.14**), the application **shall** show the **field viewer** busy overlay centred over the **viewport**: a horizontal **progress bar** (**0**–**100** percent) and a status message. The presentation **shall** match the **field viewer** busy overlay used for other long-running viewer operations in the shipping application (for example **AGD** import and viewer **prepare**). The status message **shall** identify **water layer** calculation (for example **Calculating water layer**).

**[7.1.18]** **Progress reporting (normative)** — the overlay **shall** appear before calculation work begins on a background thread (**§6.0.6**) and **shall** be dismissed when calculation finishes, whether it succeeds or fails (**§6.0.4**). Reported progress **shall** be **monotonic non-decreasing** for the duration of one calculation session. Implementations **shall** advance the bar from reported sub-progress when the ponding pipeline exposes it; when sub-progress is unavailable, the bar **may** advance smoothly toward **100%** without blocking the **UI thread**. Progress and message updates **shall** be applied on the **UI thread** only.

**[7.1.19]** **UI responsiveness (normative)** — while the overlay is visible, the **UI thread** **shall** remain responsive. Sidebar navigation, **VIEW** switching, **DISPLAY** toggles, and opening other dialogs **shall not** be blocked waiting for calculation to finish. Map pan, zoom, and **3D** orbit **may** remain available or **may** be temporarily suppressed by the busy overlay; in either case, input handling **shall not** stall the **UI thread** on calculation work.

**[7.1.20]** **Completion handoff (normative)** — on successful completion, the application **shall** hide the overlay and **shall** apply **§7.1.15** on the **UI thread**. On failure, the application **shall** hide the overlay, **shall** retain the previous cache per **§6.0.4**, and **shall** present the failure to the user without leaving the progress overlay visible.

**[7.1.21]** *(Reserved — extended **Water dialog** editing workflows, import/export, and additional commands to be specified in a later revision.)*

### 7.2 PROJECT sidebar Water button (normative)

**[7.2.1]** The application **shall** expose a **Water** button in the left sidebar **PROJECT** section (**AgGrade Designer §7.4.2**).

**[7.2.2]** **Button presentation (normative)** — the **Water** button **shall** use the same **`sidebar-button`** / **`sidebar-dialog-button`** layout as **Survey**, **Design**, and **Haul**:

**[7.2.3]** 1. A leading **icon** on the left — artwork **TBD**; implementations **may** use a placeholder icon until a final asset is specified (**AgGrade Designer §7.0.6**).
**[7.2.4]** 2. Centred visible label **Water**.
**[7.2.5]** 3. A trailing **right arrow** on the right, matching the chevron used by other **PROJECT** dialog-launch buttons.

**[7.2.6]** **Placement (normative)** — the **Water** button **shall** appear in the **PROJECT** section **directly below** the **Design** button (**AgGrade Designer §7.4.4**) and **directly above** the **PROJECT** **Haul** dialog-launch button when **Haul** integration is present.

**[7.2.7]** *(Reserved — **Water** button enablement rules to be specified in a later revision.)*

**[7.2.8]** **Click behaviour (normative)** — clicking the **Water** button while it is **enabled** **shall** open the **Water dialog** (**§7.1**). The click **shall not** by itself change the active field content view or toggle **DISPLAY** decorations.

### 7.3 DISPLAY Water toggle (normative)

**[7.3.1]** The application **shall** add a **Water** toggle to the right sidebar **DISPLAY** panel (**AgGrade Designer §7.5.1**).

**[7.3.2]** The visible toggle label **shall** be **Water**.

**[7.3.3]** **Enablement (normative)** — the **Water** toggle **shall** be **enabled** only when the **active** field content view **has a water layer to show** (**§4.2.2**). When the active view’s **basis surface** has no valid **cached water layer result**, the toggle **shall** be **disabled** (**§7.3.9**).

**[7.3.4]** **Manual toggle (normative)** — the user **may** turn the **Water** toggle **off** or **on** at any time while it is **enabled**. When **off**, the **water layer** **shall** be hidden while **cached water layer results** remain stored (**§7.3.5**).

**[7.3.5]** **Behaviour (normative)** — when **on** and **enabled**, the **Water** toggle **shall** show the **water layer** decoration in the **field viewer** (**§7.4**) using the valid **cached water layer result** for the active field content view (**§6.3.1**). Display **shall** be immediate when a valid cache entry exists; the toggle **shall not** trigger recalculation. When **off**, the **water layer** **shall** be hidden while **cached water layer results** remain stored.

**[7.3.6]** **Post-calculation default (normative)** — when **surface water layer calculation** completes successfully (**§6**, **§7.1.15**), the application **shall** set the **Water** toggle to **on** if it is currently **off**, so the newly computed layer is displayed.

**[7.3.7]** The **Water** toggle **shall** use the same green on/off switch styling as other **DISPLAY** toggles (**AgGrade Designer §7.5.3**).

**[7.3.8]** *(Reserved — interaction with other **DISPLAY** toggles and automatic deactivation of active map tools to be aligned with **Tools §2.0.8** in a later revision.)*

**[7.3.9]** **Field content view switch (normative)** — when the user changes the **active field content view**, the application **shall** update **Water** toggle enablement and visibility per the **basis surface** of the new view (**§6.2**):

**[7.3.10]** 1. **Missing cache** — when the new view’s **basis surface** has **no** valid **cached water layer result** (never calculated, **stale**, or **discarded**), the **Water** toggle **shall** turn **off**, **shall** be **disabled**, and the **water layer** **shall** be hidden. The user **shall** run **surface water layer calculation** again via **Water dialog** **OK** (**§7.1.11**) while that view is **active** to generate water for that **basis surface**. *Example:* user calculates water on **Proposed**, then switches to **Existing** before **existing surface** water exists — the toggle **shall** turn **off** and **shall** be **disabled** until the user runs calculation on **Existing** (or **Cut/fill map**).

**[7.3.11]** 2. **Existing** ↔ **Cut/fill map** **exception** — **Existing** and **Cut/fill map** share the **existing surface** cache (**§6.2.3**). Switching between **Existing** and **Cut/fill map** **shall not** turn the **Water** toggle **off** solely because of the view change when a valid **existing surface** **cached water layer result** exists. A calculation performed in either view **shall** make the **water layer** available in **both** views. The **Water** toggle **on**/**off** state **may** be preserved across switches between these two views when the shared cache is valid.

**[7.3.12]** 3. **Valid cache present** — when the new view’s **basis surface** has a valid **cached water layer result** (**§4.5.1**), the **Water** toggle **shall** be **enabled**. The toggle **shall** remain **off** until the user turns it **on**, except after a successful calculation on the active view (**§7.3.6**) or as preserved under rule **2** above. When **on**, display **shall** use the cache immediately (**§7.3.5**).

**[7.3.13]** **Basis surface change (normative)** — when a **basis surface** changes (**§6.3.7**), the application **shall** update **Water** toggle enablement and visibility for the **active** field content view:

**[7.3.14]** 1. **Active basis surface changed** — when the **active** view’s **basis surface** is the one that changed and its **cached water layer result** was **discarded**, the **Water** toggle **shall** turn **off** if it is currently **on**, **shall** be **disabled**, and the **water layer** **shall** be hidden. *Example:* user calculates water on **Proposed** with the toggle **on**, then replaces the **proposed surface** via **AI** — the **proposed surface** **water layer** **shall** be **discarded** and the toggle **shall** turn **off** and **shall** be **disabled** until the user runs **Water dialog** **OK** (**§7.1.11**) on **Proposed** again.

**[7.3.15]** 2. **Inactive basis surface changed** — when a **basis surface** other than the **active** view’s **basis surface** changes, the **Water** toggle **on**/**off** state and enablement for the **active** view **shall** remain unchanged. *Example:* user is on **Existing** when **proposed surface** water is **discarded** — the **Existing** toggle state **shall** be unaffected when a valid **existing surface** cache remains.

**[7.3.16]** **Haul route scrub index change (normative)** — while **Haul** is the **active** field content view, when the **route scrub index** changes to **`k'`**, the application **shall** update **Water** toggle enablement and visibility per **§7.3.10** and **§7.3.12** for the **haul cut/fill surface** at **`k'`** (**§4.4.4**, **§6.2.5**). Valid caches for other indices **may** remain in session memory; display **shall** use the cache for **`k'`** when the toggle is **on** and a valid entry exists.

### 7.4 Water layer rendering (normative)

**[7.4.1]** **Presentation modes (normative)** — the **water layer** **shall** be drawable in both **2D** plan view and **3D** orbit view (**AgGrade Designer §2.0.2**). The active **2D** / **3D** mode **shall not** disable **Water** decoration; when the **Water** toggle is **on**, the layer **shall** appear in whichever viewer mode is active.

**[7.4.2]** **View applicability (normative)** — the **water layer** **shall** be available as a **decoration** on all field content views: **Existing**, **Proposed**, **Cut/fill map**, and **Haul** (**AgGrade Designer §2.4**, **Haul §7.1**). The **Water** toggle **shall** apply to whichever field content view is currently active.

**[7.4.3]** **Basis surface per view (normative)** — when the **Water** toggle is **on** and **enabled**, the **field viewer** **shall** present the valid **cached water layer result** for the **basis surface** of the **active** field content view (**§6.2**, **§6.3.1**). Switching field content views **shall** follow **§7.3.9**; when the target view has a valid cache, display **shall** switch to that result without recalculation.

**[7.4.4]** **Haul view (normative)** — in the **Haul** field content view, the **water layer** **shall** be calculated from and displayed on the **haul cut/fill base map** (**Haul §4.6**) for the **active route scrub index** — the same **2D** cut/fill surface shown in the **Haul** viewer (**Haul §4.1.1**). The displayed **water layer** **shall** be the valid **cached water layer result** for that **haul cut/fill surface** (**§4.4.4**, **§6.2.5**), composited over the **Haul bitmap** per **§7.4.15**. When no valid cache exists for the **active route scrub index**, the **Haul** **water layer** **shall** be hidden and the toggle **shall** follow **§7.3.16**.

**[7.4.5]** **2D presentation (normative)** — in **2D** plan view, the **water layer** **shall** be drawn as a map-aligned overlay composited over the active field content view. The overlay **shall** use the same field coordinate frame as the **basis surface** presentation for that view.

**[7.4.6]** **3D presentation — terrain draping and clipping (normative)** — in **3D** orbit view, when the **Water** toggle is **on**, the **water layer** **shall** be **draped** on the **basis surface** terrain for the active field content view (**§4.4**, **§7.4.3**):

**[7.4.7]** 1. **Draping** — each drawable **water layer** feature **shall** follow the elevation of the active **basis surface** at its map coordinates. The layer **shall not** be drawn on a flat horizontal plane independent of terrain shape.

**[7.4.8]** 2. **Camera-view clipping** — portions of the **water layer** that are **obscured** by closer **basis surface** terrain from the current camera **shall** be **clipped** and **shall not** be drawn. Visible portions **shall** remain continuous along each feature except at occlusion boundaries.

**[7.4.9]** 3. **Camera motion** — when the user changes **3D** orbit heading, pitch, distance, or vertical exaggeration, the application **shall** re-evaluate draping and clipping against the updated camera and **shall** refresh the **water layer** presentation without recalculating the **cached water layer result** (**§6.3.1**).

**[7.4.10]** **Z-order (normative)** — when the **Water** toggle is **on**, the **water layer** **shall** be composited in the **field viewer** with this draw order relative to other **DISPLAY** decorations (**AgGrade Designer §7.5.2**):

**[7.4.11]** 1. **Contours** — drawn **below** the **water layer**.
**[7.4.12]** 2. **Water layer** — drawn **above contours** and **below benchmarks**.
**[7.4.13]** 3. **Benchmarks** — drawn **above** the **water layer**.

**[7.4.14]** This ordering **shall** apply in both **2D** plan view and **3D** orbit view.

**[7.4.15]** *(Reserved — symbology, **Haul** bitmap compositing, and opacity rules to be specified in a later revision.)*

### 7.5 Project file persistence (normative)

**[7.5.1]** **Save** / **Open** (normative) — when the user **Save**s a **`ProjectDocument`**, the application **shall** embed **Water settings** and **cached water layer results** for the **existing surface** and **proposed surface** in the **`.agdesign`** file per **Appendix A**. **Open** **shall** deserialize **Water settings** and any present cache payloads into the loaded project (**§6.3.8**). Omitting absent or empty cache entries **shall** be valid; when no **Water settings** have been saved yet, **Save** **shall** still write default or current **Water settings** if the user has opened the **Water dialog** or computed water for the project.

**[7.5.2]** **Water layer** data and **Water settings** **shall** be **project-owned** (**AgGrade Designer §4.2.1**). Changes to either **shall** mark the project **dirty** (**AgGrade Designer §4.4.1**) until the next successful **Save**.

**[7.5.3]** **Non-persisted caches (normative)** — **Haul cut/fill surface** **cached water layer results** **shall not** be written to **`.agdesign`** in **Phase 1**.

---

## 8. Reference implementation (AgGrade)

**[8.0.1]** **AgGrade Designer** **surface water layer** calculation **shall** be compatible with the in-cab **AgGrade** **ponding** pipeline unless this document **explicitly** documents an incompatible change. Paths below are relative to the **AgGrade reference tree** (**§0.0.15**).

### 8.1 FlowMapGenerator (normative)

**[8.1.1]** The reference implementation **shall** be **`Application/Data/FlowMapGenerator.cs`** — class **`AgGrade.Data.FlowMapGenerator`**.

**[8.1.2]** **`FlowMapGenerator`** **shall** be treated as the authoritative **AgGrade** entry point for **elevation-surface typing** and flow-raster utilities used by the **surface water layer** (**ponding**) pipeline:

**[8.1.3]** 1. **`ElevationTypes`** — in-cab **AgGrade** enum with values **`Initial`**, **`Current`**, and **`Target`**. Each value selects which per-bin elevation field (**`InitialElevationM`**, **`CurrentElevationM`**, **`TargetElevationM`**) is used when building DEM inputs for flow or ponding analysis (**`Field.GenerateElevationDEM`**). **`Initial`** (pre-grade as-imported elevation in the cab session) **does not apply** in **AgGrade Designer** — **surface water layer** calculation **shall** derive elevations from **basis surface** topology (**§4.4**, **§6.2**) and **shall not** use **`ElevationTypes.Initial`** or map any **basis surface** to it.

**[8.1.4]** 2. **`GenerateFlowDEM`** — runs WhiteboxTools **D-Infinity** flow accumulation on a georeferenced DEM GeoTIFF (matches **`demtoflow.py`** behaviour).
**[8.1.5]** 3. **`ConvertFlowDEMtoPNG`** — converts a flow accumulation TIFF to an overlay PNG (**0** = white, **10** = black; optional alpha for compositing).

**[8.1.6]** **Ponding call chain (informative).** In-cab **AgGrade** generates the **ponding** overlay — the in-cab equivalent of **AgGrade Designer** **surface water layer** (**§4.1.2**) — through:

**[8.1.7]** 1. **`Application/Data/MapGenerator.cs`** — **`CalculatePonding`**, which accepts a **`FlowMapGenerator.ElevationTypes`**, **`CurveNumber`**, **`RainfallMm`**, and opacity, then calls **`PondingMapGenerator.GeneratePondingPNG`**.
**[8.1.8]** 2. **`Application/Data/PondingMapGenerator.cs`** — SCS **Curve Number** runoff, **D8** flow routing, and depression fill/spill (matches **`AI Flow Analysis/ponding.py`**).
**[8.1.9]** 3. **`Application/Controls/Map.cs`** — **`ShowPondingMap`**, which reads **`PondingRainfallMm`** and **`PondingCurveNumber`** from app settings and invokes **`MapGenerator.CalculatePonding`**.
**[8.1.10]** 4. **`Application/Controls/AppSettingsEditor.cs`** — validates and persists **`PondingRainfallMm`** (default **`50`**) and **`PondingCurveNumber`** (default **`85`**).

**[8.1.11]** **Designer integration (normative)** — **AgGrade Designer** **shall** produce **cached water layer results** (**§6.3**) compatible with the **ponding** model in **`PondingMapGenerator`** for the mapped **basis surface** (**§6.2**), using **`PondingRainfallMm`** and **`PondingCurveNumber`** from **Water settings** (**§7.1**). **AgGrade Designer** **shall not** reuse in-cab **`MapGenerator`** heading-rotated tile caches or cab map rotation (**AgGrade Designer §8.2.4**); it **may** adapt or rehost **`FlowMapGenerator`** / **`PondingMapGenerator`** logic for **north-up** **2D** and **3D** decoration (**§7.4**).

**[8.1.12]** **Basis surface mapping (normative)** — when adapting in-cab **`FlowMapGenerator`** / **`PondingMapGenerator`** call chains for **AgGrade Designer**, **basis surfaces** **shall** map to **`ElevationTypes`** as follows. **`ElevationTypes.Initial`** **shall not** be used.

**[8.1.13]** | **AgGrade Designer basis surface** (**§4.4**) | **`FlowMapGenerator.ElevationTypes`** |
|---|---|
| **Existing surface** | **`Current`** |
| **Proposed surface** | **`Target`** |
| **Haul cut/fill surface** (per **route scrub index** **`k`**, **§4.4.4**) | *(none — use the **haul cut/fill base map** elevation field directly; **Haul §4.6**, **§6.2.5**)* |

**[8.1.14]** The **existing surface** (**§4.4.2**) **shall** map to **`Current`**; **Existing** and **Cut/fill map** field content views **shall** share that mapping through the shared **existing surface** cache (**§6.2.3**). Implementations **may** bypass **`ElevationTypes`** entirely when feeding **`PondingMapGenerator`** directly from **AgGrade Designer** topology arrays, provided the elevation source matches **§8.1.13**.

---

## 9. Automated testing

**[9.0.1]** **Test suite placement (normative)** — unit and integration tests for **Water** **shall** be added to the existing **`Application/AgGradeDesigner.Core.Tests`** project, following the same patterns as other field-viewer rendering tests (for example **`MeshDepthBufferTests`**, **`HaulFlowArrowRenderer3DTests`**).

**[9.0.2]** **Phase 1** tests **shall** cover at least:

**[9.0.3]** 1. **Water** **DISPLAY** toggle **disabled** when the **active** field content view **has no water layer to show** (**§4.2.2**, **§7.3.3**).
**[9.0.4]** 2. **Water** **DISPLAY** toggle **enabled** when the **active** view has a valid **cached water layer result** (**§4.2.2**, **§7.3.3**), including when ponding depth is zero everywhere (**§4.5.3**).
**[9.0.5]** 3. **Water dialog** **OK** runs **surface water layer calculation** and turns the **Water** toggle **on** when it was **off** (**§6**, **§7.1.11**–**§7.1.15**, **§7.3.6**).
**[9.0.6]** 4. **PROJECT** **Water** button click opens the **Water dialog** (**§7.2.8**).
**[9.0.7]** 5. **Water settings** round-trip through **Save** / **Open** in **`.agdesign`** (**Appendix A §A.0.3**–**§A.0.6**).
**[9.0.8]** 6. Saving a **`ProjectDocument`** to **`.agdesign`** **shall** embed **Water settings** and **existing surface** / **proposed surface** **water layer** payloads when present (**§7.5**, **Appendix A**) and **shall not** embed **Haul cut/fill surface** water (**§7.5.3**).
**[9.0.9]** 7. **Ponding rainfall** display-unit conversion for **metric** (**mm**) and **imperial** (**in**) labels (**§7.1.6**–**§7.1.8**), including default **`50 mm`** / **`2 in`** presentation.
**[9.0.10]** 8. **Ponding curve number** defaults to **`85`** when **`<Water>`** is absent on **Open** (**§7.1.9**, **Appendix A §A.0.4**).
**[9.0.11]** 9. **Settings** dialog **shall not** expose **Water settings** controls (**§7.1.3**).
**[9.0.12]** 10. **Water layer** visible with toggle **on** in **2D** and **3D** on **Existing**, **Proposed**, **Cut/fill map**, and **Haul** views (**§7.4.1**–**§7.4.2**). **3D** presentation **shall** follow **§7.4.6**–**§7.4.9**.
**[9.0.13]** 11. **Basis surface** mapping — **Existing** and **Cut/fill map** use **existing surface**; **Proposed** uses **proposed surface**; **Haul** uses **haul cut/fill surface** at the **active route scrub index** (**§6.2**, **§7.4.3**–**§7.4.4**).
**[9.0.14]** 12. **Cut/fill map** **water layer** **shall not** be derived from the cut/fill depth display surface (**§6.2.3**).
**[9.0.15]** 13. Valid **cached water layer result** for the **existing surface** **shall** display immediately when the **Water** toggle is turned **on** without recalculation (**§6.3.1**, **§7.3.5**).
**[9.0.16]** 14. **Water dialog** **OK** **shall not** recompute **surface water layer** results whose **basis surface** and **Water settings** are unchanged (**§6.3.1**, **§7.1.14**).
**[9.0.17]** 15. Replacing or clearing **haul data** **shall** discard all **Haul cut/fill surface** **cached water layer results** (**§6.3.6**, **§7.4.4**).
**[9.0.18]** 16. Changing the **existing surface** or **proposed surface** **shall** **discard** the corresponding **cached water layer result** (**§6.3.7**).
**[9.0.19]** 17. Switching from **Proposed** (with valid cache, toggle **on**) to **Existing** (no **existing surface** cache) **shall** turn the **Water** toggle **off** and **disabled** (**§7.3.9**–**§7.3.10**).
**[9.0.20]** 18. **Water dialog** **OK** on **Existing** after step **17** **shall** compute **existing surface** water only and **shall not** recompute **proposed surface** cache (**§6.2.6**, **§7.1.14**).
**[9.0.21]** 19. Switching between **Existing** and **Cut/fill map** **shall** preserve **Water** toggle availability when **existing surface** cache is valid (**§7.3.11**).

**[9.0.22]** 20. **Proposed surface** change while **Proposed** is the **active** view with **Water** toggle **on** — for example **AI** proposed-surface replacement — **shall** **discard** the **proposed surface** **water layer**, **shall** turn the toggle **off**, and **shall** **disable** the toggle (**§6.3.7**, **§7.3.13**–**§7.3.14**).

**[9.0.23]** 21. **3D terrain draping** — sampled **water layer** geometry in **3D** **shall** follow the active **basis surface** elevation at map coordinates and **shall not** use a flat plane independent of terrain (**§7.4.7**).

**[9.0.24]** 22. **3D camera-view clipping** — when a closer **basis surface** mesh occludes **water layer** geometry from the camera, the obscured portions **shall** not be drawn (**§7.4.8**). Tests **shall** include at least one regression case using a synthetic full-screen occluder depth buffer that suppresses previously visible **water layer** pixels, analogous to **`HaulFlowArrowRenderer3DTests.Draw3D_FullScreenOccluder_SuppressesArrowStrokes`**.

**[9.0.25]** 23. **3D camera refresh** — changing orbit heading, pitch, distance, or vertical exaggeration **shall** refresh **water layer** draping and clipping without invalidating or recomputing the **cached water layer result** (**§7.4.9**, **§6.3.1**).

**[9.0.26]** 24. **Z-order** — with **Contours**, **Water**, and **Benchmarks** toggles **on**, **water layer** pixels **shall** appear **above contours** and **below benchmarks** (**§7.4.10**–**§7.4.14**).

**[9.0.27]** 25. **Water settings**, **existing surface** cache, and **proposed surface** cache **shall** round-trip through **Save** / **Open** in **`.agdesign`** (**§7.5**, **Appendix A**).

**[9.0.28]** 26. **BASE64** **`<Payload>`** text **shall** encode and decode the binary **water layer** blob losslessly (**Appendix A §A.0.12**).

**[9.0.29]** 27. **Viewport progress** — when **surface water layer calculation** runs after **Water dialog** **OK**, the **field viewer** **shall** show the busy overlay with a **progress bar** (**§7.1.17**–**§7.1.18**). Tests **shall** assert that calculation is invoked **off the UI thread** (for example by running ponding work on a thread-pool or background task and verifying the **UI thread** is not blocked for the duration of the work).

**[9.0.30]** 28. **Progress dismissal** — on calculation success or failure, the overlay **shall** be hidden and **shall not** remain visible after completion (**§7.1.20**).

---

## 10. Summary

**[10.0.1]** *(Reserved — to be written in a later revision.)*

---

## Appendix A — `.agdesign` water extension (normative)

**[A.0.1]** This appendix **extends** **AgGrade Designer Appendix A** with embedded **Water settings** and **water layer** data for the **existing surface** and **proposed surface** (**§7.5**). Files **shall** remain **UTF-8** XML consistent with **AgGrade Designer §4.2**.

**[A.0.2]** **Root element (normative)** — the project document **shall** include an optional **`<Water>`** element as a direct child of the project root (alongside survey, design, haul, and viewer state). When **`<Water>`** is absent on **Open**, loaders **shall** apply default **Water settings** (**§7.1.8**–**§7.1.9**) and **shall** treat the project as having no persisted **water layer** cache entries.

**[A.0.3]** **Water settings (normative)** — the **`<Water>`** element **shall** carry attributes for the project’s current **Water settings** (**§4.3**):

**[A.0.4]** 1. **`pondingRainfallMm`** — **`uint`**, canonical millimetres (**§4.3.2**). Default **`50`** when **`<Water>`** is absent.
**[A.0.5]** 2. **`pondingCurveNumber`** — **`uint`**, unitless curve number (**§4.3.3**). Default **`85`** when **`<Water>`** is absent.

**[A.0.6]** **Save** / **Open** (normative) — **Save** **shall** write the active project’s **Water settings** to **`<Water>`** attributes. **Open** **shall** read them into **`ProjectDocument`**. The **Water dialog** **shall** display the loaded values when opened.

**[A.0.7]** **Child elements (normative)** — **`<Water>`** **may** contain zero or one **`<Existing>`** element and zero or one **`<Proposed>`** element:

**[A.0.8]** | Element | **Basis surface** | Field content views |
|---|---|---|
| **`<Existing>`** | **Existing surface** (**§4.4.2**) | **Existing**, **Cut/fill map** (**§6.2.3**) |
| **`<Proposed>`** | **Proposed surface** (**§4.4.3**) | **Proposed** |

**[A.0.9]** **Cache metadata (normative)** — each **`<Existing>`** and **`<Proposed>`** element **shall** carry attributes recording the **Water settings** values used when that payload was produced (**§6.3.2**):

**[A.0.10]** 1. **`pondingRainfallMm`** — **`uint`**, canonical millimetres (**§4.3.2**).
**[A.0.11]** 2. **`pondingCurveNumber`** — **`uint`**, unitless curve number (**§4.3.3**).

**[A.0.12]** **Binary payload (normative)** — each **`<Existing>`** and **`<Proposed>`** element **shall** contain exactly one **`<Payload>`** child whose text content is the **BASE64** encoding of the opaque binary **water layer** blob for that **basis surface**. Encoders **shall** use the standard **BASE64** alphabet defined in [RFC 4648](https://www.rfc-editor.org/rfc/rfc4648) **§4** with **no** line breaks and **no** padding variants other than standard **`=`** padding. Decoders **shall** reject malformed **BASE64** text and **shall** treat the corresponding cache entry as missing.

**[A.0.13]** **Example layout (informative):**

```xml
<Water pondingRainfallMm="50" pondingCurveNumber="85">
  <Existing pondingRainfallMm="50" pondingCurveNumber="85">
    <Payload>…BASE64…</Payload>
  </Existing>
  <Proposed pondingRainfallMm="50" pondingCurveNumber="85">
    <Payload>…BASE64…</Payload>
  </Proposed>
</Water>
```

**[A.0.14]** **Schema versioning (normative)** — when **`<Water>`** is first added to the **`.agdesign`** schema, increment the project document schema version per **AgGrade Designer Appendix A** conventions. Loaders **shall** ignore unknown attributes on **`<Water>`**, **`<Existing>`**, **`<Proposed>`**, and **`<Payload>`** elements.

**[A.0.15]** *(Reserved — internal binary blob layout inside decoded **`<Payload>`** bytes to be specified in a later revision.)*
