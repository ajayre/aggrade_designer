# AgGrade Designer Tools Specification — Addendum 1 **[0.0.1]**

**[0.0.2]** Every normative paragraph carries a bold marker **`[<S>.<U>.<P>]`** where **`<S>`** is the section, **`<U>`** is the subsection (**`0`** for body text before the first **`###`**), and **`<P>`** is the 1-based paragraph index within that group.

**[0.0.3]** This addendum extends **TOOLS-SPEC.md** with the **Polygon** map-editing tool and **tool polygon** **tool data**.

**[0.0.4]** - **Project name:** AgGrade Designer — Tools (Addendum 1)
**[0.0.5]** - **Document type:** Application-layer feature specification addendum
**[0.0.6]** - **Baseline specification:** **TOOLS-SPEC.md** (through **v3.10.0**). The baseline remains normative except where this addendum **explicitly supersedes** a paragraph (**§0.0.7**).
**[0.0.7]** - **Supersedes (normative):** **Tools §3.0.1**, **Tools §3.0.3**, **Tools §3.1.2**, **Tools §3.2.2**, **Tools §3.3.4**, **Tools §3.3.9**, **Tools §4.0.1**, **Tools §4.2.4**, **Tools §4.4.1**, **Tools §4.4.3**, **Tools §4.4.4**, **Tools §4.5.1**, **Tools §4.5.2**, **Tools §4.5.3**. **Extends (normative):** **Tools §3.3.6**, **Tools §3.3.7**, **Tools §3.3.8** (**§3.3.1**–**§3.3.5**). All other **TOOLS-SPEC.md** obligations **shall** apply unchanged unless a later revision of either document says otherwise.
**[0.0.8]** - **Language / implementation:** C# (.NET 8), Avalonia UI, MVVM — source **shall** follow **ANDY-CSHARPCODINGSTYLEGUIDE.md**
**[0.0.9]** - **Target platforms:** Windows, Linux, macOS (same as **AGGRADE-DESIGNER-SPEC-1.0.md**)
**[0.0.10]** - **Assumed knowledge:** **TOOLS-SPEC.md**, **AGGRADE-DESIGNER-SPEC-1.0.md**, and **ANDY-SPECSTYLEGUIDE.md**. In this addendum, bare **§N** or **§N.M** refers to **this document** when the same paragraph marker exists here; otherwise it refers to **TOOLS-SPEC.md**. **Tools §N** refers explicitly to **TOOLS-SPEC.md**; **AgGrade Designer §N** refers to the baseline application specification; **AI Features §N** refers to **AI-FEATURES-SPEC.md**.
**[0.0.11]** - **Related documents:** **TOOLS-SPEC.md**, **AI-FEATURES-SPEC.md**, **ANDY-SPECSTYLEGUIDE.md**, **ANDY-CSHARPCODINGSTYLEGUIDE.md**, **AGGRADE-DESIGNER-SPEC-1.0.md**

**[0.0.12]** **Version axes:**

**[0.0.13]** 1. **Addendum document** version (this markdown file; semver in **§0.1**).
**[0.0.14]** 2. **Application** version (shipping product semver; distinct from this document).

**[0.0.15]** Incrementing one version axis **does not** imply incrementing another.

### 0.1 Specification revision history

**[0.1.1]** | Version | Date | Change |
|---|---|---|
| **1.0.0** | 2026-07-04 | **Polygon** toggle tool (**§3.4**); **tool polygon** **tool data** (**§4.0**); **Erase** and **`get_tool_data`** extensions (**§4.4**, **§4.6**). |
| **1.0.1** | 2026-07-04 | **§3.4.13**–**§3.4.15**: **self-intersection** checked only on **close**; **concave** polygons allowed; in-progress preview **may** cross temporarily. |
| **1.0.2** | 2026-07-04 | **Overlapping** **tool polygons** allowed (**§4.0.4**); **Erase** removes each **tool polygon** **wholly** or not at all (**§3.3.2**–**§3.3.4**, **§4.4**). |
| **1.0.3** | 2026-07-04 | **Erase** drag direction: **drag down** / **drag up** in screen space (**§3.3.1**), matching implementation. |
| **1.0.4** | 2026-07-04 | **Tool polygon** **interior** fill: **translucent** with **additive** compositing on overlap (**§3.4.9**, **§3.4.10**, **§4.0.4**). |
| **1.0.5** | 2026-07-04 | Consistency pass: cross-references, **3D** display (**§4.5.3**), **screen-space** **Erase** hit testing (**§3.3.5**), **drag down**/**drag up** mapping (**§3.3.1**). |
| **1.0.6** | 2026-07-04 | **Polygon** requires **proposed** surface (**§3.4.1**, **§3.4.4**); vertex **actual elevation** from **proposed** surface at click location (**§3.4.3**, **§4.0.2**). |

**[0.1.2]** This document follows [semantic versioning](https://semver.org/): **MAJOR** = incompatible normative change, **MINOR** = backward-compatible addition, **PATCH** = clarification.

---

## 3. Initial tools (normative extensions)

**[3.0.1]** **Supersedes Tools §3.0.1.** The **TOOLS** section **shall** include the tools listed in **§3.0.2**, including **Polygon** defined in **§3.4**.

**[3.0.2]** **Supersedes Tools §3.0.2.** | Tool | Activation kind |
|---|---|
| **Add Point** | Toggle tool |
| **Add Polyline** | Toggle tool |
| **Polygon** | Toggle tool |
| **Erase** | Toggle tool |

**[3.0.3]** **Supersedes Tools §3.0.3.** All **tool buttons** in **§3.0.2** **shall** require a **design** (**proposed** surface) in the current project (**AgGrade Designer §5**). When the project contains **no** **design**, those **tool buttons** **shall** be **disabled**. The user **shall not** activate **Add Point**, **Add Polyline**, **Polygon**, or **Erase** until a **design** exists.

**[3.0.4]** **Supersedes Tools §3.1.2.** When **Add Point** is active, it **shall** remain active until the user activates **Add Polyline**, activates **Polygon**, activates **Erase**, toggles **Add Point** off, presses **Escape** (**Tools §2.0.6**), performs an action listed in **Tools §2.0.8**, or activates another toggle tool defined in a later revision of either document.

**[3.0.5]** **Supersedes Tools §3.2.2.** When **Add Polyline** is active, it **shall** remain active until the user activates **Add Point**, activates **Polygon**, activates **Erase**, toggles **Add Polyline** off, presses **Escape** (**Tools §2.0.6**), performs an action listed in **Tools §2.0.8**, or activates another toggle tool defined in a later revision of either document.

**[3.0.6]** **Supersedes Tools §3.3.9.** When **Erase** is active, it **shall** remain active until the user activates **Add Point**, activates **Add Polyline**, activates **Polygon**, toggles **Erase** off, presses **Escape** (**Tools §2.0.6**, **Tools §3.3.5** when an **Erase** rectangle drag is in progress), performs an action listed in **Tools §2.0.8**, or activates another toggle tool defined in a later revision of either document.

**[3.0.7]** **Supersedes Tools §3.3.4.** While the user is dragging an **Erase** rectangle (**Tools §3.3.2**), the application **shall** **highlight** every **tool point**, **tool polyline** segment, and **tool polygon** that **would** be deleted on pointer **release** at the current rectangle position and size, using the selection mode from **§3.3.1** and the same rules as **Tools §3.3.7** and **Tools §3.3.8** (read as **drag down** and **drag up** per **§3.3.1**) for **tool points** and **tool polyline** segments and **§3.3.2**–**§3.3.3** for **tool polygons**. A **tool polygon** marked for deletion **shall** be highlighted **in its entirety** — boundary, **interior** fill, and vertices (**§3.4.9**, **§3.4.10**) — not as individual boundary segments.

### 3.3 Erase (normative extensions)

**[3.3.1]** **Extends Tools §3.3.6.** **Erase** selection mode **shall** be resolved from the **vertical** component of the rectangle drag in **screen space** (viewport pixels): **drag down** when pointer-**up** **Y** ≥ pointer-**down** **Y**; **drag up** when pointer-**up** **Y** < pointer-**down** **Y**. Horizontal drag direction **shall not** affect the selection mode. **Drag down** **shall** select **Tools §3.3.7** (**enter** the rectangle **and** lie **completely inside**). **Drag up** **shall** select **Tools §3.3.8** (lie **completely inside**). **Tools §3.3.7** and **Tools §3.3.8** **shall** be read as the **drag down** and **drag up** rules respectively, regardless of the baseline compass-corner wording in **TOOLS-SPEC.md**.

**[3.3.2]** **Extends Tools §3.3.7** — **drag down**. When the user **drags down** (**§3.3.1**), the application **shall** delete a **tool polygon** **wholly** — removing every **tool polygon** vertex, boundary segment, and **interior** from **tool data** — when **every** **tool polygon** boundary segment, including the closing segment from the **last** vertex to the **first**, **would** be deleted under **Tools §3.3.7** as interpreted by **§3.3.1** and **§3.3.5**. If **any** boundary segment **would not** be deleted under that rule, the application **shall not** delete **any** part of that **tool polygon**.

**[3.3.3]** **Extends Tools §3.3.8** — **drag up**. When the user **drags up** (**§3.3.1**), the application **shall** delete a **tool polygon** **wholly** when **every** **tool polygon** boundary segment, including the closing segment from the **last** vertex to the **first**, **would** be deleted under **Tools §3.3.8** as interpreted by **§3.3.1** and **§3.3.5**. If **any** boundary segment **would not** be deleted under that rule, the application **shall not** delete **any** part of that **tool polygon**.

**[3.3.4]** **Erase** **shall not** split, clip, or partially delete a **tool polygon**. Each **tool polygon** **shall** be removed only as one **single** completed feature (**§3.4.16**) when **§3.3.2** or **§3.3.3** applies. **Tools §3.3.10**, **§4.4.3**, and **§4.4.4** **shall not** apply to **tool polygons**.

**[3.3.5]** **Erase** hit testing for **tool points**, **tool polyline** segments, and **tool polygon** boundary segments **shall** use the **screen-space** **Erase** rectangle (**Tools §3.3.3**), projecting map locations to viewport pixels for **inside** and **segment** tests. **Tool polygon** **interiors** **shall not** be used as an independent **Erase** selection criterion; only boundary segments determine whether a **tool polygon** qualifies for **whole** deletion (**§3.3.2**, **§3.3.3**).

### 3.4 Polygon

**[3.4.1]** **Polygon** **shall** be a **toggle tool** (**Tools §2.0.3**). The user **shall not** add **tool polygon** vertices when the project contains **no** **proposed** surface (**design**) (**AgGrade Designer §5**, **§3.0.3**). The **Polygon** **tool button** **shall** be **disabled** until a **design** exists.

**[3.4.2]** When **Polygon** is active, it **shall** remain active until the user activates **Add Point**, activates **Add Polyline**, activates **Erase**, toggles **Polygon** off, presses **Escape** (**§3.4.17**, **Tools §2.0.6**), performs an action listed in **Tools §2.0.8**, or activates another toggle tool defined in a later revision of either document.

**[3.4.3]** When **Polygon** is active, map click behaviour for adding vertices **shall** match **Tools §3.1.3**–**Tools §3.1.6** and **Tools §3.2.3**–**Tools §3.2.5**: each successful click **shall** append one **tool polygon vertex** to the **current** **tool polygon** in **tool data** (**§4.0**), with the same **field data** guard (**§3.4.4**) and **proposed** surface prerequisite (**§3.4.1**, **§3.0.3**) as **Add Point**, except when **§3.4.12** applies. Each appended **tool polygon vertex** **shall** store an **actual elevation** equal to the **proposed** surface elevation at that vertex horizontal map location (**Tools §3.1.6**, **§4.0.2**), regardless of which **field content view** is active when the user clicks.

**[3.4.4]** While the user is creating the **current** **tool polygon**, a map click **shall** be **ignored** when the project contains **no** **design** (**§3.0.3**, **§3.4.1**) or when the clicked location lies **outside** **field data** (**Tools §3.1.5**, **AgGrade Designer §4.1.1**). The click **shall not** append a vertex and **shall not** alter the **current** **tool polygon** in progress.

**[3.4.5]** The user **shall** be able to add **any number** of **tool polygon vertices** to the **current** **tool polygon** while **Polygon** remains active. The application **shall not** impose a maximum vertex count per **tool polygon** in this revision of the addendum.

**[3.4.6]** **Close proximity (normative).** For **Polygon**, **near** the **first** **tool polygon vertex** of the **current** **tool polygon** **shall** mean a horizontal map distance of **3 ft** (**0.9144 m**) or less in the field viewer plan view, measured between the pointer location and the **first** vertex location.

**[3.4.7]** While the **current** **tool polygon** has **three** or more **tool polygon vertices**, and the pointer lies **near** (**§3.4.6**) the **first** **tool polygon vertex**, the application **shall** **highlight** that **first** vertex to indicate that the next click **shall** **close** the **tool polygon** per **§3.4.12** rather than append a duplicate vertex.

**[3.4.8]** While the user is creating the **current** **tool polygon**, the application **shall** display the **tool polygon** boundary as **straight line segments** between consecutive **tool polygon vertices** at their **display** positions (**§4.5.2**, **§4.5.3**), plus one **preview segment** from the **last** appended vertex to the current pointer location when a pointer location is available.

**[3.4.9]** While the user is creating the **current** **tool polygon**, and the **current** **tool polygon** has **two** or more **tool polygon vertices**, the application **shall** display the **interior** of the **tool polygon** preview — including the **preview segment** to the pointer when shown (**§3.4.8**) — as a **light**, **translucent** shaded fill bounded by that boundary. That fill **shall** use the same **translucent** colour and **additive** compositing rules as completed **tool polygon** **interiors** (**§3.4.10**).

**[3.4.10]** Completed **tool polygon** **interiors** **shall** be displayed with a **light**, **translucent** shaded fill (**§3.4.9**). **Tool polygon** boundary segments **shall** use the same **contrasting colour** rules as **tool polyline** segments (**Tools §3.2.8**). Each **tool polygon** **interior** fill **shall** be **translucent**. When **two** or more **tool polygon** **interiors** overlap in the field viewer, the application **shall** composite those fills **additively** so that the overlapping region appears **more opaque** than a single **interior** alone, making the overlap **visually obvious** to the user. Boundary strokes **shall not** use **additive** compositing; only **interior** fills **shall**.

**[3.4.11]** **Tool polygon** vertices **shall** be **displayed** on the field viewer under the same rules as **tool points** (**Tools §3.1.7**–**Tools §3.1.10**). **Tool polygon** boundaries and **interiors** **shall** be **visible** in **Existing**, **Proposed**, and **Cut/fill map** field content views and in both **2D** and **3D** viewer modes (**Tools §4.2.2**) whenever **tool data** is shown, using **§4.5.2** and **§4.5.3** for vertex and boundary **display** positions and **§3.4.9**–**§3.4.10** for **interior** fill.

**[3.4.12]** When the user clicks while the pointer is **near** (**§3.4.6**) the **first** **tool polygon vertex** of the **current** **tool polygon**, and the **current** **tool polygon** has **three** or more **tool polygon vertices**, the application **shall** **close** the **tool polygon** (**§3.4.16**). That click **shall not** append a new vertex.

**[3.4.13]** **Concave** **tool polygons** **shall** be **allowed**. **Self-intersection** **shall** mean two non-adjacent **tool polygon** boundary segments that cross at a point other than a shared endpoint. **Concavity** **shall not** be treated as **self-intersection**.

**[3.4.14]** While the user is **appending** **tool polygon vertices** to the **current** **tool polygon** in progress, the application **shall not** reject a map click because the **open** vertex chain — segments between consecutive appended vertices only, **excluding** the closing edge and **excluding** the **preview segment** to the pointer (**§3.4.8**) — would **self-intersect** (**§3.4.13**). An in-progress **tool polygon** **may** therefore show **temporary** **self-intersections** in its **preview** display (**§3.4.8**, **§3.4.9**) until **closed**.

**[3.4.15]** Before **closing** a **tool polygon** (**§3.4.12**, **§3.4.17**), the application **shall** reject closure when the **completed** closed ring — all appended vertices in order plus the closing edge from the **last** vertex to the **first** (**§3.4.16**) — would **self-intersect** (**§3.4.13**). A rejected **close** **shall not** append a vertex, **shall not** complete the **tool polygon**, and **shall** leave the **current** **tool polygon** in progress unchanged so the user **may** continue editing.

**[3.4.16]** A **closed** **tool polygon** **shall** comprise **three** or more **tool polygon vertices** in order, with a closing edge from the **last** vertex back to the **first**. The **first** vertex **shall** appear **once** in stored **tool data**; the closing edge **shall not** duplicate the **first** vertex as an additional stored vertex. A **completed** **tool polygon** **shall not** **self-intersect** (**§3.4.13**).

**[3.4.17]** When the user **deactivates** **Polygon** — by toggling **Polygon** off, by pressing **Escape** (**Tools §2.0.6**), by performing an action listed in **Tools §2.0.8**, or by activating another toggle tool (**§3.4.2**) — the application **shall** **close** the **current** **tool polygon** when it has **three** or more **tool polygon vertices** and **§3.4.15** permits closure. Otherwise the **current** **tool polygon** **shall** be **discarded** (**§3.4.19**).

**[3.4.18]** When the user **reactivates** **Polygon** after completing a **tool polygon**, the application **shall** begin a **new**, additional **tool polygon**. Map clicks **shall** append vertices to that new **tool polygon**, not to any previously completed **tool polygon**.

**[3.4.19]** When **Polygon** is **deactivated** (**§3.4.17**), a **current** **tool polygon** with **fewer than three** **tool polygon vertices** **shall not** be stored in **tool data**. A **closed** **tool polygon** (**§3.4.16**) **shall** be retained as a completed **tool polygon**.

**[3.4.20]** The user **shall** be able to create **any number** of **tool polygons** in the session by repeating activate → add vertices → close → reactivate (**§3.4.18**). Each completed **tool polygon** **shall** remain in **tool data** until removed by **Erase** (**§4.4**). Overlapping **tool polygons** **shall** remain separate features (**§4.0.4**).

**[3.4.21]** **Tool polygons** **shall not** contain **holes**. Each **tool polygon** **shall** be a **single** simple closed ring. The application **shall not** support nested or multiply connected **tool polygon** rings in this revision of the addendum.

---

## 4. Tool data (normative extensions)

**[4.0.1]** **Supersedes Tools §4.0.1.** **Tool data** is the in-memory map overlay created by **Add Point** (**tool points**), **Add Polyline** (**tool polylines**, each comprising **tool polyline** vertices and segments), **Polygon** (**tool polygons**, each comprising **tool polygon** vertices, boundary segments, and a simple **interior**), and any additional drawing tools defined in a later revision of either document. **Tool data** **shall** reference the **proposed** surface (**Tools §4.0.3**).

**[4.0.2]** Each **tool polygon vertex** **shall** store the horizontal map location of the click and an **actual elevation** equal to the **proposed** surface elevation at that location, evaluated on the **continuous** **proposed topology** surface (**Tools §3.1.6**). Stored **actual elevation** **shall** be the **proposed** surface value at creation, **shall not** be the **existing** surface elevation, and **shall not** be updated when the committed **proposed** surface is edited later (**Tools §4.5.1**, **AI Features §9.10.3**). **Tool polygons** **shall not** store **existing** surface elevations.

**[4.0.3]** **Tool polygon** boundaries and **interiors** **shall** be **displayed** per **§4.5.2** and **§4.5.3**. **Tool polygon** boundary segments **shall** connect consecutive vertices — and the closing edge from last to first — at those **display** positions. **Interior** fill **shall** follow **§3.4.9**–**§3.4.10**.

**[4.0.4]** **Tool polygons** **may** overlap one another in plan view (**§3.4.20**). Each **tool polygon** **shall** remain a **separate**, distinct feature in **tool data** with its own stable identifier (**§4.6.1**, **`pg_{n}`**). Overlap **shall not** merge, union, or nest **tool polygons** into a single feature. Overlapping **interiors** **shall** be **displayed** with **additive** **translucent** fill compositing per **§3.4.10**.

### 4.2 Persistence and display (normative extensions)

**[4.2.1]** **Supersedes Tools §4.2.4.** **Tool points**, **tool polyline** vertices, **tool polygon** vertices, **tool polyline** segments, and **tool polygon** boundaries and **interiors** **shall** be **displayed** per **§4.5.2** and **§4.5.3** in all field content views and viewer modes where **Tools §4.2.2** requires **tool data** to be shown.

### 4.4 Erase (normative extensions)

**[4.4.1]** **Supersedes Tools §4.4.1.** When the user completes an **Erase** rectangle drag (**Tools §3.3.2**), **tool data** **shall** be removed from the current session according to **§3.3.1**, **§3.3.5**, **Tools §3.3.7**, **Tools §3.3.8**, **Tools §3.3.10**, **§3.3.2**, and **§3.3.3**, then **§4.4.3** and **§4.4.4** **shall** apply to any resulting **tool polyline** splits. **Tool polygons** **shall** be affected only per **§3.3.2**–**§3.3.4**.

**[4.4.2]** **Tool polygon** deletion **shall** be **atomic**: the application **shall** remove a **tool polygon** only when **§3.3.2** (**drag down**) or **§3.3.3** (**drag up**) qualifies that **entire** feature for deletion, and **shall** leave the **tool polygon** unchanged otherwise — including when only part of its boundary or **interior** intersects the **Erase** rectangle.

**[4.4.3]** **Supersedes Tools §4.4.3.** When an **Erase** operation splits a **tool polyline** into two or more non-empty vertex chains, the application **shall** replace that **tool polyline** with separate **tool polylines** — one per chain, preserving vertex order within each chain. When removal excises the **middle** of a **tool polyline** and leaves one chain on each side, the result **shall** be exactly **two** separate **tool polylines**. This rule **shall** apply to **tool polylines** only (**§3.3.4**).

**[4.4.4]** **Supersedes Tools §4.4.4.** When splitting (**§4.4.3**) yields a **tool polyline** with exactly **one** vertex, that vertex **shall** be converted into a **tool point** (**Tools §3.1.6**) as if it had been created with **Add Point**, and the single-vertex **tool polyline** **shall not** remain in **tool data**. This rule **shall** apply to **tool polylines** only (**§3.3.4**).

### 4.5 Display snapping (normative extensions)

**[4.5.1]** **Supersedes Tools §4.5.1.** **Tool points**, **tool polyline** vertices, and **tool polygon** vertices **shall** retain their stored **actual elevation** — the **proposed** surface elevation at their horizontal field location when created (**Tools §3.1.6**) — regardless of viewer mode or **field content view**. Stored **actual elevation** **shall not** be updated when the committed **proposed** surface is edited later. **Display snapping** (**§4.5.2**) **shall** affect **display** only and **shall not** change stored **tool data**.

**[4.5.2]** **Supersedes Tools §4.5.2.** When the viewer is in **2D** mode (**AgGrade Designer §2.1**) **or** **Cut/fill map** is the active **field content view**, each **tool point**, **tool polyline** vertex, and **tool polygon** vertex **shall** be **displayed** at its horizontal map location **snapped** to the **displayed surface** for the active **field content view** — **Existing**, **Proposed**, or **Cut/fill map** — at that location (**AgGrade Designer §2.4.9** when **Cut/fill map** is active). **Tool polyline** segments and **tool polygon** boundary segments **shall** connect consecutive vertices — and, for **tool polygons**, the closing edge from last to first — at those **display** positions.

**[4.5.3]** **Supersedes Tools §4.5.3.** In **3D** mode (**AgGrade Designer §2.2**), when **Existing** or **Proposed** is the active **field content view**, **tool point**, **tool polyline** vertex, and **tool polygon** vertex **display** **shall** follow **Tools §3.1.8** and **Tools §3.2.7** at stored **actual elevation** (with **vertical exaggeration**). **Tool polyline** segments and **tool polygon** boundary segments **shall** connect consecutive vertices — and, for **tool polygons**, the closing edge from last to first — at those **display** positions. **Tool polygon** **interior** fills **shall** remain **visible** with **§3.4.10** compositing. **Display snapping** (**§4.5.2**) **shall not** apply in that case.

### 4.6 AI tools API (normative extensions)

**[4.6.1]** **`get_tool_data`** (**AI Features §9.10**) **shall** include completed **tool polygons** in the **`polygons`** array. The return payload **shall** conform to **AI Features §9.10.2**–**§9.10.15**; each **tool polygon** entry **shall** follow **§9.10.10**–**§9.10.11** and **§9.10.13** (**`pg_{n}`** identifiers, **`closed`: `true`**, **`vertex_count`** ≥ **`3`**). In-progress **tool polygons** **shall not** appear in **`get_tool_data`** until **closed** (**§3.4.16**).

---
