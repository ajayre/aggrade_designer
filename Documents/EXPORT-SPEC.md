# AgGrade Control Export Specification **[0.0.1]**

**[0.0.2]** Every normative paragraph carries a bold marker **`[<S>.<U>.<P>]`** where **`<S>`** is the section, **`<U>`** is the subsection (**`0`** for body text before the first **`###`**), and **`<P>`** is the 1-based paragraph index within that group.

**[0.0.3]** This document specifies how **AgGrade Designer** **exports** field and haul results to **AgGrade Control** as a single **`.agcontrol`** file: what data is included, how that file is structured, and how the application produces it.

**[0.0.4]** - **Project name:** AgGrade Designer — AgGrade Control Export
**[0.0.5]** - **Document type:** Application-layer export specification
**[0.0.6]** - **Language / implementation:** C# (.NET 8), Avalonia UI, MVVM — source **shall** follow **ANDY-CSHARPCODINGSTYLEGUIDE.md**; on-disk format is **SQLite 3**
**[0.0.7]** - **Target platforms:** Windows, Linux, macOS (same as **AGGRADE-DESIGNER-SPEC-1.0.md**)
**[0.0.8]** - **Scope:** Normative definition of the **`.agcontrol`** export file for **AgGrade Control**, including **haul plan**, **pivot movements**, **haul stats**, **flow arrows**, **existing topology**, and **proposed topology** (**§4**, **§5**, **§6**, **Appendix A**). Export command UI and automated tests **may** be added in later revisions.
**[0.0.9]** - **Relationship to baseline:** **AGGRADE-DESIGNER-SPEC-1.0.md** remains the authoritative contract for the core application. Baseline survey export (**`.ags`**, **`.txt`**) and design export (**`.agd`**) (**AgGrade Designer §3.4**, **AgGrade Designer §9**) remain separate deliverables. This document defines a **new** export path and file type for **AgGrade Control**.
**[0.0.10]** - **Relationship to haul:** **HAUL-SPEC.md** defines **haul data** and **Haul Builder** integration. A **haul plan** is **optional** for **`.agcontrol`** export (**§7.1**). When **haul data** is present, export **shall** include haul payloads (**§6.1**–**§6.4**); when absent, haul tables **shall** be empty or omitted per **§6.1.10**. Persistence of **haul data** inside **`.agdesign`** remains governed by **HAUL-SPEC.md**.
**[0.0.11]** - **Relationship to AgGrade Control:** **`.agcontrol`** is the **Designer → Control** interchange format. It **should** reuse familiar Control table names (**`FieldState`**, **`Data`**, **`HaulPaths`**, **`HaulArrows`**, **`Benchmarks`**) where those concepts still apply, but it **is not** required to be openable by today’s Control field-database code without changes. **Control will be updated** to read this schema. **Backwards compatibility** with older Control field databases / loaders **is not** a goal of this document (**§5.1.5**).
**[0.0.12]** - **Relationship to AI features / tools / water:** **AI-FEATURES-SPEC.md**, **TOOLS-SPEC.md**, and **WATER-SPEC.md** are out of scope for **`.agcontrol`** unless a future revision explicitly adds them.
**[0.0.13]** - **Assumed knowledge:** Readers **shall** already know **AGGRADE-DESIGNER-SPEC-1.0.md**, **HAUL-SPEC.md**, and Control’s current field-database concepts (informative: **`AgGrade/Application/Data/Database.cs`**). In this document, bare **§N** refers to **this document**; **AgGrade Designer §N** refers to the baseline spec; **Haul §N** refers to **HAUL-SPEC.md**; **Control** means **AgGrade Control**.
**[0.0.14]** - **Related documents:** **ANDY-SPECSTYLEGUIDE.md**, **ANDY-CSHARPCODINGSTYLEGUIDE.md**, **AGGRADE-DESIGNER-SPEC-1.0.md**, **HAUL-SPEC.md**. Informative Control reference: **`AgGrade/Application/Data/Database.cs`**, **`FieldImporter.cs`**, **`FieldCreator.cs`**, **`Field.cs`**.

**[0.0.15]** **Version axes:**

**[0.0.16]** 1. **Specification document** version (this markdown file; semver in **§0.1**).
**[0.0.17]** 2. **Application** version (shipping product semver; distinct from this document).
**[0.0.18]** 3. **`.agcontrol` file format** version — **`PRAGMA user_version`** (and optional **`Data`** markers); distinct from this document.

**[0.0.19]** Incrementing one version axis **does not** imply incrementing another.

**[0.0.20]** **Phased delivery:**

**[0.0.21]** 1. **Phase 1** — **SQLite** schema and payload mapping (**§5**, **§6**): topology, **Data**, **Benchmarks**, expanded **`HaulPaths`**, **`HaulArrows`**, **`HaulRoutes`** (stats), **`PivotMoves`**.
**[0.0.22]** 2. **Phase 2** — *(Reserved — export UI, validation, automated tests, and Control-side import of **`.agcontrol`**.)*

**[0.0.23]** **Phase 1** **shall not** be withheld until **Phase 2** features exist.

### 0.1 Specification revision history

**[0.1.1]**

| Version | Date | Change |
|---|---|---|
| **0.1.0** | 2026-07-10 | Initial skeleton: metadata, purpose, design goals, non-goals, terminology stubs, payload inventory. |
| **0.2.0** | 2026-07-10 | **(Decided)** **`.agcontrol`** is **SQLite 3**; map Designer payloads onto Control-like tables; additive tables for Designer-only data. |
| **0.3.0** | 2026-07-10 | **(Decided)** No Control backwards-compat requirement; **expand `HaulPaths`** with HaulBuilder guidance columns; **`PivotMoves`** own table; **`HaulRoutes`** for per-route stats (**§5**, **§6**, **§8**, **Appendix A**). |
| **0.4.0** | 2026-07-10 | **(Decided)** Export **disabled** without a **proposed surface**; **haul plan** optional (**§7.1**, **§6.1.7**). |
| **0.5.0** | 2026-07-10 | **(Decided)** **`FieldState.HaulPath`** set on the **2 ft** design bin under the **blade centre** at **cut start** (**§6.1.4**). |
| **0.5.1** | 2026-07-10 | Multiple passes **may** share a cut-start design bin at **different cutting depths**; removed last-wins overwrite (**§6.1.4**–**§6.1.7**, **§8.0.11**). |
| **0.6.0** | 2026-07-10 | **(Decided)** Guidance is an **ordered** cut-fill plan (**1…n**); each pair has a **blade depth**; revisiting the same cut-start bin is expected; Control follows plan order, not bin→single-path lookup (**§4.2**, **§4.8**, **§6.1**). |
| **0.7.0** | 2026-07-10 | **(Decided)** **`Data.TotalCutCY`/`TotalFillCY`** use **design** totals; **`PRAGMA user_version = 2`** (Control field DB is **1**) (**§5.1.3**, **§6.3.1**). |
| **0.8.0** | 2026-07-10 | Proposed normative **Appendix A** DDL for topology tables, expanded **`HaulPaths`**, **`HaulRoutes`**, **`PivotMoves`**, **`PivotConfig`**, **`PivotPassBearings`**. |

**[0.1.2]** This document follows [semantic versioning](https://semver.org/): **MAJOR** = incompatible normative change, **MINOR** = backward-compatible addition, **PATCH** = clarification.

---

## 1. Purpose

**[1.0.1]** **AgGrade Designer** **shall** be able to **export** a field’s design and haul results to **AgGrade Control** as a single **SQLite 3** database file with extension **`.agcontrol`**.

**[1.0.2]** The schema **shall** carry Designer haul products cleanly (expanded **`HaulPaths`**, **`HaulRoutes`**, **`PivotMoves`**) while keeping topology and map overlays in familiar Control-shaped tables. Control **shall** be updated to consume this format; preserving today’s Control loader behaviour is **not** required.

**[1.0.3]** Obligations in this document use **shall**, **should**, **may** as defined in **ANDY-SPECSTYLEGUIDE.md §6**.

---

## 2. Design goals

**[2.0.1]** The implementation **shall provide**:

**[2.0.2]** 1. One **`.agcontrol`** **SQLite 3** file per export (**§5**).
**[2.0.3]** 2. Familiar Control-shaped tables for topology and overlays: **`FieldState`**, **`Data`**, **`HaulArrows`**, **`Benchmarks`** (**§5.2**).
**[2.0.4]** 3. An **expanded `HaulPaths`** table that stores each HaulBuilder **guidance point** when a haul plan is present (**§5.3**, **§6.1**).
**[2.0.5]** 4. A **`HaulRoutes`** table for ordered **cut-fill pairs** (stats, blade depth, cut-start bin) when a haul plan is present (**§5.4**, **§6.1**, **§6.3**).
**[2.0.6]** 5. A **`PivotMoves`** table for pivot schedule and geometry when pivot data is present (**§5.5**, **§6.2**).
**[2.0.7]** 6. Mapping of **existing** / **proposed topology** into **`FieldState`** + **`Data`** (**§6.5**, **§6.6**).
**[2.0.8]** 7. Mapping of **flow arrows** into **`HaulArrows`** when a flow field is present (**§6.4**).
**[2.0.9]** 8. Survey **benchmarks** in **`Benchmarks`** (**§6.7**).
**[2.0.10]** 9. Export enablement that **requires** a **proposed surface** and treats a **haul plan** as **optional** (**§7.1**).

---

## 3. Non-goals

**[3.0.1]** The following are **explicitly out of scope** and **shall not** be implied:

**[3.0.2]** 1. Replacing baseline survey export (**`.ags`**, **`.txt`**) or design export (**`.agd`**).
**[3.0.3]** 2. Import of **`.agcontrol`** back into **AgGrade Designer**.
**[3.0.4]** 3. Embedding **AI** chat history, **user settings**, session **tool data**, or **water layer** data.
**[3.0.5]** 4. Keeping **`.agcontrol`** openable by **unmodified** current Control **`Database`/`Field`** loaders (**§5.1.5**).
**[3.0.6]** 5. Exact **Export** UI placement — reserved for **§7**.

---

## 4. Terminology

### 4.1 AgGrade Control export file

**[4.1.1]** An **AgGrade Control export file** is a single **SQLite 3** database with extension **`.agcontrol`** produced by **AgGrade Designer** for consumption by **AgGrade Control**.

### 4.2 Haul plan / guidance plan

**[4.2.1]** A **haul plan** (also **guidance plan**) is the ordered sequence of **cut-fill pairs** produced by Designer for in-cab execution. It is stored as **`HaulRoutes`** (one row per pair, in order **1…n**) plus **`HaulPaths`** guidance points for each pair.

**[4.2.2]** The operator **shall** execute the plan **in order**: pair **1**, then **2**, then **3**, … through **n**. Control **shall** advance through that sequence; it **shall not** treat “which path belongs to this bin?” as the primary way to choose the next work.

### 4.3 Cut-fill pair

**[4.3.1]** A **cut-fill pair** is one ordered unit of the guidance plan (one Designer pass / route): cut work, haul, and fill, with an associated **blade depth** for that pair.

**[4.3.2]** Different cut-fill pairs **may** start cutting in the **same** **2 ft** design bin when their **blade depths** differ. The plan **may** return to that bin on a later pair; that is expected.

### 4.4 Pivot movements

**[4.4.1]** **Pivot movements** are pivot schedule records from Designer (**`HaulPivotOverlayDto`** / settings), stored in **`PivotMoves`**.

### 4.5 Haul stats

**[4.5.1]** **Haul stats** are plan- and route-level aggregates. Plan totals that Control already uses conceptually (**`TotalCutCY`**, **`TotalFillCY`**) **shall** use **`Data`**; per-pair stats **shall** use **`HaulRoutes`**.

### 4.6 Flow arrows

**[4.6.1]** **Flow arrows** are directional samples written to **`HaulArrows`** as **`Latitude`**, **`Longitude`**, **`Heading`**.

### 4.7 Existing topology / proposed topology

**[4.7.1]** **Existing topology** and **proposed topology** are the design-grid elevations exported as **`FieldState.InitialHeightM`** / **`CurrentHeightM`** (existing) and **`FieldState.TargetHeightM`** (proposed), with grid metadata in **`Data`**.

### 4.8 Proposed surface

**[4.8.1]** The project **has a proposed surface** when a valid **design grid** with proposed elevations is present on the active project (the same condition that makes the **Proposed** field content view meaningful — **`DesignGrid`** with proposed bin data). Exact UI/project predicates **may** align with baseline design-present checks (**AgGrade Designer §5**).

**[4.8.2]** Without a **proposed surface**, **`.agcontrol`** export **shall** be **disabled** (**§7.1**).

### 4.9 Cut-start bin

**[4.9.1]** The **cut-start bin** for a cut-fill pair is the **2 ft** design-grid bin under the **middle of the blade** when cutting starts for that pair. It is metadata on the pair (**`HaulRoutes`**), not a claim that the bin “owns” only one path.

---

## 5. File format overview

### 5.1 Container **(Decided)**

**[5.1.1]** **`.agcontrol`** **shall** be a **SQLite 3** database file (not a zip, not a custom binary envelope).

**[5.1.2]** The file extension **shall** be **`.agcontrol`**.

**[5.1.3]** Export **shall** set **`PRAGMA user_version = 2`**. Control’s current field-database schema uses **`user_version = 1`** (**`Database.FieldCreatorSchemaStatements`**); **`.agcontrol`** **shall** use the next integer so Control can distinguish Designer export databases from legacy Field Creator / AGD-import databases.

**[5.1.4]** Export **should** write a **`Data`** provenance marker identifying Designer as the producer (exact key **TBD** in **Appendix A**).

**[5.1.5]** **No Control backwards compatibility (Decided).** This format **shall not** be constrained to load under today’s Control **`Database.Open`** / **`Field`** code. Control **shall** be updated to read expanded **`HaulPaths`**, **`HaulRoutes`**, and **`PivotMoves`**. Older Field Creator / AGD-import databases remain a separate Control concern outside this document.

### 5.2 Shared topology / overlay tables **(Decided)**

**[5.2.1]** **`.agcontrol`** **shall** include **`FieldState`**, **`Data`**, **`HaulArrows`**, and **`Benchmarks`** with the same *roles* Control uses today (bin heights, grid metadata, flow arrows, benchmarks). Column sets **may** match Control’s current DDL where sufficient; Control import of **`.agcontrol`** **may** adapt as needed.

**[5.2.2]**

| Table | Role at export |
|---|---|
| **`FieldState`** | One row per design bin: existing / current / proposed heights, centroid; **`HaulPath`** column **`0`** for Designer guidance exports (**§6.1.6**) |
| **`Data`** | Grid extents, mean lat/lon, totals, calibration flags, offsets |
| **`HaulArrows`** | Flow-arrow samples (lat/lon/heading) |
| **`Benchmarks`** | Survey benchmarks |
| **`Events`** / leveling history | Optional; empty at export if present (in-cab only) |

**[5.2.3]** Units: elevations in **metres**; **`Data`** volume keys that Control already names **`*CY`** remain **cubic yards**; positions as **WGS-84** lat/lon where those columns exist; UTM extents in **`Data.MinX`/`MinY`/`MaxX`/`MaxY`** as metres.

### 5.3 Expanded `HaulPaths` **(Decided)**

**[5.3.1]** **`HaulPaths`** **shall** be the single table for HaulBuilder **guidance points**. It **shall** keep the existing identity columns Control already understands conceptually (**`HaulPath`**, **`PointNumber`**, **`Latitude`**, **`Longitude`**) and **shall** add columns for the remaining **`HaulGuidancePointDto`** fields.

**[5.3.2]** Provisional column set (normative DDL in **Appendix A**):

**[5.3.3]**

```text
HaulPaths (
  HaulPathID INTEGER PRIMARY KEY AUTOINCREMENT,
  HaulPath INTEGER,          -- 1-based route / pass id (same as PassNo)
  PointNumber INTEGER,       -- order along the path (Seq or dense index)
  Latitude REAL,
  Longitude REAL,
  -- expanded HaulBuilder guidance:
  Seq INTEGER,               -- 1-based global sequence
  Action TEXT,               -- e.g. CUT / FILL / ...
  Blade TEXT,                -- blade state for inbound leg
  EastingM REAL,             -- UTM easting (guidance X)
  NorthingM REAL,            -- UTM northing (guidance Y)
  DepthMm REAL,              -- target blade depth
  HeadingDeg REAL            -- heading to next waypoint (0=N, 90=E)
)
```

**[5.3.4]** Rationale: guidance is already a polyline of points; putting HaulBuilder attributes on those rows avoids a parallel guidance table and keeps one authoritative path representation for Control to adopt.

**[5.3.5]** Per-route aggregates (load BCY, haul distance, timing, sweep endpoints) **shall not** be duplicated on every **`HaulPaths`** row; those belong in **`HaulRoutes`** (**§5.4**).

### 5.4 `HaulRoutes` (per-route stats) **(Decided)**

**[5.4.1]** **`HaulRoutes`** **shall** store one row per ordered **cut-fill pair**: route id (**1…n**), **blade depth**, **cut-start** design bin (and/or cut-start UTM), load, distance, timing, sweep endpoints, and related stats from **`HaulPassDto`** / **`HaulCutFillPairDto`**.

**[5.4.2]** **`HaulRoutes`** route id **shall** match **`HaulPaths.HaulPath`** for that pair.

**[5.4.3]** Column list **shall** follow **Appendix A §A.3** (proposed).

### 5.5 `PivotMoves` / pivot metadata **(Decided)**

**[5.5.1]** Pivot schedule rows **shall** live in **`PivotMoves`**. Pivot centre and radius **shall** live in **`PivotConfig`** (one row when pivot data exists). Per-pass held bearings **shall** live in **`PivotPassBearings`**. Pivot data **shall not** be forced into **`HaulPaths`** or **`HaulArrows`**.

**[5.5.2]** Column lists **shall** follow **Appendix A §A.4** (proposed). Source: **`HaulPivotOverlayDto`**, **`HaulPivotMoveDto`**, **`HaulSettings.PivotLatitudeDeg`** / **`PivotLongitudeDeg`**.

### 5.6 Forward evolution

**[5.6.1]** New columns or tables in later **`.agcontrol`** revisions **shall** bump **`PRAGMA user_version`** (and this specification). Control import **shall** target the versions it supports explicitly.

---

## 6. Export payload mapping

**[6.0.1]** This section maps Designer in-memory sources onto **`.agcontrol`** tables.

### 6.1 Haul plan → expanded `HaulPaths` + `HaulRoutes`

**[6.1.1]** A **haul plan** is **optional** (**§7.1.3**). When the project **has haul data**, export **shall** write the ordered **guidance plan** as **`HaulRoutes`** (cut-fill pairs **1…n**) and **`HaulPaths`** (guidance points per pair).

**[6.1.2]** **Execution model (Decided):** Control **shall** present and execute cut-fill pairs **in plan order** (**1**, **2**, **3**, … **n**). Each pair has its own **blade depth**. The plan **may** return to the same **cut-start bin** on a later pair at a different depth (**§4.2**, **§4.3**).

**[6.1.3]** **Path id model (Decided):** each cut-fill pair **shall** receive one positive integer **`HaulPath`** / route id equal to its **1-based** order index (**`PassNo`**). All guidance points for that pair share that id with increasing **`PointNumber`**.

**[6.1.4]** **Guidance source (Decided):** each **`HaulGuidancePointDto`** **shall** become one **`HaulPaths`** row, including expanded columns (**§5.3.3**), including **`DepthMm`** for that waypoint.

**[6.1.5]** **Cut-start bin (Decided):** for each cut-fill pair, export **shall** record the **2 ft** design bin under the **blade centre** at cut start (informative UTM: **`HaulPassDto.CutStartX`** / **`CutStartY`**) on that pair’s **`HaulRoutes`** row, together with the pair’s **blade depth**. Export **shall not** use a single **`FieldState.HaulPath`** integer to mean “the only path for this bin.”

**[6.1.6]** **`FieldState.HaulPath` (Decided):** for Designer **`.agcontrol`** exports, **`FieldState.HaulPath`** **shall** be **`0`** on all bins. Historical Control “bin → one soil-flow path” lookup is **not** the Designer guidance model. Control **shall** obtain the active path from the **current cut-fill pair** in the ordered plan (**`HaulRoutes`** / **`HaulPaths`**).

**[6.1.7]** Export **shall not** stamp path membership onto every haul-grid cut cell or every design bin along the cut swath.

**[6.1.8]** Route-level fields from **`HaulPassDto`** / **`HaulCutFillPairDto`** (load, distance, timing, sweep endpoints, cut-start bin indices, blade depth) **shall** go on **`HaulRoutes`**.

**[6.1.9]** When the project **has no haul data**, export **shall** still succeed if a **proposed surface** is present (**§7.1**).

**[6.1.10]** When the project **has no haul data**: **`HaulPaths`**, **`HaulRoutes`**, **`PivotMoves`**, and **`HaulArrows`** **shall** be created empty (or with zero rows); every **`FieldState.HaulPath`** **shall** be **`0`**.

### 6.2 Pivot movements → `PivotMoves`

**[6.2.1]** When **haul data** includes pivot overlay data, pivot rows **shall** be written only to **`PivotMoves`** (**§5.5**).

**[6.2.2]** When there is no pivot overlay (including when there is no haul plan), **`PivotMoves`** **shall** be empty.

### 6.3 Haul stats → `Data` + `HaulRoutes`

**[6.3.1]** **`Data.TotalCutCY`** and **`Data.TotalFillCY`** **shall** always be the **design** totals from the proposed surface / **`DesignGrid`** (cubic yards), whether or not a haul plan is present. Haul plan volume aggregates **shall** live on **`HaulRoutes`** (and related haul stats), not replace these **`Data`** keys.

**[6.3.2]** **`Data.CompletedCutCY`** and **`Data.CompletedFillCY`** **shall** be **`0`** at export.

**[6.3.3]** Per-route stats **shall** be written to **`HaulRoutes`** when a haul plan is present; otherwise **`HaulRoutes`** **shall** be empty.

### 6.4 Flow arrows → `HaulArrows`

**[6.4.1]** When a haul **flow field** is present, export **shall** populate **`HaulArrows`** with **`(Latitude, Longitude, Heading)`** from Designer **flow field** samples, heading **degrees clockwise from north** (**`atan2(east, north)`**, normalized to **`[0, 360)`**).

**[6.4.2]** Arrow spacing **shall** honour Designer **`ArrowStride`**. Arrows outside design-bin coverage **should** be omitted.

**[6.4.3]** When no flow field is present (including when there is no haul plan), **`HaulArrows`** **shall** be empty.

### 6.5 Existing topology → `FieldState` + `Data`

**[6.5.1]** Export **shall** use the Designer **design grid** (**`DesignGrid`**, **2 ft** / **`0.6096 m`** bins) as the **`FieldState`** grid.

**[6.5.2]** For each valid bin: **`InitialHeightM`** = existing (m); **`CurrentHeightM`** = existing (m) at export; **`TargetHeightM`** = proposed (m); centroids from the bin; nodata **`-1000`**.

**[6.5.3]** **`Data`** **shall** include at least the Control-familiar keys: **`GridWidth`**, **`GridHeight`**, **`MeanLat`**, **`MeanLon`**, **`MinX`**, **`MinY`**, **`MaxX`**, **`MaxY`**, **`MinLat`**, **`MinLon`**, **`MaxLat`**, **`MaxLon`**, **`TotalCutCY`**, **`TotalFillCY`**, **`CompletedCutCY`**, **`CompletedFillCY`**, **`Calibrated`**, **`HeightOffsetM`**, **`EastingOffsetM`**, **`NorthingOffsetM`**.

**[6.5.4]** At export, calibration offsets **shall** be **`0`** and **`Calibrated`** false unless a later revision defines otherwise.

### 6.6 Proposed topology → `FieldState.TargetHeightM`

**[6.6.1]** Proposed topology **shall** be **`TargetHeightM`** on the same **`FieldState`** rows as existing topology (**§6.5**).

### 6.7 Benchmarks → `Benchmarks`

**[6.7.1]** Survey benchmarks **shall** be inserted as **`(Latitude, Longitude, Name, ElevationM)`**.

---

## 7. Export workflow and UI

### 7.1 Enablement and preconditions **(Decided)**

**[7.1.1]** The **Export to AgGrade Control** / **`.agcontrol`** command **shall** be **enabled** only when the active project **has a proposed surface** (**§4.7**).

**[7.1.2]** When the project **has no proposed surface**, that command **shall** be **disabled**. The user **shall not** be able to produce a **`.agcontrol`** file in that state.

**[7.1.3]** A **haul plan** / **haul data** is **optional**. Absence of haul data **shall not** disable export when a **proposed surface** is present.

**[7.1.4]** Export with a **proposed surface** and **no haul data** **shall** write topology (**`FieldState`**, **`Data`**), **benchmarks**, and empty haul-related tables per **§6.1.10**.

**[7.1.5]** *(Reserved.)* File-name defaults, overwrite behaviour, progress, and error reporting.

**[7.1.6]** *(Reserved.)* Exact control placement in the UI chrome (menu / toolbar / PROJECT section).

---

## 8. Open design decisions

**[8.0.1]** Resolved:

**[8.0.2]** 1. **(Decided)** Container is **SQLite 3** (**§5.1**).
**[8.0.3]** 2. **(Decided)** No requirement that unmodified current Control loaders open **`.agcontrol`** (**§5.1.5**).
**[8.0.4]** 3. **(Decided)** Expand **`HaulPaths`** with HaulBuilder guidance columns; do not use a parallel guidance table (**§5.3**).
**[8.0.5]** 4. **(Decided)** **`PivotMoves`** / **`PivotConfig`** / **`PivotPassBearings`** for pivot data (**§5.5**).
**[8.0.6]** 5. **(Decided)** Per-route stats live in **`HaulRoutes`**, not on every **`HaulPaths`** row (**§5.4**).
**[8.0.7]** 6. **(Decided)** Topology grid is the Designer **2 ft** design grid (**§6.5.1**).
**[8.0.8]** 7. **(Decided)** Core elevations are metres; **`Data`** volume keys named **`*CY`** remain CY (**§5.2.3**).
**[8.0.9]** 8. **(Decided)** Export **requires** a **proposed surface**; **haul plan** is **optional** (**§7.1**).
**[8.0.10]** 9. **(Decided)** Guidance is an **ordered** cut-fill plan (**1…n**); each pair has a **blade depth**; the same cut-start bin **may** be revisited at different depths; Control follows **plan order** (**§4.2**, **§6.1.2**).
**[8.0.11]** 10. **(Decided)** Cut-start bin + depth live on **`HaulRoutes`**; **`FieldState.HaulPath`** is **`0`** for Designer exports (**§6.1.5**–**§6.1.6**).
**[8.0.12]** 11. **(Decided)** **`Data.TotalCutCY`** / **`TotalFillCY`** use **design** totals always (**§6.3.1**).
**[8.0.13]** 12. **(Decided)** **`PRAGMA user_version = 2`** (increment from Control’s current **`1`**) (**§5.1.3**).

**[8.0.14]** Still open:

**[8.0.15]** 1. Accept or revise the **proposed** Appendix A DDL (**§A.1**–**§A.5**).
**[8.0.16]** 2. Provenance **`Data`** key (optional; proposal **`ProducerDesigner = 1`** in **§A.1.4**).
**[8.0.17]** 3. Whether Control keeps supporting old Field Creator DBs via a separate code path (out of scope here, but affects how Control detects **`.agcontrol`**).

---

## 9. Summary

**[9.0.1]** **`.agcontrol`** is a **SQLite 3** Designer→Control package (**`user_version = 2`**): topology in familiar tables; an **ordered guidance plan** of cut-fill pairs (**`HaulRoutes`** + expanded **`HaulPaths`**) with per-pair **blade depth**; pivot tables for schedule/centre/bearings; **`Data`** cut/fill totals from the **design**. Export requires a **proposed surface**; haul is optional. Control executes pairs **1…n** and **may** return to the same cut-start bin at a later depth.

---

## Appendix A. `.agcontrol` schema (proposed)

**[A.0.1]** **`.agcontrol`** databases **shall** set **`PRAGMA user_version = 2`**.

**[A.0.2]** This appendix is a **concrete proposal** for review. Until accepted in **§8**, treat it as the working contract subject to column-level edits.

**[A.0.3]**

```text
PRAGMA user_version = 2;
PRAGMA foreign_keys = ON;
```

### A.1 Topology / overlay / history tables

**[A.1.1]** Familiar Control-shaped tables. Elevations in metres; nodata **`-1000`**.

**[A.1.2]**

```text
CREATE TABLE FieldState (
  BinID INTEGER PRIMARY KEY AUTOINCREMENT,
  X INTEGER NOT NULL,
  Y INTEGER NOT NULL,
  InitialHeightM REAL NOT NULL,
  CurrentHeightM REAL NOT NULL,
  TargetHeightM REAL NOT NULL,
  CentroidLat REAL NOT NULL,
  CentroidLon REAL NOT NULL,
  HaulPath INTEGER NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX idx_fieldstate_xy ON FieldState (X, Y);

CREATE TABLE Data (
  DataID INTEGER PRIMARY KEY AUTOINCREMENT,
  Name TEXT NOT NULL,
  Value REAL NOT NULL
);
CREATE UNIQUE INDEX idx_data_name ON Data (Name);

CREATE TABLE HaulArrows (
  ArrowID INTEGER PRIMARY KEY AUTOINCREMENT,
  Latitude REAL NOT NULL,
  Longitude REAL NOT NULL,
  Heading REAL NOT NULL
);

CREATE TABLE Benchmarks (
  BenchMarkID INTEGER PRIMARY KEY AUTOINCREMENT,
  Latitude REAL NOT NULL,
  Longitude REAL NOT NULL,
  Name TEXT NOT NULL,
  ElevationM REAL NOT NULL
);

CREATE TABLE Events (
  EventID INTEGER PRIMARY KEY AUTOINCREMENT,
  Type TEXT,
  Value REAL,
  Timestamp INTEGER
);

CREATE TABLE LevelingOperation (
  OperationId INTEGER PRIMARY KEY AUTOINCREMENT,
  OperationType TEXT NOT NULL CHECK (OperationType IN ('CUT', 'FILL')),
  StartedAtMs INTEGER NOT NULL,
  CompletedAtMs INTEGER NOT NULL
);

CREATE TABLE LevelingOperationBin (
  OperationBinId INTEGER PRIMARY KEY AUTOINCREMENT,
  OperationId INTEGER NOT NULL,
  X INTEGER NOT NULL,
  Y INTEGER NOT NULL,
  DeltaHeightM REAL NOT NULL,
  FOREIGN KEY (OperationId) REFERENCES LevelingOperation(OperationId) ON DELETE CASCADE
);
CREATE INDEX idx_opbin_operation ON LevelingOperationBin (OperationId);
CREATE INDEX idx_opbin_xy ON LevelingOperationBin (X, Y);
```

**[A.1.3]** Required **`Data.Name`** keys: **`GridWidth`**, **`GridHeight`**, **`MeanLat`**, **`MeanLon`**, **`MinX`**, **`MinY`**, **`MaxX`**, **`MaxY`**, **`MinLat`**, **`MinLon`**, **`MaxLat`**, **`MaxLon`**, **`TotalCutCY`**, **`TotalFillCY`**, **`CompletedCutCY`** (=0), **`CompletedFillCY`** (=0), **`Calibrated`** (=0), **`HeightOffsetM`** (=0), **`EastingOffsetM`** (=0), **`NorthingOffsetM`** (=0).

**[A.1.4]** Optional provenance: **`ProducerDesigner`** = **`1`**.

**[A.1.5]** **`Events`** / leveling tables **shall** be empty at export. **`FieldState.HaulPath`** **shall** be **`0`** on every row (**§6.1.6**).

### A.2 Expanded `HaulPaths` (guidance points)

**[A.2.1]** One row per **`HaulGuidancePointDto`**. **`HaulPath`** = 1-based cut-fill pair; **`PointNumber`** = 1-based order within that pair; **`Seq`** = global 1-based engine sequence.

**[A.2.2]**

```text
CREATE TABLE HaulPaths (
  HaulPathID INTEGER PRIMARY KEY AUTOINCREMENT,
  HaulPath INTEGER NOT NULL,
  PointNumber INTEGER NOT NULL,
  Latitude REAL NOT NULL,
  Longitude REAL NOT NULL,
  Seq INTEGER NOT NULL,
  Action TEXT NOT NULL,
  Blade TEXT NOT NULL,
  EastingM REAL NOT NULL,
  NorthingM REAL NOT NULL,
  DepthMm REAL NOT NULL,
  HeadingDeg REAL NOT NULL
);
CREATE UNIQUE INDEX idx_haulpaths_haul_point ON HaulPaths (HaulPath, PointNumber);
CREATE INDEX idx_haulpaths_seq ON HaulPaths (Seq);
```

**[A.2.3]** Zero rows when no haul plan.

### A.3 `HaulRoutes` (cut-fill pairs)

**[A.3.1]** One row per ordered cut-fill pair. **`BladeDepthMm`** is the pair’s cutting depth. **`CutStartBinX`/`Y`** are the **2 ft** design bins under the blade centre at cut start.

**[A.3.2]**

```text
CREATE TABLE HaulRoutes (
  HaulRouteID INTEGER PRIMARY KEY AUTOINCREMENT,
  HaulPath INTEGER NOT NULL UNIQUE,
  BladeDepthMm REAL NOT NULL,
  CutStartBinX INTEGER NOT NULL,
  CutStartBinY INTEGER NOT NULL,
  CutStartEastingM REAL NOT NULL,
  CutStartNorthingM REAL NOT NULL,
  CutEndEastingM REAL NOT NULL,
  CutEndNorthingM REAL NOT NULL,
  FillStartEastingM REAL NOT NULL,
  FillStartNorthingM REAL NOT NULL,
  FillEndEastingM REAL NOT NULL,
  FillEndNorthingM REAL NOT NULL,
  LoadBcy REAL NOT NULL,
  HaulDistanceM REAL NOT NULL,
  RouteTimeSeconds REAL NOT NULL,
  TotalCutBcyBefore REAL NOT NULL,
  TotalFillBcyBefore REAL NOT NULL
);
CREATE UNIQUE INDEX idx_haulroutes_path ON HaulRoutes (HaulPath);
```

**[A.3.3]** Mapped from **`HaulPassDto`** + **`HaulCutFillPairDto`**. Zero rows when no haul plan.

### A.4 Pivot tables

**[A.4.1]** When pivot data exists: one **`PivotConfig`** row, all **`PivotMoves`**, and **`PivotPassBearings`** per pair. Otherwise all three tables have zero rows.

**[A.4.2]**

```text
CREATE TABLE PivotConfig (
  PivotConfigID INTEGER PRIMARY KEY CHECK (PivotConfigID = 1),
  CenterLatitude REAL NOT NULL,
  CenterLongitude REAL NOT NULL,
  RadiusM REAL NOT NULL
);

CREATE TABLE PivotMoves (
  PivotMoveID INTEGER PRIMARY KEY AUTOINCREMENT,
  MoveIndex INTEGER NOT NULL UNIQUE,
  FirstRoute INTEGER NOT NULL,
  LastRoute INTEGER NOT NULL,
  BearingDeg REAL NOT NULL,
  MoveDeg REAL NOT NULL
);
CREATE UNIQUE INDEX idx_pivotmoves_index ON PivotMoves (MoveIndex);

CREATE TABLE PivotPassBearings (
  HaulPath INTEGER NOT NULL PRIMARY KEY,
  BearingDeg REAL NOT NULL
);
```

**[A.4.3]** Centre from **`HaulSettings.PivotLatitudeDeg`/`PivotLongitudeDeg`**; **`RadiusM`** from **`HaulPivotOverlayDto`**; moves from **`HaulPivotMoveDto`**; pass bearings from **`BearingByPassDeg[i]`** → **`HaulPath = i + 1`**.

### A.5 Schema checklist

**[A.5.1]**

| Table | Present at export | Empty OK without haul? |
|---|---|---|
| **`FieldState`**, **`Data`** | Yes | — |
| **`Benchmarks`**, **`HaulArrows`** | Yes | Yes (zero rows) |
| **`HaulPaths`**, **`HaulRoutes`** | Yes | Yes (zero rows) |
| **`PivotConfig`**, **`PivotMoves`**, **`PivotPassBearings`** | Yes | Yes (zero rows) |
| **`Events`**, leveling tables | Yes | Always empty at export |