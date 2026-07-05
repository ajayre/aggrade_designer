# AgGrade Designer Tools Specification **[0.0.1]**

**[0.0.2]** Every normative paragraph carries a bold marker `[<S>.<U>.<P>]` where `<S>` is the section, `<U>` is the subsection (`0` for body text before the first `###`), and `<P>` is the 1-based paragraph index within that group.

**[0.0.3]** This document specifies the **map-editing tools** in the **AgGrade Designer** left sidebar **TOOLS** section: **Add Point**, **Add Polyline**, **Polygon**, and **Erase**.

**[0.0.4]** - **Project name:** AgGrade Designer — Tools
**[0.0.5]** - **Document type:** Application-layer feature specification
**[0.0.6]** - **Language / implementation:** C# (.NET 8), Avalonia UI, MVVM — source **shall** follow **ANDY-CSHARPCODINGSTYLEGUIDE.md**
**[0.0.7]** - **Target platforms:** Windows, Linux, macOS (same as **AGGRADE-DESIGNER-SPEC-1.0.md**)
**[0.0.8]** - **Scope:** Tool activation behaviour (**§2**), map-editing tools (**§3**) including **Add Point** (**§3.1**), **Add Polyline** (**§3.2**), **Polygon** (**§3.4**), and **Erase** (**§3.3**), and **tool data** lifecycle (**§4**): visibility across **Existing**, **Proposed**, and **Cut/fill map** field content views; **display snapping** (**§4.5**); session persistence; clearing on **New**, **Open**, **import**, and **design** create/replace (**§4.2.3**); **Erase** rectangle deletion; exclusion from project files and export; and read-only **AI tools API** access (**§4.6**, **AI Features §9.10**). Further tool behaviour **shall** be added in later revisions of this document.
**[0.0.9]** - **Relationship to baseline:** **AGGRADE-DESIGNER-SPEC-1.0.md** remains the authoritative contract for the core application (viewer, survey, design, project files, **§7** UI chrome). This document **extends** the product by defining map-editing tools for the left sidebar **TOOLS** section (**AgGrade Designer §7.4.6**). It **supersedes** the empty-slot placeholder obligation in **AgGrade Designer §7.4.7** for the four tools defined in **§3.0.2**; remaining **TOOLS** grid slots **shall** stay as specified in the baseline until a later revision assigns them. This document **shall not** change baseline wire formats (**Appendix A** of the baseline spec) unless a future revision of either document explicitly says otherwise.
**[0.0.10]** - **Relationship to AI features:** **AI-FEATURES-SPEC.md** defines the **AI tools API** (the **surface tool** catalog and provider integration) through which the **LLM** reads project data (**AI Features §8**, **AI Features §9.10**). This document **shall** expose **tool data** to that API for **read-only** access (**§4.6**); the **AI** **shall not** write **tool data**.
**[0.0.11]** - **Assumed knowledge:** Readers **shall** already know **AGGRADE-DESIGNER-SPEC-1.0.md** terminology (**§6**), project model (**§4**), and UI layout (**§7**). In this document, bare **§N** refers to **this document**; **AgGrade Designer §N** or **AGGRADE-DESIGNER-SPEC-1.0.md §N** refers to the baseline spec; **AI Features §N** or **AI-FEATURES-SPEC.md §N** refers to the AI features specification.
**[0.0.12]** - **Related documents:** **ANDY-SPECSTYLEGUIDE.md** (authoring rules), **ANDY-CSHARPCODINGSTYLEGUIDE.md** (C# source style), **AGGRADE-DESIGNER-SPEC-1.0.md** (baseline application specification), **AI-FEATURES-SPEC.md** (AI chat and **AI tools API**).

**[0.0.13]** **Version axes:**

**[0.0.14]** 1. **Specification document** version (this markdown file; semver in **§0.1**).
**[0.0.15]** 2. **Application** version (shipping product semver; distinct from this document).

**[0.0.16]** Incrementing one version axis **does not** imply incrementing another.

### 0.1 Specification revision history

**[0.1.1]** | Version | Date | Change |
|---|---|---|
| **1.0.0** | 2026-07-04 | Initial skeleton: metadata, tool activation model, **Add Point**, **Add Polyline**, and **Erase**. |
| **1.1.0** | 2026-07-04 | **Tool data** (**§4**): functional in all field content views; session persistence; **Erase** clears all **tool data**; not stored in project files or export. |
| **1.2.0** | 2026-07-04 | **Add Point** (**§3.1**): map clicks, **field data** guard, **proposed** elevation, **3D** display with **vertical exaggeration**, unlimited point count. |
| **1.3.0** | 2026-07-04 | **Add Polyline** (**§3.2**): same map interaction as **Add Point**; straight segments between consecutive vertices; contrasting colour; visible in all field content views. |
| **1.4.0** | 2026-07-04 | **Cut/fill map** display (**§4.5**): stored **proposed** elevation unchanged; display adjusted to planar **Cut/fill map** for **tool points** and **tool polyline** vertices and segments. |
| **2.0.0** | 2026-07-04 | **Erase** (**§3.3**): **toggle tool**; drag rectangle to delete **tool data** on release. Replaces instant-action erase-all (**§1.1.0**). |
| **2.1.0** | 2026-07-04 | **Erase** (**§3.3.2**–**§3.3.5**): deletion scope depends on rectangle drag direction. |
| **2.2.0** | 2026-07-04 | **Erase** rectangle appearance (**§3.3.3**): **white** border, **light green** translucent fill. |
| **2.2.1** | 2026-07-04 | **Erase** rule 1 (**§3.3.5**): **tool polylines** must **enter** and lie **completely inside** the rectangle. |
| **2.3.0** | 2026-07-04 | **Tool data** exposed to the **AI** through the **AI tools API** (**§4.6**). |
| **2.4.0** | 2026-07-04 | **Add Polyline** (**§3.2.10**–**§3.2.12**): toggling off completes a polyline; toggling on again starts an additional polyline. |
| **2.5.0** | 2026-07-04 | **Escape** deactivates the active **toggle tool** (**§2.0.6**). |
| **2.6.0** | 2026-07-04 | Automatic tool deactivation (**§2.0.7**–**§2.0.13**): field content view change, **DISPLAY**, **AI**, **Settings**, clicks outside **viewport**. |
| **2.7.0** | 2026-07-04 | **Add Polyline** (**§3.2.4**): click outside **field data** while drawing is ignored. |
| **2.8.0** | 2026-07-04 | Consistency pass: cross-references, **design** prerequisite (**§3.0.3**), empty **tool polyline** discard (**§3.2.14**), **New**/**Open** clears **tool data** (**§4.2.3**), **§4.1** deactivation clarification. |
| **2.9.0** | 2026-07-04 | **Import** clears **tool data** (**§4.2.1**, **§4.2.3**). |
| **3.0.0** | 2026-07-04 | **Erase** polyline split (**§4.4.3**); **2D** display (**§4.2.4**); active **tool button** highlight (**§2.0.14**). |
| **3.1.0** | 2026-07-04 | **Erase** split: single-vertex chain → **tool point** (**§4.4.4**); **§4.4.3** polyline split only; **AI** read-only (**§4.6.3**). |
| **3.2.0** | 2026-07-04 | **Erase** (**§3.3.7**, **§3.3.8**): deletion scope is **tool polyline** segments, not whole **tool polylines**. |
| **3.3.0** | 2026-07-04 | **Erase** drag preview (**§3.3.4**): highlight **tool points** and **tool polyline** segments selected for deletion. |
| **3.4.0** | 2026-07-04 | **Erase** drag cancel on **Escape** (**§3.3.5**): dismiss rectangle; **no** **tool data** erased. |
| **3.5.0** | 2026-07-04 | **Tool data** **display** snapping in **2D** and **Cut/fill map** (**§4.5.2**); stored **actual elevation** unchanged. |
| **3.6.0** | 2026-07-04 | **Tool data** always references **proposed** surface elevations (**§3.1.6**, **§4.0.3**), including when drawn in **Existing** view. |
| **3.7.0** | 2026-07-04 | **Design** create/replace clears **tool data** when a **design** was already present (**§4.2.3**). |
| **3.8.0** | 2026-07-04 | All **tool buttons** **disabled** when the project has **no** **proposed** surface (**§3.0.3**). |
| **3.9.0** | 2026-07-04 | `get_tool_data` JSON return schema (**AI Features §9.10.2**–**§9.10.9**). |
| **3.10.0** | 2026-07-04 | Consistency pass: **AgGrade Designer** cross-references, **§4.2** order, **§3.3.10** erase vertex cleanup, frozen elevations (**§3.1.6**). |
| **4.0.0** | 2026-07-04 | **Polygon** tool (**§3.4**); **tool polygon** **tool data**; **Erase** and **`get_tool_data`** extensions; merged from **TOOLS-SPEC-ADDENDUM1.md** (superseded). |

**[0.1.2]** This document follows [semantic versioning](https://semver.org/): **MAJOR** = incompatible normative change, **MINOR** = backward-compatible addition, **PATCH** = clarification.

---

## 1. Purpose

**[1.0.1]** **AgGrade Designer** **shall** provide **map-editing tools** in the left sidebar **TOOLS** section through which the user selects how pointer input on the field viewer is interpreted.

**[1.0.2]** **Tool data** (**§4**) **shall** help the user and **AI** plan and manipulate the **proposed** surface. The **existing** surface **shall not** be changed through **tool data** or the map-editing tools in **§3** (**§4.0.3**).

**[1.0.3]** Obligations in this document use **shall**, **should**, **may** as defined in **ANDY-SPECSTYLEGUIDE.md §6**.

---

## 2. Tool activation (normative)

**[2.0.1]** Each tool **shall** be exposed as a **tool button** in the left sidebar **TOOLS** section (**AgGrade Designer §7.4.6**).

**[2.0.2]** Tools **shall** use one of two activation kinds:

**[2.0.3]** 1. **Toggle tool** — clicking the tool button **toggles** the tool **on** or **off**. When **on**, the tool **remains active** until the user clicks **another** tool button, clicks the **same** tool button again to toggle it **off**, presses **Escape** (**§2.0.6**), or performs an action listed in **§2.0.8**.
**[2.0.4]** 2. **Instant-action tool** — clicking the tool button **performs the action immediately**. The click **shall not** leave an editing mode active and **shall not** change which toggle tool (if any) is currently active. No tool in **§3.0.2** **shall** use this activation kind.

**[2.0.5]** At most **one** toggle tool **shall** be active at a time. Activating a toggle tool **shall** deactivate any other toggle tool that was active.

**[2.0.6]** When a **toggle tool** is **active**, pressing **Escape** **shall** **deactivate** it, with the same effect as toggling that tool **off**.

**[2.0.7]** **Rules — automatic tool deactivation (normative)**

**[2.0.8]** When a **toggle tool** is **active**, the following actions **shall** **deactivate** it, with the same effect as toggling that tool **off** (**§2.0.3**):

**[2.0.9]** 1. Changing the active **field content view** among **Existing**, **Proposed**, and **Cut/fill map** (**AgGrade Designer §2.4.2**, **AgGrade Designer §7.4.10**–**§7.4.12**).

**[2.0.10]** 2. Clicking a control in the right sidebar **DISPLAY** panel (**AgGrade Designer §7.5.1**, **§7.5.2**).

**[2.0.11]** 3. Clicking the **AI** button (**AI Features §6.1**).

**[2.0.12]** 4. Clicking the **Settings** button (**AI Features §6.1.4**, **AI Features §5**).

**[2.0.13]** 5. Clicking anywhere **outside** the field viewer **viewport** — anywhere in the main window that is **not** the central field viewer map area (**AgGrade Designer §7.2.5**, **AI Features §6.2.2**).

**[2.0.14]** When a **toggle tool** in **§3.0.2** is **active**, its **tool button** **shall** use the same **green accent highlight** as the active **Existing**, **Proposed**, or **Cut / Fill** **VIEW** button (**AgGrade Designer §7.4.10**–**§7.4.12**, **AgGrade Designer §7.2.8**). Inactive **tool buttons** **shall** use the default **TOOLS** grid styling.

---

## 3. Initial tools (normative)

**[3.0.1]** The **TOOLS** section **shall** include the tools listed in **§3.0.2**.

**[3.0.2]** | Tool | Activation kind |
|---|---|
| **Add Point** | Toggle tool |
| **Add Polyline** | Toggle tool |
| **Polygon** | Toggle tool |
| **Erase** | Toggle tool |

**[3.0.3]** All **tool buttons** in **§3.0.2** **shall** require a **design** (**proposed** surface) in the current project (**AgGrade Designer §5**). When the project contains **no** **design**, those **tool buttons** **shall** be **disabled**. The user **shall not** activate **Add Point**, **Add Polyline**, **Polygon**, or **Erase** until a **design** exists.

### 3.1 Add Point

**[3.1.1]** **Add Point** **shall** be a **toggle tool** (**§2.0.3**).

**[3.1.2]** When **Add Point** is active, it **shall** remain active until the user activates **Add Polyline**, activates **Polygon**, activates **Erase**, toggles **Add Point** off, presses **Escape** (**§2.0.6**), performs an action listed in **§2.0.8**, or activates another toggle tool defined in a later revision of this document.

**[3.1.3]** When **Add Point** is active, the user **shall** be able to **click** on the field viewer map to add a **tool point**. Each successful click **shall** append one **tool point** to **tool data** (**§4**).

**[3.1.4]** The user **shall** be able to add **any number** of **tool points** while **Add Point** remains active. The application **shall not** impose a maximum **tool point** count in this revision of the document.

**[3.1.5]** A map click **shall** have **no effect** when the clicked location lies in an area that does **not** have **field data** (**AgGrade Designer §4.1.1**), or when the project contains **no** **design** (**§3.0.3**).

**[3.1.6]** Each **tool point** **shall** store the horizontal map location of the click and an **actual elevation** equal to the **proposed** surface elevation at that location, evaluated on the **continuous** **proposed topology** surface (**AgGrade Designer §2.4.8**, **AgGrade Designer §2.4.13**, **AgGrade Designer §2.4.14**). This **shall** apply regardless of which **field content view** — **Existing**, **Proposed**, or **Cut/fill map** — is active when the user clicks (**§4.0.3**). The stored **actual elevation** **shall not** be the **existing** surface elevation at that location, **shall not** change when the active **field content view** changes, and **shall not** be updated when the committed **proposed** surface is edited later (**AI Features §9.10.3**, `elevations_frozen_at_creation`).

**[3.1.7]** **Tool points** **shall** be **displayed** on the field viewer whenever **tool data** is shown (**§4.2.2**).

**[3.1.8]** In **3D** mode (**AgGrade Designer §2.2**), when **Existing** or **Proposed** is the active field content view, each **tool point** **shall** be rendered at its stored **actual elevation**, with mesh vertical extent **scaled** by the current **vertical exaggeration** setting (**AgGrade Designer §2.3**).

**[3.1.9]** When **Cut/fill map** is the active field content view, **tool point** **display** **shall** follow **§4.5.2**.

**[3.1.10]** In **2D** mode (**AgGrade Designer §2.1**), each **tool point** **shall** be **displayed** per **§4.5.2** and **§4.2.4**.

### 3.2 Add Polyline

**[3.2.1]** **Add Polyline** **shall** be a **toggle tool** (**§2.0.3**).

**[3.2.2]** When **Add Polyline** is active, it **shall** remain active until the user activates **Add Point**, activates **Polygon**, activates **Erase**, toggles **Add Polyline** off, presses **Escape** (**§2.0.6**), performs an action listed in **§2.0.8**, or activates another toggle tool defined in a later revision of this document.

**[3.2.3]** When **Add Polyline** is active, map click behaviour **shall** match **§3.1.3**–**§3.1.6**: each successful click **shall** append one **tool polyline vertex** to the **current** **tool polyline** in **tool data** (**§4**), with the same **field data** guard and **proposed** surface **elevation** rules as **Add Point**.

**[3.2.4]** While the user is creating the **current** **tool polyline**, a map click whose location lies **outside** **field data** (**§3.1.5**, **AgGrade Designer §4.1.1**) **shall** be **ignored**. The click **shall not** append a vertex and **shall not** alter the **current** **tool polyline** in progress.

**[3.2.5]** The user **shall** be able to add **any number** of **tool polyline vertices** to the **current** **tool polyline** while **Add Polyline** remains active. The application **shall not** impose a maximum vertex count per **tool polyline** in this revision of the document.

**[3.2.6]** For every pair of **consecutive** **tool polyline vertices** within the same **tool polyline**, the application **shall** draw one **straight line segment** connecting those vertices at their **display** positions (**§4.5**).

**[3.2.7]** **Tool polyline** vertices **shall** be **displayed** on the field viewer under the same rules as **tool points** (**§3.1.7**–**§3.1.10**).

**[3.2.8]** **Tool polyline** segments **shall** be **displayed** in a **contrasting colour** relative to the mandatory map rendering of the active field content view (**AgGrade Designer §2.4.12**).

**[3.2.9]** **Tool polyline** segments **shall** be **visible** in **Existing**, **Proposed**, and **Cut/fill map** field content views whenever **tool data** is shown (**§4.2.2**).

**[3.2.10]** When **Cut/fill map** is the active field content view, **tool polyline** segment **display** **shall** connect consecutive vertices at their **display** positions (**§4.5.2**).

**[3.2.11]** When the user **deactivates** **Add Polyline** — by toggling **Add Polyline** off, by pressing **Escape** (**§2.0.6**), by performing an action listed in **§2.0.8**, or by activating another toggle tool (**§3.2.2**) — the **current** **tool polyline** **shall** be **completed**. Vertices already added **shall** remain in **tool data** (**§4.2.1**).

**[3.2.12]** When the user **reactivates** **Add Polyline** after completing a **tool polyline**, the application **shall** begin a **new**, additional **tool polyline**. Map clicks **shall** append vertices to that new **tool polyline**, not to any previously completed **tool polyline**.

**[3.2.13]** The user **shall** be able to create **any number** of **tool polylines** in the session by repeating activate → add vertices → deactivate → reactivate. Each completed **tool polyline** **shall** remain in **tool data** until removed by **Erase** (**§4.4**).

**[3.2.14]** When **Add Polyline** is **deactivated** (**§3.2.11**), a **current** **tool polyline** with **zero** vertices **shall not** be stored in **tool data**. A **tool polyline** with one or more vertices **shall** be retained as a completed **tool polyline**.

### 3.3 Erase

**[3.3.1]** **Erase** **shall** be a **toggle tool** (**§2.0.3**).

**[3.3.2]** When **Erase** is active, the user **shall** be able to **drag** a **rectangle** on the field viewer map. When the pointer button is **released**, **tool data** **shall** be **deleted** according to the drag direction (**§3.3.6**, **§3.3.7**, **§3.3.8**).

**[3.3.3]** While the user is dragging an **Erase** rectangle, the application **shall** display the rectangle with a **white** border and a **light green**, **translucent** interior fill.

**[3.3.4]** While the user is dragging an **Erase** rectangle (**§3.3.2**), the application **shall** **highlight** every **tool point**, **tool polyline** segment, and **tool polygon** that **would** be deleted on pointer **release** at the current rectangle position and size, using the selection mode from **§3.3.6** and the same rules as **§3.3.7** and **§3.3.8** for **tool points** and **tool polyline** segments and **§3.3.10** and **§3.3.11** for **tool polygons**. A **tool polygon** marked for deletion **shall** be highlighted **in its entirety** — boundary, **interior** fill, and vertices (**§3.4.9**, **§3.4.10**) — not as individual boundary segments.

**[3.3.5]** When the user presses **Escape** (**§2.0.6**) while dragging an **Erase** rectangle (**§3.3.2**), the application **shall** **cancel** the drag: the **Erase** rectangle (**§3.3.3**) and deletion preview (**§3.3.4**) **shall** be dismissed, and **no** **tool data** **shall** be erased. **Escape** **shall** then **deactivate** **Erase** per **§2.0.6**.

**[3.3.6]** **Rules — erase rectangle drag direction (normative).** **Erase** selection mode **shall** be resolved from the **vertical** component of the rectangle drag in **screen space** (viewport pixels): **drag down** when pointer-**up** **Y** ≥ pointer-**down** **Y**; **drag up** when pointer-**up** **Y** < pointer-**down** **Y**. Horizontal drag direction **shall not** affect the selection mode. **Drag down** **shall** select **§3.3.7**. **Drag up** **shall** select **§3.3.8**.

**[3.3.7]** **Drag down.** When the user **drags down** (**§3.3.6**), the application **shall** delete **all** **tool points** whose horizontal map location lies **inside** the rectangle, and **shall** delete **all** **tool polyline** segments that **enter** the rectangle **and** lie **completely inside** the rectangle.

**[3.3.8]** **Drag up.** When the user **drags up** (**§3.3.6**), the application **shall** delete **all** **tool points** whose horizontal map location lies **inside** the rectangle, and **shall** delete **all** **tool polyline** segments that lie **completely inside** the rectangle.

**[3.3.9]** **Erase** hit testing for **tool points**, **tool polyline** segments, and **tool polygon** boundary segments **shall** use the **screen-space** **Erase** rectangle (**§3.3.3**), projecting map locations to viewport pixels for **inside** and **segment** tests. **Tool polygon** **interiors** **shall not** be used as an independent **Erase** selection criterion; only boundary segments determine whether a **tool polygon** qualifies for **whole** deletion (**§3.3.10**, **§3.3.11**).

**[3.3.10]** **Tool polygon — drag down.** When the user **drags down** (**§3.3.6**), the application **shall** delete a **tool polygon** **wholly** — removing every **tool polygon** vertex, boundary segment, and **interior** from **tool data** — when **every** **tool polygon** boundary segment, including the closing segment from the **last** vertex to the **first**, **would** be deleted under **§3.3.7** as interpreted with **§3.3.6** and **§3.3.9**. If **any** boundary segment **would not** be deleted under that rule, the application **shall not** delete **any** part of that **tool polygon**.

**[3.3.11]** **Tool polygon — drag up.** When the user **drags up** (**§3.3.6**), the application **shall** delete a **tool polygon** **wholly** when **every** **tool polygon** boundary segment, including the closing segment from the **last** vertex to the **first**, **would** be deleted under **§3.3.8** as interpreted with **§3.3.6** and **§3.3.9**. If **any** boundary segment **would not** be deleted under that rule, the application **shall not** delete **any** part of that **tool polygon**.

**[3.3.12]** **Erase** **shall not** split, clip, or partially delete a **tool polygon**. Each **tool polygon** **shall** be removed only as one **single** completed feature (**§3.4.16**) when **§3.3.10** or **§3.3.11** applies. **§3.3.14**, **§4.4.3**, and **§4.4.4** **shall not** apply to **tool polygons**.

**[3.3.13]** When **Erase** is active, it **shall** remain active until the user activates **Add Point**, activates **Add Polyline**, activates **Polygon**, toggles **Erase** off, presses **Escape** (**§2.0.6**, **§3.3.5** when an **Erase** rectangle drag is in progress), performs an action listed in **§2.0.8**, or activates another toggle tool defined in a later revision of this document.

**[3.3.14]** When **§3.3.7** or **§3.3.8** removes **tool polyline** segments, the application **shall** also remove any **tool polyline** vertex whose horizontal map location lies **inside** the erase rectangle and that is not an endpoint of a remaining segment. **§4.4.3** and **§4.4.4** **shall** then apply to the resulting vertex chains. This rule **shall** apply to **tool polylines** only (**§3.3.12**).

### 3.4 Polygon

**[3.4.1]** **Polygon** **shall** be a **toggle tool** (**§2.0.3**). The user **shall not** add **tool polygon** vertices when the project contains **no** **proposed** surface (**design**) (**AgGrade Designer §5**, **§3.0.3**). The **Polygon** **tool button** **shall** be **disabled** until a **design** exists.

**[3.4.2]** When **Polygon** is active, it **shall** remain active until the user activates **Add Point**, activates **Add Polyline**, activates **Erase**, toggles **Polygon** off, presses **Escape** (**§3.4.17**, **§2.0.6**), performs an action listed in **§2.0.8**, or activates another toggle tool defined in a later revision of this document.

**[3.4.3]** When **Polygon** is active, map click behaviour for adding vertices **shall** match **§3.1.3**–**§3.1.6** and **§3.2.3**–**§3.2.5**: each successful click **shall** append one **tool polygon vertex** to the **current** **tool polygon** in **tool data** (**§4**), with the same **field data** guard (**§3.4.4**) and **proposed** surface prerequisite (**§3.4.1**, **§3.0.3**) as **Add Point**, except when **§3.4.12** applies. Each appended **tool polygon vertex** **shall** store an **actual elevation** equal to the **proposed** surface elevation at that vertex horizontal map location (**§3.1.6**, **§4.0.3**), regardless of which **field content view** is active when the user clicks.

**[3.4.4]** While the user is creating the **current** **tool polygon**, a map click **shall** be **ignored** when the project contains **no** **design** (**§3.0.3**, **§3.4.1**) or when the clicked location lies **outside** **field data** (**§3.1.5**, **AgGrade Designer §4.1.1**). The click **shall not** append a vertex and **shall not** alter the **current** **tool polygon** in progress.

**[3.4.5]** The user **shall** be able to add **any number** of **tool polygon vertices** to the **current** **tool polygon** while **Polygon** remains active. The application **shall not** impose a maximum vertex count per **tool polygon** in this revision of this document.

**[3.4.6]** **Close proximity (normative).** For **Polygon**, **near** the **first** **tool polygon vertex** of the **current** **tool polygon** **shall** mean a horizontal map distance of **3 ft** (**0.9144 m**) or less in the field viewer plan view, measured between the pointer location and the **first** vertex location.

**[3.4.7]** While the **current** **tool polygon** has **three** or more **tool polygon vertices**, and the pointer lies **near** (**§3.4.6**) the **first** **tool polygon vertex**, the application **shall** **highlight** that **first** vertex to indicate that the next click **shall** **close** the **tool polygon** per **§3.4.12** rather than append a duplicate vertex.

**[3.4.8]** While the user is creating the **current** **tool polygon**, the application **shall** display the **tool polygon** boundary as **straight line segments** between consecutive **tool polygon vertices** at their **display** positions (**§4.5.2**, **§4.5.3**), plus one **preview segment** from the **last** appended vertex to the current pointer location when a pointer location is available.

**[3.4.9]** While the user is creating the **current** **tool polygon**, and the **current** **tool polygon** has **two** or more **tool polygon vertices**, the application **shall** display the **interior** of the **tool polygon** preview — including the **preview segment** to the pointer when shown (**§3.4.8**) — as a **light**, **translucent** shaded fill bounded by that boundary. That fill **shall** use the same **translucent** colour and **additive** compositing rules as completed **tool polygon** **interiors** (**§3.4.10**).

**[3.4.10]** Completed **tool polygon** **interiors** **shall** be displayed with a **light**, **translucent** shaded fill (**§3.4.9**). **Tool polygon** boundary segments **shall** use the same **contrasting colour** rules as **tool polyline** segments (**§3.2.8**). Each **tool polygon** **interior** fill **shall** be **translucent**. When **two** or more **tool polygon** **interiors** overlap in the field viewer, the application **shall** composite those fills **additively** so that the overlapping region appears **more opaque** than a single **interior** alone, making the overlap **visually obvious** to the user. Boundary strokes **shall not** use **additive** compositing; only **interior** fills **shall**.

**[3.4.11]** **Tool polygon** vertices **shall** be **displayed** on the field viewer under the same rules as **tool points** (**§3.1.7**–**§3.1.10**). **Tool polygon** boundaries and **interiors** **shall** be **visible** in **Existing**, **Proposed**, and **Cut/fill map** field content views and in both **2D** and **3D** viewer modes (**§4.2.2**) whenever **tool data** is shown, using **§4.5.2** and **§4.5.3** for vertex and boundary **display** positions and **§3.4.9**–**§3.4.10** for **interior** fill.

**[3.4.12]** When the user clicks while the pointer is **near** (**§3.4.6**) the **first** **tool polygon vertex** of the **current** **tool polygon**, and the **current** **tool polygon** has **three** or more **tool polygon vertices**, the application **shall** **close** the **tool polygon** (**§3.4.16**). That click **shall not** append a new vertex.

**[3.4.13]** **Concave** **tool polygons** **shall** be **allowed**. **Self-intersection** **shall** mean two non-adjacent **tool polygon** boundary segments that cross at a point other than a shared endpoint. **Concavity** **shall not** be treated as **self-intersection**.

**[3.4.14]** While the user is **appending** **tool polygon vertices** to the **current** **tool polygon** in progress, the application **shall not** reject a map click because the **open** vertex chain — segments between consecutive appended vertices only, **excluding** the closing edge and **excluding** the **preview segment** to the pointer (**§3.4.8**) — would **self-intersect** (**§3.4.13**). An in-progress **tool polygon** **may** therefore show **temporary** **self-intersections** in its **preview** display (**§3.4.8**, **§3.4.9**) until **closed**.

**[3.4.15]** Before **closing** a **tool polygon** (**§3.4.12**, **§3.4.17**), the application **shall** reject closure when the **completed** closed ring — all appended vertices in order plus the closing edge from the **last** vertex to the **first** (**§3.4.16**) — would **self-intersect** (**§3.4.13**). A rejected **close** **shall not** append a vertex, **shall not** complete the **tool polygon**, and **shall** leave the **current** **tool polygon** in progress unchanged so the user **may** continue editing.

**[3.4.16]** A **closed** **tool polygon** **shall** comprise **three** or more **tool polygon vertices** in order, with a closing edge from the **last** vertex back to the **first**. The **first** vertex **shall** appear **once** in stored **tool data**; the closing edge **shall not** duplicate the **first** vertex as an additional stored vertex. A **completed** **tool polygon** **shall not** **self-intersect** (**§3.4.13**).

**[3.4.17]** When the user **deactivates** **Polygon** — by toggling **Polygon** off, by pressing **Escape** (**§2.0.6**), by performing an action listed in **§2.0.8**, or by activating another toggle tool (**§3.4.2**) — the application **shall** **close** the **current** **tool polygon** when it has **three** or more **tool polygon vertices** and **§3.4.15** permits closure. Otherwise the **current** **tool polygon** **shall** be **discarded** (**§3.4.19**).

**[3.4.18]** When the user **reactivates** **Polygon** after completing a **tool polygon**, the application **shall** begin a **new**, additional **tool polygon**. Map clicks **shall** append vertices to that new **tool polygon**, not to any previously completed **tool polygon**.

**[3.4.19]** When **Polygon** is **deactivated** (**§3.4.17**), a **current** **tool polygon** with **fewer than three** **tool polygon vertices** **shall not** be stored in **tool data**. A **closed** **tool polygon** (**§3.4.16**) **shall** be retained as a completed **tool polygon**.

**[3.4.20]** The user **shall** be able to create **any number** of **tool polygons** in the session by repeating activate → add vertices → close → reactivate (**§3.4.18**). Each completed **tool polygon** **shall** remain in **tool data** until removed by **Erase** (**§4.4**). Overlapping **tool polygons** **shall** remain separate features (**§4.0.4**).

**[3.4.21]** **Tool polygons** **shall not** contain **holes**. Each **tool polygon** **shall** be a **single** simple closed ring. The application **shall not** support nested or multiply connected **tool polygon** rings in this revision of this document.

---

## 4. Tool data (normative)

**[4.0.1]** **Tool data** is the in-memory map overlay created by **Add Point** (**tool points**), **Add Polyline** (**tool polylines**, each comprising **tool polyline** vertices and segments), **Polygon** (**tool polygons**, each comprising **tool polygon** vertices, boundary segments, and a simple **interior**), and any additional drawing tools defined in a later revision of this document. **Tool data** **shall** reference the **proposed** surface (**§4.0.3**).

**[4.0.2]** **Tool data** **shall** be distinct from **survey data**, **design** data, and **DISPLAY** decorations (**AgGrade Designer §2.4.12**). It **shall not** modify the **existing topology**, **proposed topology**, or **cut/fill map** scalar fields rendered by the field viewer.

**[4.0.3]** **Tool data** **shall** exist to support **AI**-assisted manipulation of the **proposed** surface (**§4.6**). Each **tool point**, **tool polyline** vertex, and **tool polygon** vertex **shall** store a **proposed** surface **actual elevation** (**§3.1.6**) even when the user creates it while **Existing** is the active **field content view**. Each **tool polygon** vertex **shall** store the horizontal map location of the click and an **actual elevation** equal to the **proposed** surface elevation at that location, evaluated on the **continuous** **proposed topology** surface (**§3.1.6**). Stored **actual elevation** **shall** be the **proposed** surface value at creation, **shall not** be the **existing** surface elevation, and **shall not** be updated when the committed **proposed** surface is edited later (**§4.5.1**, **AI Features §9.10.3**). **Tool data** **shall not** store **existing** surface elevations, **shall not** target edits to the **existing topology**, and **shall not** change the **existing** surface. Map-editing tools in **§3** **shall** affect **tool data** only; they **shall not** write to **existing topology** or other **existing** surface data in the project.

**[4.0.4]** **Tool polygons** **may** overlap one another in plan view (**§3.4.20**). Each **tool polygon** **shall** remain a **separate**, distinct feature in **tool data** with its own stable identifier (**§4.6.2**, **`pg_{n}`**). Overlap **shall not** merge, union, or nest **tool polygons** into a single feature. Overlapping **interiors** **shall** be **displayed** with **additive** **translucent** fill compositing per **§3.4.10**.

### 4.1 Field content views

**[4.1.1]** The map-editing tools (**§3**) **shall** be **usable** in any **field content view** — **Existing**, **Proposed**, or **Cut/fill map** (**AgGrade Designer §2.4.1**, **AgGrade Designer §7.4.10**–**§7.4.12**) — when their preconditions are met (**§3.0.3**, **§3.1.5**). Automatic deactivation (**§2.0.8**) **shall not** contradict this: the user **may** re-activate a tool after a view change.

**[4.1.2]** Switching among **Existing**, **Proposed**, and **Cut/fill map** **shall not** clear, hide, or permanently disable **tool data**. Deactivating the active tool on view change (**§2.0.9**) **shall not** remove **tool data**.

### 4.2 Persistence and display

**[4.2.1]** **Tool data** **shall persist** in the current application session until removed by **Erase** (**§4.4**) or cleared by **New**, **Open**, **import**, or **design** create/replace (**§4.2.3**).

**[4.2.2]** **Tool data** **shall** be **shown** on the field viewer in **all** field content views (**Existing**, **Proposed**, and **Cut/fill map**) and in both **2D** and **3D** viewer modes (**AgGrade Designer §2.0.2**) whenever it is present, until **Erase** removes it (**§4.4**) or **New**, **Open**, **import**, or **design** create/replace clears it (**§4.2.3**).

**[4.2.3]** **New** (**AgGrade Designer §4.1.1**), **Open** (**AgGrade Designer §4.1.2**), and **import** of survey or design data into the current project (**AgGrade Designer §3**, **AgGrade Designer §5.4**, **AgGrade Designer §6.3.2**) **shall** clear **all** **tool data** from the session. When the user creates or replaces the **design** (**proposed** surface) for the current project through the **design dialog** (**AgGrade Designer §5.1**) and the project **already** contains a **design**, the application **shall** clear **all** **tool data** from the session. **Save** and **Save As** **shall not** persist or clear **tool data** (**§4.3.1**).

**[4.2.4]** In **2D** mode, **tool points**, **tool polyline** vertices, **tool polygon** vertices, **tool polyline** segments, and **tool polygon** boundaries and **interiors** **shall** be **displayed** per **§4.5.2** and **§4.5.3** whenever **§4.2.2** requires **tool data** to be shown. **Tool polyline** segments **shall** connect consecutive vertices at those **display** positions (**§3.2.6**, **§3.2.8**). **Tool polygon** boundary segments **shall** connect consecutive vertices — and the closing edge from last to first — at those **display** positions. **Interior** fill **shall** follow **§3.4.9**–**§3.4.10**.

### 4.3 Project files and export

**[4.3.1]** **Tool data** **shall not** be stored in `.agdesign` project files (**AgGrade Designer §4**).

**[4.3.2]** **Tool data** **shall not** be included in **import** or **export** operations (**AgGrade Designer §3.4**, **§5.3**, **§6.3.2**).

### 4.4 Erase

**[4.4.1]** When the user completes an **Erase** rectangle drag (**§3.3.2**), **tool data** **shall** be removed from the current session according to **§3.3.6**, **§3.3.9**, **§3.3.7**, **§3.3.8**, **§3.3.14**, **§3.3.10**, and **§3.3.11**, then **§4.4.3** and **§4.4.4** **shall** apply to any resulting **tool polyline** splits. **Tool polygons** **shall** be affected only per **§3.3.10**–**§3.3.12**.

**[4.4.2]** **Erase** **shall not** remove **survey data**, **design** data, or other project content defined in **AGGRADE-DESIGNER-SPEC-1.0.md**.

**[4.4.3]** When an **Erase** operation splits a **tool polyline** into two or more non-empty vertex chains, the application **shall** replace that **tool polyline** with separate **tool polylines** — one per chain, preserving vertex order within each chain. When removal excises the **middle** of a **tool polyline** and leaves one chain on each side, the result **shall** be exactly **two** separate **tool polylines**. This rule **shall** apply to **tool polylines** only (**§3.3.12**).

**[4.4.4]** When splitting (**§4.4.3**) yields a **tool polyline** with exactly **one** vertex, that vertex **shall** be converted into a **tool point** (**§3.1.6**) as if it had been created with **Add Point**, and the single-vertex **tool polyline** **shall not** remain in **tool data**. This rule **shall** apply to **tool polylines** only (**§3.3.12**).

**[4.4.5]** **Tool polygon** deletion **shall** be **atomic**: the application **shall** remove a **tool polygon** only when **§3.3.10** (**drag down**) or **§3.3.11** (**drag up**) qualifies that **entire** feature for deletion, and **shall** leave the **tool polygon** unchanged otherwise — including when only part of its boundary or **interior** intersects the **Erase** rectangle.

### 4.5 Display snapping (normative)

**[4.5.1]** **Tool points**, **tool polyline** vertices, and **tool polygon** vertices **shall** retain their stored **actual elevation** — the **proposed** surface elevation at their horizontal field location when created (**§3.1.6**) — regardless of viewer mode or **field content view**. Stored **actual elevation** **shall not** be updated when the committed **proposed** surface is edited later. **Display snapping** (**§4.5.2**) **shall** affect **display** only and **shall not** change stored **tool data**.

**[4.5.2]** When the viewer is in **2D** mode (**AgGrade Designer §2.1**) **or** **Cut/fill map** is the active **field content view**, each **tool point**, **tool polyline** vertex, and **tool polygon** vertex **shall** be **displayed** at its horizontal map location **snapped** to the **displayed surface** for the active **field content view** — **Existing**, **Proposed**, or **Cut/fill map** — at that location (**AgGrade Designer §2.4.9** when **Cut/fill map** is active). **Tool polyline** segments and **tool polygon** boundary segments **shall** connect consecutive vertices — and, for **tool polygons**, the closing edge from last to first — at those **display** positions.

**[4.5.3]** In **3D** mode (**AgGrade Designer §2.2**), when **Existing** or **Proposed** is the active **field content view**, **tool point**, **tool polyline** vertex, and **tool polygon** vertex **display** **shall** follow **§3.1.8** and **§3.2.7** at stored **actual elevation** (with **vertical exaggeration**). **Tool polyline** segments and **tool polygon** boundary segments **shall** connect consecutive vertices — and, for **tool polygons**, the closing edge from last to first — at those **display** positions. **Tool polygon** **interior** fills **shall** remain **visible** with **§3.4.10** compositing. **Display snapping** (**§4.5.2**) **shall not** apply in that case.

### 4.6 AI tools API (normative)

**[4.6.1]** **Tool data** **shall** be made available to the **AI** through the **AI tools API** defined in **AI-FEATURES-SPEC.md** (**AI Features §8**, **AI Features §9.10**).

**[4.6.2]** The **AI tools API** **shall** expose the current session **tool data** (**§4.0.1**) via **`get_tool_data`** (**AI Features §9.10.1**) as JSON conforming to **AI Features §9.10.2**–**§9.10.15** so the **LLM** can read **tool points**, **tool polylines**, and **tool polygons** present in the open project session. **`get_tool_data`** **shall** include completed **tool polygons** in the **`polygons`** array; each **tool polygon** entry **shall** follow **AI Features §9.10.10**–**§9.10.11** and **§9.10.13** (**`pg_{n}`** identifiers, **`closed`: `true`**, **`vertex_count`** ≥ **`3`**). In-progress **tool polygons** **shall not** appear in **`get_tool_data`** until **closed** (**§3.4.16**).

**[4.6.3]** The **AI** **shall not** create, modify, or delete **tool data**. **Tool data** **shall** be changed only through the map-editing tools in **§3** or cleared per **§4.2.3**.