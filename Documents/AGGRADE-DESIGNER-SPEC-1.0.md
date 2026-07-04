# AgGrade Designer Specification **[0.0.1]**

**[0.0.2]** Every normative paragraph carries a bold marker **`[<S>.<U>.<P>]`** where **`<S>`** is the section, **`<U>`** is the subsection (**`0`** for body text before the first **`###`**), and **`<P>`** is the 1-based paragraph index within that group.

**[0.0.3]** **AgGrade Designer** is a cross-platform desktop application that helps people design field leveling plans for use in-cab with **AgGrade**.

**[0.0.4]** - **Project name:** AgGrade Designer
**[0.0.5]** - **Document type:** Initial application specification
**[0.0.6]** - **Language / implementation:** C# (.NET 8), Avalonia UI, MVVM
**[0.0.7]** - **Target platforms:** Windows, Linux, macOS
**[0.0.8]** - **Scope:** Design field leveling plans exportable for in-cab use with AgGrade. The application **shall** persist work in **project files** (**§4**, **Appendix A**), **shall** include a field **viewer** (**§2**), **shall** support **import** and **export** of field survey data (**§3**, **§9**), **shall** support creation, **import**, and **export** of a field **design** (**§5**, **§9**), **shall** meet the **user interface** appearance requirements of **§7**, and **shall** use the terminology of **§6**.
**[0.0.9]** - **Related documents:** **ANDY-SPECSTYLEGUIDE.md** (authoring rules), **ANDY-CSHARPCODINGSTYLEGUIDE.md** (C# source style). **`.agdesign`** project schema: **Appendix A**. Informative reference imagery: **`Documents/reference/ui-mockup.png`** (main-window layout and look-and-feel, **§7.2.1**), **`Documents/reference/AgGrade-Logo.png`** (**§7.0.7**), **`Documents/reference/2d-survey-view-example.png`** (**§2.4.5**), **`Documents/reference/3d-survey-view-example.png`** (**§2.4.7**). **AgGrade** reference implementation and wire-format sources (**§8**): survey **`AgGrade/Application/Data/Survey.cs`**, design algorithm **`AgGrade/Application/Data/FieldCreator.cs`** (**§5.2.4**–**§5.2.12**), design/AGD **`AgGrade/AI Field Design/fielddesign.py`**, AGD normative **`AgGrade/AI Haul Algorithm III/LAND-LEVELING-SPEC-2.md`** **§6.1**, cut/fill palette **`LAND-LEVELING-SPEC-2.md`** **§14.1** / **`AgGrade/Application/Data/MapGenerator.cs`**, in-cab rotating map **`AgGrade/Application/Controls/Map.cs`** / **`MapGenerator.cs`**, north-up basemap preview **`AgGrade/Application/Controls/MiniMap.cs`** (**§8.2.4**, **§8.3.4**), satellite tiling **`AgGrade/Application/Data/BruTileBasemapLayer.cs`**, **`BasemapDownloader.cs`**, field map tiling **`AgGrade/Application/Data/MAP_TILE_SPEC.md`**.

**[0.0.10]** **Version axes:**

**[0.0.11]** 1. **Specification document** version (this markdown file; semver in **§0.1**).
**[0.0.12]** 2. **Application** version (shipping product semver; distinct from this document).

**[0.0.13]** Incrementing one version axis **does not** imply incrementing another.

**[0.0.14]** - **AgGrade reference tree:** **`C:\Users\andy\OneDrive\Documents\AgGrade\`** (paths in **§8** are relative to this folder unless absolute paths are given).

### 0.1 Specification revision history

**[0.1.1]** | Version | Date | Change |
|---|---|---|
| **1.0.0** | 2026-06-29 | Initial skeleton: metadata and product scope only. |
| **1.1.0** | 2026-06-29 | Field viewer: 2D/3D modes, optional satellite background, 3D orbit, vertical exaggeration setting. |
| **1.2.0** | 2026-06-29 | Survey import (`.ags`, Trimble Multiplane ASCII); survey points in 2D (elevation colour) and 3D (true height). |
| **1.3.0** | 2026-06-29 | Project files (New, Open, Save); self-contained `.agdesign` format. |
| **1.3.1** | 2026-06-29 | `.agdesign` project files specified as XML. |
| **1.4.0** | 2026-06-29 | Design dialog (toolbar); design parameters; overwrite confirmation. |
| **1.5.0** | 2026-06-29 | Terminology (Load/Import/Export); survey export (`.ags`, `.txt`); design export (`.agd`). |
| **1.5.1** | 2026-06-29 | Survey import prompt: add to or replace existing survey data. |
| **1.6.0** | 2026-06-29 | Design import (`.agd`); replace confirmation when design exists. |
| **1.7.0** | 2026-06-29 | Title bar modified indicator for unsaved project changes. |
| **1.8.0** | 2026-06-29 | Design display in 2D plan view and 3D solid mesh with height-band colours. |
| **1.9.0** | 2026-06-29 | Design topology toolbar: existing, proposed, cut/fill map. |
| **1.10.0** | 2026-06-29 | User interface appearance: professional, high-end visual design. |
| **1.11.0** | 2026-06-29 | Benchmark display: filled triangles, names, toolbar toggle. |
| **1.11.1** | 2026-06-29 | Default benchmark display is on. |
| **1.12.0** | 2026-06-29 | Satellite map background in 3D view (and 2D). |
| **1.13.0** | 2026-06-29 | Survey/Design toolbar when design loaded; 3D survey surface rendering (contours, heat map). |
| **1.13.1** | 2026-06-29 | Reference image for 3D Survey view (`Documents/reference/3d-survey-view-example.png`). |
| **1.14.0** | 2026-06-29 | 2D Survey view: heat map, contours, subtle hillshading; reference image. |
| **1.14.1** | 2026-06-29 | Smooth 3D orbit during drag. |
| **1.15.0** | 2026-06-29 | AgGrade reference implementation pointers (formats, rendering, satellite tiles). |
| **2.0.0** | 2026-06-29 | Normative wire formats for `.ags`, Trimble Multiplane ASCII, and `.agd` (**§9**). |
| **2.1.1** | 2026-06-29 | Iconography deferred; implementation **may** use placeholder icons until a final set is specified (**§7.0.6**). |
| **2.1.2** | 2026-06-29 | Informative **AgGrade** brand logo reference (`Documents/reference/AgGrade-Logo.png`, **§7.0.7**). |
| **2.2.0** | 2026-06-29 | Main window layout per UI mockup (**§7.2**–**§7.6**): top toolbar, left/right sidebars, status bar; viewer controls moved out of generic toolbar buttons. |
| **2.2.1** | 2026-06-29 | Status bar **shall** show latitude and longitude of the view centre (**§7.6.2**). |
| **2.3.0** | 2026-06-29 | **PROJECT** sidebar: **Survey** and **Design** only; survey statistics dialog (**§3.5**); **Design** opens design dialog (**§7.4**). |
| **2.3.1** | 2026-06-29 | **TOOLS** sidebar: reserve six empty slots (**§7.4.6**–**§7.4.7**); no map-editing tools in initial phase. |
| **2.3.2** | 2026-06-29 | Cross-reference and **§5.4** paragraph-order fixes; mockup scope limited to layout and look-and-feel (**§7.2.1**); status bar lat/lon only (**§7.6**). |
| **2.4.0** | 2026-06-29 | **VIEW** buttons (no radios); mandatory elevation render vs **DISPLAY** decorations; **2D** pan/zoom; vertical exaggeration **3D** only; **`.agd`** import supplies existing and proposed topology; **Design** disabled without survey. |
| **2.5.0** | 2026-06-29 | Proposed **`.agdesign`** schema (**Appendix A**); design algorithm from **AgGrade** **`FieldCreator.cs`** (**§5.2.4**–**§5.2.12**); default views, **Untitled** project, startup, empty viewer. |
| **2.5.1** | 2026-06-29 | Cross-reference, paragraph-order, and **§8.2.3** decoration alignment fixes. |
| **2.6.0** | 2026-06-29 | Normative nine-band colour scale (**§2.4.16**) for all mandatory field presentations. |
| **2.7.0** | 2026-06-29 | Satellite alignment and **3D** draping (**§2.5.4**–**§2.5.5**); central theme resources (**§7.0.5**); **3D** cut/fill flat plane (**§2.4.9**); top-toolbar **Save As** (**§7.3.2**); empty-project default view and status bar (**§4.1.1**, **§7.6.2**). |
| **2.7.1** | 2026-06-29 | **`.agdesign`** design-parameter volumes in **m³** (**§A.0.2**, **§A.4.2**). |
| **2.7.2** | 2026-06-29 | Empty-project default view centre **36°N**, **90°W** (**§2.1.1**, **§4.0.4**, **§4.1.1**, **§7.6.2**). |
| **2.8.0** | 2026-06-29 | **3D** cut/fill plane height (**§2.4.9**); **3D** orbit pitch and zoom (**§2.2.2**–**§2.2.3**); **`.agd`** import rules and sentinels (**§9.3.7**); no application menus (**§7.0.8**). |
| **2.9.0** | 2026-06-29 | Smooth continuous viewer surfaces (**§2.4.13**); **2 ft** bins for design/export only (**§5.2.11**, **§8.4.2**). |
| **2.10.0** | 2026-06-29 | **2D** north-up map model (**§2.1.7**); **`MiniMap.cs`** reference for basemap pan/zoom (**§8.3.4**); contrast with rotating in-cab **`MapGenerator`** (**§8.2.4**). |

**[0.1.2]** This document follows [semantic versioning](https://semver.org/): **MAJOR** = incompatible normative change, **MINOR** = backward-compatible addition, **PATCH** = clarification.

---

## 1. Purpose

**[1.0.1]** **AgGrade Designer** shall provide a desktop workspace in which the user designs a field leveling plan and prepares it for in-cab use with **AgGrade**. The application **shall** present a **professional**, **high-end** user interface (**§7**).

**[1.0.2]** The primary workspace **shall** include a **field viewer** that displays the current project field in **2D** or **3D** (**§2**).

**[1.0.3]** The user **shall** import existing field **survey data** from supported file formats (**§3**) and inspect that data in the **field viewer** (**§2**).

**[1.0.4]** The user **shall** create, open, and save **projects** as self-contained **`.agdesign`** files that hold all project data (**§4**).

**[1.0.5]** The user **shall** define a field **design** — target grade surface and mass-balance parameters — using the **design dialog** (**§5.1**) or by **import** of an **Ag Design File** (**§5.4**). Once created, the **design** **shall** be shown in the **field viewer** (**§2.4**), with **Existing**, **Proposed**, and **Cut/fill map** selectable from the left sidebar **VIEW** buttons (**§7.4.10**–**§7.4.12**) when both survey data and a design are present.

**[1.0.6]** The user **shall** **export** survey data and field **design** to external file formats, and **shall** **import** external survey and design files, using the top toolbar **Import** and **Export** commands (**§7.3.1**).

---

## 2. Field viewer

**[2.0.1]** The main window **shall** host a **viewer** that renders the current field. The viewer **shall** support two display modes: **2D** (plan view) and **3D** (perspective view).

**[2.0.2]** The user **shall** switch between **2D** and **3D** using the **2D / 3D** toggle in the left sidebar **VIEW** section (**§7.4.9**). Exactly one mode **shall** be active at a time; switching modes **shall** preserve the current project field and design state.

### 2.1 2D view

**[2.1.1]** In **2D** mode, when the project contains field data (**§4.1.1**), the viewer **shall** display the field in **plan view** with **north up** (**§2.1.7**), **zoomed** so the field extent **fills** the viewer area (subject to user pan and zoom thereafter). When the project contains **no** survey data and **no** **design**, the viewer **shall** show **no** field content (**§4.1.1**); the view centre **shall** default to **36.00000°N**, **90.00000°W** (**§7.6.2**).

**[2.1.2]** In **2D** mode, the user **shall** **pan** the map by dragging with the primary pointer button. Pan **shall** translate the visible viewport over the field without changing zoom level. Pan **shall** be **smooth**: the view **shall** update continuously during drag, with no visible stutter under normal use on supported hardware (**§0.0.7**).

**[2.1.3]** In **2D** mode, the user **shall** **zoom** the map using the mouse wheel or an equivalent platform gesture. Zoom **shall** be anchored to the pointer position when a pointer position applies, otherwise to the viewport centre. Zoom **shall** update smoothly during interaction.

**[2.1.4]** In **2D** mode, an optional **satellite map background** **may** be displayed (**§2.5**).

**[2.1.5]** When the project contains **survey data** (**§3**), the **2D** viewer **shall** render **Existing topology** (**§2.4.4**) when **Existing** is the active field content view (**§2.4**) or when **no** **design** is present.

**[2.1.6]** When the project contains a **design** (**§5**), the **2D** viewer **shall** reflect the active **field content view** (**§2.4**).

**[2.1.7]** **North-up (normative).** In **2D** mode, **map north shall always point to the top of the viewer**. The application **shall not** rotate the plan view to field heading, **main slope direction**, tractor heading, or any other azimuth. Pan and zoom (**§2.1.2**, **§2.1.3**) **shall** translate and scale in this fixed north-up frame only. Field topography, contours, and overlays **shall** be drawn in true geographic orientation within that frame (**§2.5.4**). **3D** camera orbit (**§2.2.2**) **shall not** alter this rule when the user switches back to **2D**.

### 2.2 3D view

**[2.2.1]** In **3D** mode, the viewer **shall** display the current project field content in perspective when field data is present. When the project contains **no** **survey data** and **no** **design**, the viewer **shall** show **no** field content (**§4.1.1**). When the project contains **survey data** (**§3**) and **no** **design**, the viewer **shall** render **Existing topology** (**§2.4.6**). When a **design** is present, the active **field content view** (**§2.4**) **shall** govern what is rendered.

**[2.2.2]** In **3D** mode, the user **shall** adjust the camera by **clicking and dragging** with the primary pointer button. Drag **shall** rotate the view **horizontally** (azimuth) around the **centre of the field** and **shall** **pivot** the view **vertically** (pitch) around that same centre. The orbit centre **shall not** drift with each drag gesture unless the project field or its centre changes. When the project contains **no** field data (**§4.1.1**), the orbit centre **shall** be the default view location (**§7.6.2**). Pitch **shall** allow the user to pivot **down** until the camera is at **eye level** with the field surface and **up** to a **plan view** (near top-down over the field).

**[2.2.3]** In **3D** mode, the user **shall** **zoom** using the mouse wheel or an equivalent platform gesture, with the same anchoring and smooth-update behaviour as **2D** zoom (**§2.1.3**).

**[2.2.4]** **3D** camera interaction **shall** be **smooth**: the view **shall** update continuously in response to pointer movement during a drag and during zoom, with no visible stutter, stepping, or judder under normal use on supported hardware (**§0.0.7**).

**[2.2.5]** When the project contains a **design** (**§5**), the **3D** viewer **shall** reflect the active **field content view** (**§2.4**).

**[2.2.6]** When the project contains **survey data** but **no** **design**, only **Existing topology** (**§2.4.3**) **shall** be shown; **Proposed** and **Cut/fill map** have nothing to display. The **Proposed** and **Cut / Fill** **VIEW** buttons (**§7.4.11**, **§7.4.12**) **shall** be **disabled**.

**[2.2.7]** In **3D** mode, an optional **satellite map background** **may** be displayed (**§2.5**).

### 2.3 View settings

**[2.3.1]** In **3D** mode only, the application **shall** provide a **vertical exaggeration** control in the right sidebar (**§7.5.4**) — a scale factor applied to elevation relative to horizontal extent when rendering **Existing** and **Proposed** topology surfaces and **benchmarks**. Vertical exaggeration **shall not** apply to the **Cut/fill map** flat plane (**§2.4.9**) or the **satellite** draping plane (**§2.5.5**).

**[2.3.2]** A vertical exaggeration of **1.0** **shall** mean true scale (no exaggeration). Values greater than **1.0** **shall** increase apparent relief; values between **0.0** and **1.0** **shall** reduce apparent relief. The setting **shall** apply immediately to the active **3D** viewer without reopening the project.

**[2.3.3]** In **2D** mode, the **vertical exaggeration** control **shall** be **hidden** or **disabled** (**§7.5.4**). **2D** plan views **shall not** apply vertical exaggeration.

### 2.4 Field content views (survey and design)

**[2.4.1]** When the project contains **survey data** (**§3**) — **existing topology** — the **field content view** **shall** be **Existing** (**§2.4.3**) unless the user selects another view that is available. When the project contains **no** **design** (**§5**), **only** **Existing** **may** be shown; **Proposed** and **Cut/fill map** have nothing to display and their **VIEW** buttons (**§7.4.11**, **§7.4.12**) **shall** be **disabled**. When the project contains both **survey data** and a **design**, the left sidebar **VIEW** section (**§7.4.8**) **shall** let the user switch among **Existing**, **Proposed** (**§2.4.8**), and **Cut/fill map** (**§2.4.9**). Exactly one view **shall** be active at a time (**§2.4.2**).

**[2.4.2]** The user **shall** select the active field content view using the **Existing**, **Proposed**, and **Cut / Fill** buttons in the left sidebar **VIEW** section (**§7.4.10**–**§7.4.12**). When the project contains **survey data**, exactly one button **shall** be active (highlighted) at a time; selecting a view **shall** deactivate the others. When the project contains **no** **survey data**, **no** field content view button **shall** be active (**§4.1.1**, **§2.4.10**). **Proposed** and **Cut / Fill** **shall** be enabled only when both **survey data** and a **design** are present (**§2.4.1**). The selected view **shall** apply in both **2D** and **3D** modes (**§2.0.2**); switching between **2D** and **3D** **shall** preserve the active field content view. Additional field content views **may** be added in later revisions.

**[2.4.3]** **Survey** **shall** display the **existing** field — the as-surveyed topography derived from **survey data** (**§3**) before leveling to the **design**.

**[2.4.4]** In **2D**, **Survey** **shall** render a **smooth**, **continuous** plan-view surface (**§2.4.13**) derived from the survey data, aligned to the field boundary. The **mandatory** presentation **shall** use the **nine-band colour scale** (**§2.4.16**) mapped to **existing** elevation across the active surface. **Contour lines**, **hillshading**, **satellite map background**, **benchmarks**, and **survey point markers** are **decorations** controlled by **DISPLAY** toggles (**§7.5.2**).

**[2.4.5]** *Informative reference.* **`Documents/reference/2d-survey-view-example.png`** illustrates the intended **2D** **Survey** presentation: elevation heat map, contour lines, **subtle hillshading**, named benchmark triangles, and satellite imagery beneath the surface. This image is **not** normative where it disagrees with **§2.4.4**; it guides visual design and acceptance review.

**[2.4.6]** In **3D**, **Survey** **shall** render a **smooth**, **continuous** surface (**§2.4.13**) derived from the survey data. The **mandatory** presentation **shall** use the **nine-band colour scale** (**§2.4.16**) mapped to **existing** elevation across the active surface. Mesh vertical extent **shall** be scaled by **vertical exaggeration** (**§2.3**). **Contour lines**, **satellite map background**, **benchmarks**, and **survey point markers** are **decorations** controlled by **DISPLAY** toggles (**§7.5.2**).

**[2.4.7]** *Informative reference.* **`Documents/reference/3d-survey-view-example.png`** illustrates the intended **3D** **Survey** presentation: elevation heat map, draped contour lines, named benchmark triangles, and satellite imagery beneath the surface. This image is **not** normative where it disagrees with **§2.4.6**; it guides visual design and acceptance review.

**[2.4.8]** **Design** **shall** display the **proposed** (design target) surface from the current **design** (**§5**). The **mandatory** presentation **shall** use the **nine-band colour scale** (**§2.4.16**) mapped to **proposed** elevation on a **smooth**, **continuous** surface (**§2.4.13**) — in **3D**, as a **continuous mesh** coloured by band; in **2D**, as a smooth plan-view banded surface. Mesh vertical extent **shall** be scaled by **vertical exaggeration** (**§2.3**). **Contour lines**, **satellite map background**, and other overlays are **decorations** (**§7.5.2**).

**[2.4.9]** **Cut/fill map** **shall** display the difference between **Survey** and **Design**. The **mandatory** presentation **shall** use the **nine-band colour scale** (**§2.4.16**) mapped to **cut/fill depth** **`d = existing − proposed`** (metres; positive = **cut**, negative = **fill**) evaluated on **smooth**, **continuous** existing and proposed surfaces (**§2.4.13**). In **2D**, **shall** render as a smooth plan-view banded surface. In **3D**, **shall** render as a **flat horizontal plane** textured with the cut/fill map over the field extent — **not** as a relief mesh and **without** per-point elevation geometry; vertical exaggeration (**§2.3.1**) **shall not** apply. The plane **shall** be placed at the minimum **existing** elevation over the valid field surface. When the **satellite map background** is enabled (**§2.5.5**), the cut/fill plane **shall** sit **on** the satellite draping plane or **immediately above** it. Other overlays are **decorations** (**§7.5.2**).

**[2.4.10]** **Default field content view** — the application **shall** set the active field content view as follows: (1) after **creating** a **design** via the **design dialog** (**§5.1**) — **Proposed** (**§2.4.8**); (2) after **import** of **survey** data (**§3**) — **Existing** (**§2.4.3**); (3) after **import** of an **`.agd`** file (**§5.4.8**) — **Proposed** (**§2.4.8**); (4) after **Open** of a **`.agdesign`** project (**§4.1.2**) — if the project contains a **design**, **Proposed**; else if the project contains **survey data**, **Existing**; else **no** field content view is active and the viewer **shall** show **no** field data (**§4.1.1**). Detailed rendering rules for **contour interval**, **hillshading** parameters, and **labels** **shall** be specified in a later revision of this document.

**[2.4.11]** The terms **Existing topology** and **Proposed topology** are synonymous with **Survey** and **Design** respectively in product documentation and UI copy. The left sidebar **VIEW** section **shall** use the labels **Existing** and **Proposed** (**§7.4.10**, **§7.4.11**).

**[2.4.12]** **Mandatory rendering vs decorations.** Each field content view **shall** always render its **mandatory coloured elevation presentation** on **smooth**, **continuous** surfaces (**§2.4.13**) using the **nine-band colour scale** (**§2.4.16**) — banded elevation for **Existing** and **Proposed**, banded cut/fill depth for **Cut/fill map**. All other visual elements — including **contour lines**, **hillshading**, **satellite map background**, **benchmark** markers and labels, **survey point** markers, and **design** surface overlay — are **decorations**. Decorations **shall** be optional and **shall** be controlled only by **DISPLAY** toggles (**§7.5.2**); they **may** be shown or hidden without changing the active field content view.

**[2.4.13]** **Smooth surface rendering (normative).** The field viewer **shall** present **Existing**, **Proposed**, and **Cut/fill map** topology as **smooth**, **continuous** surfaces matching the informative reference screenshots (**§2.4.5**, **§2.4.7**). Mandatory presentations **shall not** expose **AgGrade**’s **2 ft** design **bins**, **128×128** map **tiles**, or other **pixelized**, **blocky**, or **stairstepped** geometry. **Colour-band** assignment (**§2.4.20**) **shall not** be implemented by tessellating or texturing **2 ft** cells.

**[2.4.14]** **Surface model** — **Existing** and **Proposed** elevations **shall** be evaluated on a **continuous** surface using the same MLS, smoothing, and warp methods as **`FieldCreator.cs`** **`BuildFielddesignSurface`** (**§5.2.8**), sampled at **viewer mesh** resolution sufficient for **smooth** relief and colour transitions at normal viewing distances. **Cut/fill depth** **shall** be **`d = existing − proposed`** on those same continuous surfaces.

**[2.4.15]** **Design bins (AgGrade control grid)** — **2 ft** bins (**§8.4.2**, **§5.2.6**) **shall** be used for **design computation**, **mass-balance** integration, **`.agd`** export, and **`.agdesign`** **`Grid`** persistence only. They **shall not** be the direct render primitive in the viewer.

**[2.4.16]** **Nine-band colour scale (normative).** All mandatory field presentations (**§2.4.4**, **§2.4.6**, **§2.4.8**, **§2.4.9**) **shall** use exactly **nine** discrete colour **bands** defined below. The **middle seven** bands **shall** partition the **middle 90%** of the active value range into **seven equal spans**; the **outer two** bands **shall** cover the **lowest 5%** and **highest 5%** of that range respectively.

**[2.4.17]** **Band colours** — from **high** value to **low** value ( **cut** / high ground toward **fill** / low ground on the **Cut/fill map** ):

**[2.4.18]** | Band | Name | sRGB hex | Meaning |
|---|---|---|---|
| 1 | **Dark red** | **`#B40000`** | High ground; **large cuts** |
| 2 | **Red** | **`#FF0000`** | |
| 3 | **Orange** | **`#FF8000`** | |
| 4 | **Yellow** | **`#FFFF00`** | |
| 5 | **Green** | **`#00CD00`** | **Level ground**; **smallest cut/fill** (on-grade band) |
| 6 | **Cyan** | **`#36F5F5`** | |
| 7 | **Blue** | **`#0066FF`** | |
| 8 | **Violet** | **`#800080`** | |
| 9 | **Indigo** | **`#9A31FF`** | Low ground; **large fills** |

**[2.4.19]** **Active value range** — for each field content view, let **`v_min`** and **`v_max`** be the minimum and maximum scalar over the valid **continuous** surface in the active view: **existing elevation** (**Existing**), **proposed elevation** (**Proposed**), or **`d = existing − proposed`** (**Cut/fill map**). Let **`R = v_max − v_min`**. When **`R ≤ 0`**, the entire surface **shall** use **Green** (band 5).

**[2.4.20]** **Band assignment** — define **`v_lo = v_min + 0.05·R`**, **`v_hi = v_max − 0.05·R`**, and **`w = 0.9·R / 7`**. Assign a scalar value **`v`** to a band as follows:

**[2.4.21]** 1. **`v ≥ v_hi`** → band 1 (**Dark red**).
**[2.4.22]** 2. **`v < v_lo`** → band 9 (**Indigo**).
**[2.4.23]** 3. Otherwise **`v_lo ≤ v < v_hi`**: let **`k = ⌊(v_hi − v) / w⌋`** clamped to **`0…6`**; map **`k`** to bands **`2…8`** (**Red** through **Violet**) so **`k = 0`** → **Red**, **`k = 3`** → **Green**, **`k = 6`** → **Violet**.

**[2.4.24]** **Legend** — the right sidebar elevation legend (**§7.5.5**) **shall** display all **nine** band swatches in high-to-low order with the active view’s **`v_min`**, **`v_max`**, and band boundary values in **metres**.

**[2.4.25]** *Informative reference.* Hex values match **`AgGrade/Application/Data/MapGenerator.cs`** **`CutFillBandColors`** and **LAND-LEVELING-SPEC-2.md** **§14.1.2**. **AgGrade Designer** viewer **band assignment** uses the percentile rule above (**§2.4.20**); static **AgGrade** PNG export uses fixed cut/fill depth thresholds (**§8.2.2**).

### 2.5 Satellite map background

**[2.5.1]** A **satellite map background** **may** be displayed behind field content in **2D** (**§2.1.4**) and **3D** (**§2.2.7**) modes.

**[2.5.2]** Display of the satellite map background **shall** be optional, controlled by the **Satellite** toggle in the right sidebar **DISPLAY** panel (**§7.5.1**). When disabled, or when **no** mandatory field content is rendered (**§4.1.1**), the viewer **shall** show a neutral background in the active mode (field content alone, or background alone when only **decorations** such as satellite are enabled).

**[2.5.3]** The satellite map background enabled/disabled state **shall** be preserved when switching between **2D** and **3D** (**§2.0.2**).

**[2.5.4]** **Alignment** — satellite imagery **shall** be **aligned** to the project **topography** (survey and design surfaces). Tile fetch, projection, and field-overlay compositing **shall** reconcile field **UTM**/heading coordinates with basemap **Web Mercator** tiles so imagery and topo data share the same ground footprint and orientation in **2D** and **3D**.

**[2.5.5]** **3D draping** — when the **satellite map background** is enabled in **3D** mode (**§2.5.1**) and the project contains field data, imagery **shall** be draped on a **horizontal plane** at the minimum **existing** elevation over the valid **continuous** field surface (**§2.4.13**). The plane **shall** span the field extent. Topo surfaces and the **Cut/fill map** plane (**§2.4.9**) **shall** render **on** or **above** the imagery plane. Vertical exaggeration (**§2.3.1**) **shall not** apply to this plane.

**[2.5.6]** *Informative reference.* Satellite source, tile size, and offline cache layout **shall** follow **`AgGrade/Application/Data/BruTileBasemapLayer.cs`** (Esri World Imagery via BruTile, Web Mercator, 256 px tiles, in-memory cache, offline **`{z}/{x}/{y}.tile`** read) and **`AgGrade/Application/Data/BasemapDownloader.cs`** (prefetch zoom levels **14**–**22**, **`manifest.json`**) (**§8.3**).

---

## 3. Field survey import

**[3.0.1]** The user **shall** be able to **import** a field survey file into the **current project** (**§4**). Survey data **shall not** be **loaded** from external files (**§6.2**); it **shall** be brought into the project only by **import** or by **load** of a **`.agdesign`** project that already embeds survey data (**§4.1.2**). On successful import, the application **shall** incorporate the survey **points** into the project (**§3.0.2**–**§3.0.4**) and display them in the **field viewer** (**§2**). The active field content view **shall** switch to **Existing** (**§2.4.10**).

**[3.0.2]** When the project **already contains** survey data and the user **imports** another survey file, the application **shall** prompt the user to **add** the imported points to the existing survey data or **replace** the existing survey data with the imported file.

**[3.0.3]** If the user chooses **add**, the application **shall** retain all existing survey points and **shall** append the newly imported points to the project survey set.

**[3.0.4]** If the user chooses **replace**, the application **shall** **delete** all existing survey points and **shall** use only the newly imported points as the project survey set.

**[3.0.5]** When the project contains **no** survey data, import **shall** proceed without the add-or-replace prompt; the imported file **shall** become the project survey set.

**[3.0.6]** Switching between **2D** and **3D** (**§2.0.2**) **shall** show the same survey points in both modes. Survey data **shall** be stored in the project (**§4.2**); saving the project **shall not** require the original survey file to remain on disk.

### 3.1 Supported formats

**[3.1.1]** The application **shall** accept the following survey file formats:

**[3.1.2]** 1. **Ag Survey File** — file extension **`.ags`**.
**[3.1.3]** 2. **Trimble Multiplane ASCII** — ASCII text; commonly saved with extension **`.txt`**. The importer **shall** recognize this format by content (Trimble Multiplane layout), not by filename extension alone.

**[3.1.4]** Each imported point **shall** provide at minimum a horizontal position and a **measured elevation**. Import and export **shall** conform to **§9.1** (**`.ags`**) or **§9.2** (Trimble Multiplane ASCII).

**[3.1.5]** Survey data **may** include **benchmarks** — survey points identified as benchmarks per **§9.1** and **§9.2**. Benchmark **names** and **display** in the viewer **shall** follow **§3.3**.

### 3.2 Survey point display

**[3.2.1]** After import, **existing topology** **shall** be visible in **2D** mode (**§2.4.4**) and **3D** mode (**§2.4.6**) when **Existing** is the active field content view or when **no** **design** is present.

**[3.2.2]** The **nine-band colour scale** (**§2.4.16**) **shall** use **`v_min`** and **`v_max`** measured over the valid **continuous** **Existing** surface (**§2.4.13**, **§2.4.19**). The mapping **shall** update when survey data or the active view changes.

### 3.3 Benchmark display

**[3.3.1]** When the project contains **benchmarks** (**§3.1.5**) and **benchmark display** is **on** (**§3.3.3**), the viewer **shall** render each benchmark in **2D** and **3D**. Each benchmark **shall** appear as a **filled triangle** at its survey position. In **3D**, vertical position **shall** equal the benchmark’s **measured elevation**, scaled by **vertical exaggeration** (**§2.3**).

**[3.3.2]** Each benchmark **shall** display its **name** as a label adjacent to the triangle in **2D** and **3D** when benchmark display is **on**.

**[3.3.3]** The main window **shall** provide a **Benchmarks** toggle in the right sidebar **DISPLAY** panel (**§7.5.1**) that turns **benchmark display** on and off. The toggle **shall** be enabled when the project contains at least one **benchmark**. The toggle state **shall** be preserved when switching between **2D** and **3D** (**§2.0.2**).

**[3.3.4]** **Benchmark display** **shall** default to **on**. When the project contains **benchmarks**, they **shall** be shown until the user toggles benchmark display **off**.

**[3.3.5]** When benchmark display is **off**, benchmark triangles and name labels **shall not** be drawn. Other survey and design rendering (**§2**, **§2.4**) **shall** be unaffected.

### 3.4 Survey export

**[3.4.1]** At any time, the user **shall** be able to **export** the **current survey data** in the project to an external file using the top toolbar **Export** command (**§7.3.1**).

**[3.4.2]** Survey **export** **shall** support the same formats as survey **import** (**§3.1**):

**[3.4.3]** 1. **Ag Survey File** — file extension **`.ags`**.
**[3.4.4]** 2. **Trimble Multiplane ASCII** — file extension **`.txt`**.

**[3.4.5]** Export **shall** write the survey points currently held in the project. Export **shall not** modify the project. If the project contains no survey data, the export command **shall** be disabled or **shall** prompt the user that there is nothing to export.

**[3.4.6]** Survey **export** **shall** write files that conform to **§9.1** or **§9.2**, using the same row ordering and field semantics as **AgGrade** **`Survey.cs`** save methods (**§8.1.1**).

### 3.5 Survey statistics dialog

**[3.5.1]** Clicking **Survey** in the left sidebar **PROJECT** section (**§7.4.3**) **shall** open a **survey statistics dialog** showing a read-only summary of the **current project survey data** (**§3.0.1**).

**[3.5.2]** If the project contains **no** survey data, **Survey** **shall** be disabled or **shall** open the dialog with a message that no survey is loaded; the dialog **shall not** modify the project.

**[3.5.3]** When survey data is present, the dialog **shall** display summary **statistics** computed from the project survey set, including at minimum:

**[3.5.4]** 1. **Point counts** — total points; counts of **interior**, **boundary**, and **benchmark** points (**§9.1**, **§9.2** classification).
**[3.5.5]** 2. **Elevation** — minimum, maximum, and range of **measured elevation** (metres) across all survey points.
**[3.5.6]** 3. **Master benchmark** — name or **Code** and elevation when a master benchmark is present.

**[3.5.7]** Additional statistics (field area, point spacing, import source format, and so on) **may** be added in a later revision. The dialog **shall** provide a **Close** (or equivalent dismiss) action only; it **shall not** edit survey data.

---

## 4. Project files

**[4.0.1]** The application **shall** provide **New**, **Open**, **Save**, and **Save As** commands for **project files** on the top toolbar (**§7.3.1**). A project is the unit of user work: survey data, design data created in the application, and all other state required to restore the session.

**[4.0.2]** The project file extension **shall** be **`.agdesign`**. Despite the extension, the file **shall** be a well-formed **XML** document encoded as **UTF-8**.

**[4.0.3]** **Open** **shall** reject files that are not valid XML or that do not conform to the **`.agdesign`** schema (**§4.3**, **Appendix A**). **Save** **shall** produce XML that conforms to that schema.

**[4.0.4]** **Application startup** — when the application launches, it **shall** automatically open a **new** blank **Untitled** project (**§4.1.1**, **§4.1.6**) with **no** unsaved modifications (**§4.4**). The viewer **shall** centre on **36.00000°N**, **90.00000°W** (**§7.6.2**).

### 4.1 New, Open, and Save

**[4.1.1]** **New** **shall** start a blank project. Any unsaved work in the current project **shall** be handled according to standard desktop save-or-discard prompting before the blank project replaces it. When the project contains **no** **survey data** and **no** **design**, the **field viewer** **shall** render **no** topography — no mandatory coloured elevation surface (**§2.4.12**). The user **may** still enable the **satellite map background** (**§2.5**), **pan**, and **zoom** in **2D** (**§2.1.2**, **§2.1.3**); those **shall** be the only viewer content until survey or design data is present. The viewer **shall** centre on **36.00000°N**, **90.00000°W** (**§7.6.2**); the status bar **shall** show that view-centre latitude and longitude.

**[4.1.2]** **Open** **shall** **load** a project (**§6.1**) from an existing **`.agdesign`** file on disk and **shall** restore the full project state described in **§4.2**, including embedded survey data without a separate survey **import**. After **Open**, the active field content view **shall** follow **§2.4.10** (case 4).

**[4.1.3]** **Save** **shall** write the **current project** to a **`.agdesign`** file. **Save** **shall** persist **all** data in the project (**§4.2**), not references alone to external files. On an **Untitled** project (**§4.1.6**), **Save** **shall** prompt for a path as **Save As** (**§4.1.7**).

**[4.1.4]** After **Open** or **Save**, the **field viewer** (**§2**) **shall** reflect the restored or saved project without requiring the user to re-import survey data from external survey files.

**[4.1.5]** After a successful **Save**, the project **shall** be considered **clean** (no unsaved modifications) until the user changes project-owned data again (**§4.4**).

**[4.1.6]** **Untitled projects** — a project that has **never** been saved to disk **shall** display the name **`Untitled`** in the top toolbar (**§7.3.4**) and title bar, **without** a **`.agdesign`** extension. **Save** on an **Untitled** project **shall** behave as **Save As** (**§4.1.7**): the application **shall** prompt the user for a file name and location, then write the **`.agdesign`** file. After the first successful save, the project **shall** display the chosen file name and **shall** no longer be **Untitled**.

**[4.1.7]** **Save As** — whether invoked explicitly or as the first **Save** on an **Untitled** project (**§4.1.6**), **shall** prompt the user for a **`.agdesign`** file path. **Save** on a project that already has a saved path **shall** write to that path without prompting unless the user chooses **Save As**.

### 4.2 Project contents

**[4.2.1]** A **`.agdesign`** project file **shall** be **self-contained**: it **shall** embed a **copy** of the **survey data** (**§3**) present in the project, **shall** include the current **design** if one exists (**§5**), and **shall** include **all other data** the user creates inside the application (boundaries, settings, and any future project-owned artifacts specified in later revisions).

**[4.2.2]** Embedded survey data **shall** be sufficient to render **Existing topology** in **2D** and **3D** (**§2.4.4**, **§2.4.6**) after **Open** without access to the original **`.ags`** or Trimble **`.txt`** source file.

**[4.2.3]** The XML element layout and schema for **`.agdesign`** files **shall** conform to **Appendix A** (proposed schema). Future revisions **may** refine element names or add optional sections; **MAJOR** schema version changes **shall** increment **`schemaVersion`** on the root element.

### 4.3 XML project format

**[4.3.1]** A **`.agdesign`** file **shall** be a single **XML** document. All project data described in **§4.2** **shall** be serialized within that document.

**[4.3.2]** The file **shall** use **UTF-8** encoding. The XML declaration **shall** include **`encoding="UTF-8"`** when a declaration is present.

**[4.3.3]** The root element, namespace, **`schemaVersion`** attribute, and child elements for survey data, **design** data (**§5**), viewer UI state, and other project-owned data **shall** be as defined in **Appendix A**. There is **no** existing **`.agdesign`** schema in **AgGrade** (**§8.1.2**).

### 4.4 Modified indicator (title bar)

**[4.4.1]** The application **shall** track whether the **current project** has **unsaved modifications** — changes to project-owned data (**§4.2**) that have not yet been written to disk by **Save** (**§4.1.3**).

**[4.4.2]** The main window **shall** indicate the modified state in two places: (1) the **platform-standard** title bar marker (**§7.0.4**), and (2) the **project name** display in the top toolbar (**§7.3.4**). For a saved project, the display **shall** show the current **`.agdesign`** file name and a conventional dirty marker (for example **`*`**) when the project has unsaved modifications. For an **Untitled** project (**§4.1.6**), the display **shall** show **`Untitled`** and the dirty marker when modified.

**[4.4.3]** The modified indicator **shall** appear when the project becomes dirty and **shall** be cleared when the project is clean: after a successful **Save** (**§4.1.5**), after **Open** of a saved **`.agdesign`** file (**§4.1.2**), after **New** starts a blank project (**§4.1.1**), or at **application startup** (**§4.0.4**).

**[4.4.4]** Changes that update project-owned data — including survey **import** (**§3**), **design** creation or **import** (**§5**), and other project state defined in later revisions — **shall** mark the project modified. **Export** operations that do not alter the project (**§3.4**, **§5.3**) **shall not** mark the project modified.

---

## 5. Field design

**[5.0.1]** A **design** defines the target grade surface and mass-balance parameters for leveling the surveyed field. At most **one** design **shall** exist in a project at a time.

**[5.0.2]** The user **shall** create or replace a **design** using the **design dialog** (**§5.1**) or by **import** of an **`.agd`** file (**§5.4**). The user **shall** open the **design dialog** by clicking **Design** in the left sidebar **PROJECT** section (**§7.4.4**) or using the top toolbar **Design** command (**§7.3.1**).

**[5.0.3]** The **design dialog** and the **Design** commands (**§7.3.1**, **§7.4.4**) **shall** be **disabled** when the project contains **no** **survey data** — there is **no existing topology** to design from. A **design** **may** still enter the project by **import** of an **`.agd`** file (**§5.4**), which supplies both existing and proposed topology.

### 5.1 Design dialog

**[5.1.1]** The **design dialog** **shall** allow the user to enter **design parameters** (**§5.2**) and create a **new design** for the current project. The dialog **shall** be available only when the project contains **survey data** (**§5.0.3**).

**[5.1.2]** The dialog **shall** provide **OK** and **Cancel** actions. **Cancel** **shall** close the dialog without changing the project’s existing design, if any.

**[5.1.3]** When the user activates **OK** and the project **already has** a design, the application **shall** prompt the user to confirm **overwrite** of the current design.

**[5.1.4]** If the user confirms overwrite, the application **shall** **delete** the current design and **shall** replace it with the new design defined by the dialog parameters. If the user declines overwrite, the application **shall** leave the existing design unchanged and **shall** close the dialog without applying the new parameters.

**[5.1.5]** When the user activates **OK** and the project has **no** existing design, the application **shall** create the design from the dialog parameters without an overwrite prompt. The active field content view **shall** switch to **Proposed** (**§2.4.10**).

**[5.1.6]** When the user confirms overwrite (**§5.1.4**), the active field content view **shall** switch to **Proposed** (**§2.4.10**).

**[5.1.7]** The completed design **shall** be stored in the project (**§4.2**) and **shall** be restored on **Open** of a **`.agdesign`** file.

### 5.2 Design parameters

**[5.2.1]** The **design dialog** **shall** expose the following parameters. All **shall** be persisted as part of the project design (**§4.2**).

**[5.2.2]** | Parameter | Unit | Description |
|---|---|---|
| **Main slope direction** | degrees | Compass bearing of the primary fall direction of the design surface. |
| **Main slope** | percent (%) | Grade along the main slope direction. |
| **Cross slope** | percent (%) | Grade perpendicular to the main slope direction. |
| **Cut/fill ratio** | dimensionless | Ratio applied to cut/fill mass balance (compaction). |
| **Import to field** | **CY** | Volume imported onto the field from outside. |
| **Export from field** | **CY** | Volume exported off the field. |

**[5.2.3]** **CY** (**cubic yards**) is the unit for **Import to field** and **Export from field**.

**[5.2.4]** The algorithm that computes the **proposed** design surface from **survey data** and the parameters in **§5.2.2** **shall** match **`AgGrade/Application/Data/FieldCreator.cs`** **`CreateFromSurveyAndDesign`** / **`BuildGrid`** (reference tree **§0.0.14**). In order, the implementation **shall**:

**[5.2.5]** 1. **Load survey topology** — parse interior and boundary points from the project survey set; convert each point to UTM using the zone derived from the field southwest corner; reject fields that cross UTM zone boundaries.

**[5.2.6]** 2. **Build bin grid** — allocate a rectangular grid of **2 ft** (**0.6096 m**) cells (**§8.4.2**, **`Field.BIN_SIZE_M`**) covering the survey extent plus one-cell padding on each side; compute grid width, height, and UTM bounds (**`minX`**, **`minY`**, **`maxX`**, **`maxY`**).

**[5.2.7]** 3. **Valid-field mask** — when **boundary** points form a closed polygon (**≥ 3** vertices), mark bins whose centres fall inside the polygon as valid; otherwise infer the valid envelope from observed interior/boundary bins using morphological close, hole fill, and largest connected component (**`BuildValidMaskFromBoundary`**, **`BuildValidMaskFromObservedPoints`**).

**[5.2.8]** 4. **Existing surface** — for every valid bin centre, interpolate **existing elevation** using weighted moving-least-squares (**MLS**, **`k = 64`** neighbours, spatial hash **`25 m`** cells) over all survey points; smooth with box blur; **warp** residuals at observed bins toward measured elevations using normalized residual blur passes until within **`0.009 m`** tolerance (**`BuildFielddesignSurface`**). Invalid bins **shall** use **`Field.BIN_NO_DATA_SENTINEL`** (**`-1000`**).

**[5.2.9]** 5. **Design plane** — convert **Main slope direction**, **Main slope**, and **Cross slope** to plane partial derivatives **`a`**, **`b`** in UTM easting/northing (**`ComputePlaneSlopes`**: heading **`H`** degrees clockwise from north, main grade **`sM`**, cross grade **`sC`** percent).

**[5.2.10]** 6. **Mass-balance solve** — for plane **`z = a·(E − e₀) + b·(N − n₀) + c`**, find **`c`** by bracketing and bisection so that **`(V_cut + V_import) − r·V_fill − V_export = 0`**, where **`r`** is **Cut/fill ratio** (default **`1.2`** when unset, **`Field.CUT_FILL_RATIO`**), import/export volumes are **Import to field** / **Export from field** converted from **CY** to m³, and **`V_cut`** / **`V_fill`** are integrated over valid bins comparing existing vs proposed elevation (**`SolveC`**, **`VolumesF`**).

**[5.2.11]** 7. **Proposed surface output** — for each valid bin, **proposed elevation** **`z_f = a·(E − e₀) + b·(N − n₀) + c`**; store existing and proposed elevations per bin for **`.agd`** export (**§9.3**) and **`.agdesign`** **`Grid`** persistence (**§A.4.3**). The viewer **shall** render **smooth** continuous surfaces from this data (**§2.4.13**–**§2.4.15**), not by drawing **2 ft** cells. Composite slope heading and percent **shall** be derived from **`a`**, **`b`** (**`CompositeSlopeAndHeading`**).

**[5.2.12]** Numeric constants, MLS regularization, and warp pass counts **shall** match **`FieldCreator.cs`** unless this specification explicitly documents a change. **`AgGrade/AI Field Design/fielddesign.py`** is an informative cross-check only.

### 5.3 Design export

**[5.3.1]** At any time, the user **shall** be able to **export** the **current field design** in the project to an external **Ag Design File** using the top toolbar **Export** command (**§7.3.1**).

**[5.3.2]** The design export file extension **shall** be **`.agd`**.

**[5.3.3]** Export **shall** write the current **design** (**§5.0.1**, **§5.2**), including the computed design surface if one exists. Export **shall not** modify the project. If the project contains no design, the export command **shall** be disabled or **shall** prompt the user that there is nothing to export.

**[5.3.4]** Design **export** and **import** **shall** read and write **`.agd`** files conforming to **§9.3**.

### 5.4 Design import

**[5.4.1]** The user **shall** be able to **import** an external **Ag Design File** into the **current project** using the top toolbar **Import** command (**§7.3.1**). The file extension **shall** be **`.agd`**.

**[5.4.2]** Design data **shall not** be **loaded** from external **`.agd`** files except as **import** into a project (**§6.2.2**). An **`.agd`** file **shall** contain **both** **existing topology** (survey data for the as-surveyed field) **and** **proposed topology** (the design target surface and cut/fill data). On successful import, the application **shall** incorporate **both** into the project (**§5.4.3**–**§5.4.5**, **§5.4.7**) and **shall** store them in the project (**§4.2**).

**[5.4.3]** When the project **already has** a design and the user **imports** an **`.agd`** file, the application **shall** prompt the user to confirm **replace** of the current design. When the project **already contains** survey data, the application **shall** also prompt to confirm **replace** of the existing survey data (existing topology) with the survey data from the file.

**[5.4.4]** If the user confirms replace, the application **shall** **delete** the current design and survey data as applicable and **shall** replace them with the data from the imported **`.agd`** file. If the user declines replace, the application **shall** leave the existing project data unchanged and **shall** not apply the imported file.

**[5.4.5]** When the project contains **no** design, import **shall** proceed without a replace prompt; the imported **`.agd`** file **shall** become the project design.

**[5.4.6]** At most **one** design **shall** exist in the project after import (**§5.0.1**). There **shall** be no **add** option when importing a design (unlike survey import, **§3.0.2**).

**[5.4.7]** Design **import** **shall** parse **`.agd`** files per **§9.3**. Rows representing the **existing** field **shall** populate project **survey data** (existing topology); rows classified as **benchmarks** **shall** be stored in the project survey benchmark set. Rows representing the **proposed** surface and cut/fill **shall** populate the project **design** (proposed topology).

**[5.4.8]** On successful **`.agd`** import, the active field content view **shall** switch to **Proposed** (**§2.4.8**, **§2.4.10**).

---

## 6. Terminology

### 6.1 Load

**[6.1.1]** **Load** means restoring a **project** from a **`.agdesign`** file into the application. The top toolbar **Open** command (**§7.3.1**) performs **load** of a project (**§4.1.2**).

**[6.1.2]** **Load** **shall not** be used in this specification to mean reading external survey or design files into the project.

### 6.2 Import

**[6.2.1]** **Import** means bringing **external data** from a file on disk into the **current project**. Survey data **shall** enter the project only by **import** (**§3**) or as embedded data when a project is **loaded** (**§4.1.2**). A **design** **shall** enter the project by **import** of an **`.agd`** file (**§5.4**), by creation in the **design dialog** (**§5.1**), or as embedded data when a project is **loaded** (**§4.1.2**).

**[6.2.2]** There **shall** be no **Load survey** or **Load design** (or equivalent) command. Survey data **shall not** be **loaded** directly from **`.ags`** or Trimble **`.txt`** files except as **import** into a project. A **design** **shall not** be **loaded** directly from an **`.agd`** file except as **import** into a project.

### 6.3 Export

**[6.3.1]** **Export** means writing data from the **current project** to an external file on disk. Export **shall not** replace or remove data in the project.

**[6.3.2]** Survey **import** (**§3**), survey **export** (**§3.4**), design **import** (**§5.4**), and design **export** (**§5.3**) **shall** be available from the top toolbar **Import** and **Export** commands (**§7.3.1**) while a project is open.

---

## 7. User interface appearance

**[7.0.1]** The application **user interface** **shall** present a **professional**, **high-end** appearance appropriate to a commercial agricultural design product.

**[7.0.2]** The UI **shall not** read as a **generic**, **bland**, or default piece of engineering software — for example, unstyled or minimally styled controls, stock grey chrome with no product identity, or obvious out-of-the-box toolkit defaults with no deliberate visual design.

**[7.0.3]** Visual design **shall** be applied **consistently** across the **main window** chrome (**§7.2**), **top toolbar** (**§7.3**), **sidebars** (**§7.4**, **§7.5**), **status bar** (**§7.6**), **dialogs** (including the **survey statistics dialog**, **§3.5**, and the **design dialog**, **§5.1**), and the **field viewer** (**§2**).

**[7.0.4]** The UI **shall** remain usable and legible on **Windows**, **Linux**, and **macOS** (**§0.0.7**). Platform conventions for title-bar behaviour (**§4.4**) **shall** be respected; bespoke styling **shall** enhance rather than fight those conventions.

**[7.0.5]** **Theme resources** — accent colours, shared brushes, typography tokens, spacing, and other reusable visual tokens **shall** be defined in **one central Avalonia theme resource dictionary** merged at application scope (for example **`Application.Resources`** in **`App.axaml`**, or a dedicated merged file such as **`Themes/AgGradeDesigner.axaml`**). XAML styles, control templates, and code **shall** reference these resources by key. Implementations **shall not** scatter duplicate hex values, brush definitions, or accent-colour literals across multiple files.

**[7.0.6]** Specific typography and spacing details beyond the UI mockup **may** be specified in a companion visual-design document. Until a final icon set is specified, chrome controls **may** use **placeholder** icons matching the mockup layout — simple generic glyphs with text labels beneath each top-toolbar command.

**[7.0.7]** *Informative reference.* **`Documents/reference/AgGrade-Logo.png`** is the **AgGrade** product logo — field-green and harvest-gold brand mark with the **AgGrade** wordmark. It **shall** guide colour palette, tone, and product-family identity for **AgGrade Designer** (**§7.0.1**–**§7.0.2**). The same artwork **may** also appear in the **AgGrade** reference tree (**§0.0.14**) under **`Logo/`**. This image is **informative**, not normative, for layout and chrome details; a **Designer**-specific application icon **may** be specified later.

**[7.0.8]** **No application menus** — in the initial development phase, the main window **shall not** include an application **menu bar** (for example **File**, **Edit**, or **View**). All commands **shall** be reachable from the **top toolbar** (**§7.3**), **sidebars** (**§7.4**, **§7.5**), and **dialogs** only.

### 7.2 Main window layout (normative)

**[7.2.1]** *Informative reference.* **`Documents/reference/ui-mockup.png`** defines the intended **layout** and **look-and-feel** of the main window: overall proportions, dark-theme tone, **AgGrade** field-green accents, sidebar placement, and spacing. The mockup **shall** guide **where** major regions sit and **how** chrome appears visually. It **shall not** define which commands, toggles, or labels are implemented — those **shall** come from **§7.3**–**§7.6** and the functional sections they reference. Decorative or future controls visible in the mockup but not specified elsewhere **shall** follow **§7.2.9**.

**[7.2.2]** The main window **shall** be divided into five regions matching the mockup layout:

**[7.2.3]** 1. **Top toolbar** (**§7.3**) — file and project commands across the top; project file name on the right.
**[7.2.4]** 2. **Left sidebar** (**§7.4**) — **PROJECT** menu, **TOOLS** grid, and **VIEW** controls stacked top to bottom.
**[7.2.5]** 3. **Central viewer** (**§2**) — **2D** / **3D** field map occupying the main area between the sidebars.
**[7.2.6]** 4. **Right sidebar** (**§7.5**) — **DISPLAY** layer toggles, **vertical exaggeration**, and elevation legend.
**[7.2.7]** 5. **Status bar** (**§7.6**) — footer strip beneath the viewer.

**[7.2.8]** The chrome **shall** use a **dark** workspace theme with **AgGrade** field-green accent for active toggles, selected **VIEW** modes, and primary actions, consistent with **`Documents/reference/ui-mockup.png`** and **`Documents/reference/AgGrade-Logo.png`** (**§7.0.7**). Accent colours **shall** be taken from the central theme resources (**§7.0.5**), not hard-coded per control.

**[7.2.9]** **Future controls:** UI elements shown in the mockup but **not** assigned behaviour in this specification **shall** appear in their mockup locations as **empty placeholders** — visible layout slots with no active command — until a later revision defines them. Placeholders **shall not** simulate or stub unrelated behaviour.

### 7.3 Top toolbar (normative)

**[7.3.1]** The top toolbar **shall** provide the following commands, each with an icon above a text label, left-aligned in this order:

**[7.3.2]** | Command | Action |
|---|---|
| **New** | Start a blank project (**§4.1.1**). |
| **Open** | **Load** a **`.agdesign`** project (**§4.1.2**, **§6.1.1**). |
| **Save** | Save the current project (**§4.1.3**). |
| **Save As** | Save the current project to a new **`.agdesign`** path (**§4.1.7**). |
| **Import** | Import external survey (**§3**) or design (**§5.4**) data into the project (**§6.3.2**). |
| **Export** | Export survey (**§3.4**) or design (**§5.3**) data from the project (**§6.3.2**). |
| **Design** | Open the **design dialog** (**§5.0.2**, **§5.1**, **§7.4.4**). **Disabled** when the project contains **no** **survey data** (**§5.0.3**). |

**[7.3.3]** **Import** and **Export** **shall** present the user with a choice of survey or design operation (sub-menu, split button, or dialog) matching **§3** and **§5.3**–**§5.4**.

**[7.3.4]** The top toolbar **shall** display the **current project name** on the right: **`Untitled`** for an unsaved project (**§4.1.6**), or the saved **`.agdesign`** file name including extension, plus the dirty-state marker defined in **§4.4.2** (for example **`Home Quarter.agdesign*`** or **`Untitled*`**).

**[7.3.5]** The top toolbar **shall not** host **2D** / **3D**, field content view, layer toggles, or **vertical exaggeration** controls; those **shall** live in the sidebars (**§7.4**, **§7.5**).

### 7.4 Left sidebar (normative)

**[7.4.1]** The **left sidebar** **shall** match the mockup **layout**: a fixed-width column with three labelled sections — **PROJECT**, **TOOLS**, and **VIEW** — stacked in that order (**§7.2.4**). Section headings, spacing, and control styling **shall** follow the mockup look-and-feel. Which commands appear in each section **shall** be as specified below, not as every label shown in the mockup artwork.

**[7.4.2]** **PROJECT** section — the **PROJECT** section **shall** contain **only** the following two commands, in this order. **Benchmark** visibility and map-layer visibility **shall** be controlled from the right sidebar **DISPLAY** toggles (**§7.5.1**), not from **PROJECT**.

**[7.4.3]** 1. **Survey** — opens the **survey statistics dialog** (**§3.5**).
**[7.4.4]** 2. **Design** — opens the **design dialog** (**§5.1**). **Shall** be **disabled** when the project contains **no** **survey data** (**§5.0.3**).
**[7.4.5]** No other **PROJECT** commands **shall** be present in the initial development phase.

**[7.4.6]** **TOOLS** section — the **TOOLS** section **shall** reserve layout space for **six** tool slots in a **2×3** grid, matching the grid footprint and spacing in the mockup (**§7.2.1**).

**[7.4.7]** No map-editing tools are implemented in the initial development phase. All six slots **shall** remain **empty** — visible reserved space with **no** icons, labels, commands, or active behaviour (**§7.2.9**). Individual tools **shall** be specified in a later revision before any slot becomes interactive.

**[7.4.8]** **VIEW** section — the user **shall** choose exactly one **field content view** at a time (**§2.4.2**). Additional views **may** be added in later revisions (**§7.2.9**).

**[7.4.9]** 1. **2D / 3D** toggle — switches viewer mode (**§2.0.2**). Exactly one mode **shall** be active; the active side **shall** use the green accent style from the mockup.

**[7.4.10]** 2. **Existing** — button that selects **Existing topology** (**§2.4.3**). **Shall** be enabled when the project contains **survey data**; **shall** be **disabled** when **no** survey data is present. When active, **shall** use the green accent highlight from the mockup. When **no** **design** is present but survey data exists, **Existing** **shall** be the only enabled view button (**§2.4.1**).

**[7.4.11]** 3. **Proposed** — button that selects **Proposed topology** (**§2.4.8**). **Shall** be enabled only when both **survey data** and a **design** are present (**§2.4.1**). When active, **shall** use the green accent highlight.

**[7.4.12]** 4. **Cut / Fill** — button that selects the **Cut/fill map** field content view (**§2.4.9**). **Shall** be enabled only when both **survey data** and a **design** are present (**§2.4.1**). When active, **shall** use the green accent highlight.

**[7.4.13]** The mockup **may** show a **Topography** subheading above **Existing** and **Proposed**; that grouping is **layout only**. There **shall** be **no** radio-button control — view selection **shall** use mutually exclusive highlighted buttons only (**§2.4.2**).

### 7.5 Right sidebar (normative)

**[7.5.1]** The **right sidebar** **shall** match the mockup **layout**: a fixed-width column with the **DISPLAY** toggle panel at the top, **vertical exaggeration** below it, and the **elevation legend** below that (**§7.2.6**). Toggle switches **shall** use the mockup’s green on/off styling (**§7.2.8**). **DISPLAY** toggles control **decorations** layered on the mandatory coloured elevation presentation of the active view (**§2.4.12**). The **DISPLAY** panel **shall** provide toggle switches for decoration visibility, in this order:

**[7.5.2]** | Toggle | Behaviour |
|---|---|
| **Survey Points** | **Decoration:** show or hide individual survey point markers. Default **on** when survey data is present. |
| **Contours** | **Decoration:** show or hide **contour lines** on the active surface. Default **on**. |
| **Hillshade** | **Decoration:** show or hide **hillshading** in **2D** plan view. Default **on**. |
| **Benchmarks** | **Decoration:** **benchmark display** toggle (**§3.3.3**–**§3.3.5**). Default **on** (**§3.3.4**). |
| **Design** | **Decoration:** show or hide the **proposed** design surface as an overlay when not in the **Proposed** field content view. Default **on** when a design is present. |
| **Satellite** | **Decoration:** **satellite map background** toggle (**§2.5.2**). |

**[7.5.3]** Each **DISPLAY** toggle **shall** use the green on/off switch style from the mockup. Disabled toggles **shall** apply when their target data is absent (for example **Benchmarks** when there are no benchmarks).

**[7.5.4]** **Vertical exaggeration** — in **3D** mode only, the right sidebar **shall** provide a labelled slider control (**§2.3.1**–**§2.3.2**) showing the current multiplier (for example **`2.0×`** in the mockup). In **2D** mode the control **shall** be **hidden** or **disabled** (**§2.3.3**).

**[7.5.5]** **Elevation legend** — when mandatory field content is rendered (**§2.4.12**), the right sidebar **shall** show the **nine-band** colour legend (**§2.4.16**, **§2.4.24**) with band names and boundary values in **metres** for the active view’s scalar (**existing** elevation, **proposed** elevation, or **cut/fill depth**). The legend **shall** update when the active field content view or survey/design data changes. When **no** field data is present (**§4.1.1**), the legend **shall** be hidden or empty.

### 7.6 Status bar (normative)

**[7.6.1]** The main window **shall** include a bottom **status bar** matching the mockup footer layout (**§7.2.7**).

**[7.6.2]** **View-centre coordinates** — the status bar **shall** **always** display the **latitude** and **longitude** of the **centre of the current viewer** (the point at the middle of the visible map viewport), in **WGS-84 decimal degrees** — including when the project contains **no** survey data and **no** **design** (**§4.1.1**). The readout **shall** update continuously when the user **pans**, **zooms**, or **orbits** the view. When the project has **no** field data, the default view centre **shall** be **36.00000°N**, **90.00000°W** until the user moves the view. Labels **shall** use **`Lat:`** and **`Lon:`** (or equivalent unambiguous text). No other coordinate readout (for example UTM easting/northing or elevation) **shall** be shown in the initial development phase.

**[7.6.3]** **Formatting:** latitude and longitude **shall** be formatted with sufficient precision for field work (at least **five** digits after the decimal point) using **`InvariantCulture`**. Southern latitudes and western longitudes **shall** use negative values or an explicit hemisphere indicator — one convention **shall** be chosen and applied consistently.

**[7.6.4]** **Additional footer regions:** the status bar **may** reserve empty layout space for future readouts (for example zoom level) consistent with the mockup footprint. Those regions **shall** remain blank until specified in a later revision (**§7.2.9**).

---

## 8. Reference implementation and compatibility (AgGrade)

**[8.0.1]** **AgGrade Designer** **shall** read and write the same external survey and design file formats as the in-cab **AgGrade** application and its tooling, unless this specification **explicitly** documents an incompatible change. Paths below are relative to the **AgGrade reference tree** (**§0.0.14**).

**[8.0.2]** **AgGrade Designer** **shall** implement **3D** viewing, **DISPLAY**-controlled **contour** and **hillshade** **decorations** (**§2.4.12**, **§7.5.2**), and **`.agdesign`** project persistence described in **§2**–**§4**; those capabilities are **not** present in the existing **AgGrade** WinForms application and **shall not** be inferred from **`Map.cs`** alone.

### 8.1 Survey and design file formats

**[8.1.1]** Normative on-disk layouts for **`.ags`**, Trimble Multiplane ASCII, and **`.agd`** **shall** be **§9**. Reference parsers/writers: **`Application/Data/Survey.cs`**, **`Application/Data/AGDLoader.cs`**, **`AI Field Design/fielddesign.py`**.

**[8.1.2]** **`.agdesign`** — **no** implementation exists in **AgGrade**; schema is defined by **Appendix A** of this document (**§4.2.3**, **§4.3.3**).

### 8.2 Map rendering reference

**[8.2.1]** In-cab **AgGrade** uses **`Application/Data/MapGenerator.cs`** (**128×128** px tiles, nine-band cut/fill palette) and **`MAP_TILE_SPEC.md`** for **cab display** and cut/fill **control** — **not** for **AgGrade Designer** viewer rendering. The **design surface algorithm** **shall** match **`Application/Data/FieldCreator.cs`** (**§5.2.4**–**§5.2.12**), including **2 ft** bins for computation and export (**§2.4.15**).

**[8.2.2]** **AgGrade Designer** **viewer** presentations **shall** use **smooth**, **continuous** surfaces (**§2.4.13**) and the **nine-band colour scale** (**§2.4.16**). The viewer **shall not** use **`MapGenerator`** tile rasterization or **2 ft** bin quads as render geometry. Optional static cut/fill PNG exports intended to match in-cab **AgGrade** **may** use fixed-depth thresholds from **LAND-LEVELING-SPEC-2.md** **§14.1.2** and **`MapGenerator.cs`** **`GetCutFillBandIndexForDifference`**.

**[8.2.3]** Continuous **survey surfaces** and **3D** camera viewing (**§2.4.4**–**§2.4.6**, **§2.2.4**) are **Designer**-specific. **Contour lines** and **hillshading** are optional **decorations** (**§2.4.12**, **§7.5.2**). Implementations **may** reuse colour palettes from **AgGrade** but **shall not** use the static PNG pipeline in **`render.py`** or **bin-block** geometry for the viewer.

**[8.2.4]** **In-cab map rotation (informative contrast).** The in-cab **AgGrade** display in **`Application/Controls/Map.cs`** renders through **`MapGenerator.cs`** with **heading-dependent rotation**: cut/fill and elevation tiles are built in an unrotated field frame, then **rotated** so the tractor heading points **up** on screen. **`MapGenerator`** quantizes heading into buckets, expands canvas size for rotation, and caches tiles per heading (**`MAP_TILE_SPEC.md`**). Those optimizations **exist because the cab map rotates continuously with the machine**. **AgGrade Designer** **shall not** implement this rotating-tile pipeline (**§2.1.7**, **§8.2.1**).

### 8.3 Satellite basemap tiling

**[8.3.1]** **AgGrade** satellite underlay uses **`Application/Data/BruTileBasemapLayer.cs`**: **Esri World Imagery**, Web Mercator, **256×256** px tiles, zoom chosen from field scale, in-memory tile cache, optional offline read from disk.

**[8.3.2]** Offline prefetch: **`Application/Data/BasemapDownloader.cs`** — zoom **14**–**22**, files under **`SatelliteEsri/{z}/{x}/{y}.tile`**, **`manifest.json`**, default download centred on field (~40 ac footprint plus margins). UI workflow: **`Application/Controls/DownloadBasemapPage.cs`**, preview map **`MiniMap.cs`**.

**[8.3.3]** **AgGrade Designer** **shall** align satellite imagery with field topography per **§2.5.4**–**§2.5.5**, using the same provider, tile size, and offline cache layout as **§8.3.1**–**§8.3.2**. Field overlays use UTM/heading coordinates; basemap tiles use lat/lon Web Mercator — both paths **shall** be reconciled in the Designer viewer in the **north-up** **2D** frame (**§2.1.7**).

**[8.3.4]** **2D basemap pan/zoom model (informative reference).** For **2D** satellite underlay and geographic pan/zoom, implementations **should** follow the **north-up**, **Web Mercator** tile model in **`Application/Controls/MiniMap.cs`**: fixed north upward, WGS-84 centre lat/lon, discrete zoom levels, **256×256** px tile fetch, in-memory tile cache, smooth pan and wheel zoom with pointer anchoring, and lat/lon ↔ world-pixel conversion. **`MiniMap`** is used in **`DownloadBasemapPage.cs`** for offline-tile preview; the same geographic model **applies** to the main **Designer** **2D** viewer (**§2.1.2**, **§2.1.3**, **§2.1.7**). **`MiniMap`** does **not** rotate; neither **shall** **AgGrade Designer** **2D** (**§8.2.4**). In-cab **`BruTileBasemapLayer.cs`** remains the provider/offline-cache reference (**§8.3.1**–**§8.3.2**); **`MiniMap.cs`** is the reference for **how** to composite those tiles in a **north-up** viewport.

### 8.4 Units and grid conventions

**[8.4.1]** Elevations and design math **shall** use **metres** internally. Trimble multiplane files store **feet** on disk; conversion **shall** match **`Survey.cs`**.

**[8.4.2]** **AgGrade** uses **2 ft** (**0.6096 m**) **bins** for in-cab cut/fill **control**, **`.agd`** export, and design **volume** integration — UTM zone from the field southwest corner. **AgGrade Designer** **shall** use the same bin size and projection rules for **design computation** (**§5.2.6**), **`.agd`** export (**§9.3**), and **`.agdesign`** **`Grid`** persistence (**§A.4.3**) only (**§2.4.15**). Invalid or no-data bins **shall** use **`Field.BIN_NO_DATA_SENTINEL`** (**`-1000`**) internally and in **`.agdesign`** grids (**§A.4.4**, **§9.3.7**). The **viewer** **shall not** render these bins directly.

---

## 9. External file formats (normative)

**[9.0.1]** **AgGrade Designer** **shall** read and write external survey and design files using the layouts in this section. Files **shall** use **UTF-8** encoding (no BOM). Numeric fields **shall** use **`InvariantCulture`** parsing and **`G17`** (or equivalent full-precision) formatting unless a subsection specifies fixed decimal places.

**[9.0.2]** **AgGrade** reference implementations (**§8.1.1**) **shall** be treated as the compatibility oracle when this section is silent on an edge case.

### 9.1 Ag Survey File (`.ags`) (normative)

**[9.1.1]** An **Ag Survey File** (**`.ags`**) **shall** be a **UTF-8** text file of **comma-separated** rows. Each non-empty row **shall** represent one survey point or benchmark.

**[9.1.2]** **Data row layout:**

```text
Latitude (deg), Longitude (deg), Elevation (m), Code
```

**[9.1.3]** | Field | Type | Description |
|---|---|---|
| **Latitude** | decimal degrees | WGS-84 latitude. |
| **Longitude** | decimal degrees | WGS-84 longitude. |
| **Elevation (m)** | metres | Existing/measured ground elevation at the point. |
| **Code** | text | Row classification (**§9.1.9**). May be empty on read; **shall** be present on export for classified rows. |

**[9.1.4]** **Parsing rules:**

**[9.1.5]** 1. Split each line on **`,`** and trim surrounding whitespace from every field.
**[9.1.6]** 2. A row **shall** have at least **three** numeric fields (latitude, longitude, elevation). Rows with fewer fields **shall** be rejected.
**[9.1.7]** 3. If the first non-empty row is not numeric in all three leading fields, it **may** be skipped once as a **header row** (for example **`Latitude,Longitude,Elevation,Code`**).
**[9.1.8]** 4. Empty lines **shall** be ignored. An all-blank file **shall** be accepted as an empty survey.

**[9.1.9]** **Code classification (normative):**

**[9.1.10]** 1. **Master benchmark** — **Code** contains the substring **`mb`** (case-insensitive), for example **`MB`**, **`0mb_4g`**. Evaluated **before** regular benchmarks.
**[9.1.11]** 2. **Benchmark** — **Code** contains the substring **`bm`** (case-insensitive), for example **`BM1`**, and is not classified as master benchmark.
**[9.1.12]** 3. **Boundary** — **Code** equals **`2PER`** (case-insensitive).
**[9.1.13]** 4. **Interior** — any other **Code**, including empty.

**[9.1.14]** **Export row order (normative):** **Save** **shall** emit rows in this order:

**[9.1.15]** 1. **Master benchmark** row (exact stored **Code**; **shall** contain **`mb`**).
**[9.1.16]** 2. All other **benchmark** rows (stored order).
**[9.1.17]** 3. All **boundary** points with **Code** **`2PER`**.
**[9.1.18]** 4. All **interior** points with **Code** **`3GRD`**.

**[9.1.19]** Export **shall** require exactly one **master benchmark** before writing **`.ags`**.

### 9.2 Trimble Multiplane ASCII (normative)

**[9.2.1]** **Trimble Multiplane ASCII** **shall** be a **UTF-8** text file of **tab-separated** rows. The file extension **may** be **`.txt`**. The importer **shall** recognize the format by content (**§9.2.3** header), not by extension alone.

**[9.2.2]** **Unit mode:** If the basename of the file path contains **`_m`** (case-insensitive), all horizontal offsets and elevations in the file **shall** be interpreted as **metres**; otherwise **feet**. Conversion to internal storage **shall** use **1 ft = 0.3048 m**.

**[9.2.3]** **Header row (line 1)** — **shall** have at least **five** tab-separated columns:

```text
RecordId    OffsetX    OffsetY    MBElevation    MBLocation    [AnchorM]
```

**[9.2.4]** | Column | Index | Units | Description |
|---|---|---|---|
| **RecordId** | 0 | — | Typically **`0001`**. |
| **OffsetX** | 1 | ft or m | Easting offset applied to **MBLocation** to obtain master benchmark position. |
| **OffsetY** | 2 | ft or m | Northing offset applied to **MBLocation**. |
| **MBElevation** | 3 | ft or m | Master benchmark elevation in the **multiplane datum** (**§9.2.6**). |
| **MBLocation** | 4 | DMS text | **`MB `** followed by latitude DMS, **` / `**, then longitude DMS — for example **`MB N36:26:51.704 / W090:43:42.432`**. |
| **AnchorM** | 5 | metres | Optional absolute GPS elevation at **MB** for crash recovery. **`0`**, blank, or missing means not stored. |

**[9.2.5]** **DMS parsing:** Each coordinate **shall** use hemisphere letter (**`N`/`S`**, **`E`/`W`**) followed by **`degrees:minutes:seconds`** with **`:`** separators. Southern and western hemispheres **shall** be negative decimal degrees.

**[9.2.6]** **Multiplane elevation datum:** Internally, the master benchmark **shall** be stored at **30.48 m** (**100 ft**) relative elevation unless a legacy repair applies (**§9.2.12**). Other point elevations **shall** be stored relative to the same frame once **`AnchorM`** is known.

**[9.2.7]** **Data rows (line 2 onward)** — each non-empty line **shall** have at least **four** tab-separated columns:

```text
PointId    OffsetX    OffsetY    Elevation    [Code]
```

**[9.2.8]** | Field | Index | Units | Description |
|---|---|---|---|
| **PointId** | 0 | — | Sequential identifier (for example **`2`**, **`3`**, …). |
| **OffsetX** | 1 | ft or m | Easting offset from master benchmark in UTM space. |
| **OffsetY** | 2 | ft or m | Northing offset from master benchmark in UTM space. |
| **Elevation** | 3 | ft or m | Point elevation in multiplane-relative frame. |
| **Code** | 4 | text | **`MB`**, **`BM*`**, **`B`** (boundary), or empty (interior). |

**[9.2.9]** Horizontal positions **shall** be computed by converting **MBLocation** to UTM, applying header offsets, then applying each point’s **OffsetX**/**OffsetY** in the same UTM zone and hemisphere. All points **shall** lie in the **same UTM zone and hemisphere** as the master benchmark.

**[9.2.10]** **Code classification:** **`MB`** or codes starting with **`BM`** **shall** be **benchmarks**. **`B`** **shall** be **boundary**. Any other or empty code **shall** be **interior**.

**[9.2.11]** **Export** **shall** write the header row first, then non-**MB** benchmarks, then **boundary** rows (**Code** **`B`**), then **interior** rows (empty **Code**). **OffsetX**/**OffsetY** on export **shall** be UTM easting/northing relative to the master benchmark, formatted to **two** decimal places; elevations to **six** decimal places in file units.

**[9.2.12]** **Legacy multiplane files** that store absolute GPS elevations in data rows while omitting **`AnchorM`** **should** be repaired on import using the same detection and inference rules as **`Survey.cs`** **`TryRepairLegacyAbsoluteMultiplaneElevations`**.

### 9.3 Ag Design File (`.agd`) (normative)

**[9.3.1]** An **Ag Design File** (**`.agd`**) **shall** be a **UTF-8**, **comma-separated** text file representing a completed field design (existing and proposed elevations per point). The first line **shall** be a **header** and **shall** be skipped on read.

**[9.3.2]** **Canonical header line:**

```text
Latitude (deg), Longitude (deg), Elevation Existing(m), Elevation Proposed(m), CutFill(m), Code, Comments
```

**[9.3.3]** Each subsequent non-empty data row **shall** have fields by index:

```text
[0] Latitude            decimal degrees (WGS-84)
[1] Longitude           decimal degrees (WGS-84)
[2] Elevation Existing  metres (as-surveyed)
[3] Elevation Proposed  metres (design target surface)
[4] CutFill             metres, on disk = Proposed − Existing
[5] Code                row classification (§9.3.10)
[6] Comments            free text (ignored on read; may be empty on write)
```

**[9.3.4]** **Parsing rules:**

**[9.3.5]** 1. Split each line on **`,`** and trim surrounding whitespace from every field.
**[9.3.6]** 2. A row **shall** be treated as a **benchmark** if **Code** (case-insensitive) contains **`MB`** or **`BM`**. Benchmark rows **shall** supply **Elevation Existing**; **Elevation Proposed** and **CutFill** **may** be empty. Benchmark rows **shall not** contribute to the interior design grid on import.
**[9.3.7]** 3. **Data rows** — parsing **shall** match **`AgGrade/Application/Data/AGDLoader.cs`** **`LoadTopologyData`** for benchmarks, row classification, and **CutFill** sign handling (**§9.3.8**). A **data** row **shall** be accepted when **both** **Elevation Existing** and **Elevation Proposed** fields are **present** (non-empty after trim); **zero** is a **valid** elevation. Rows with **missing** elevation fields **shall** be skipped. **Invalid** or **no-data** grid bins **shall** use **`Field.BIN_NO_DATA_SENTINEL`** (**`-1000`**, **§8.4.2**, **§A.4.4**), not **zero**, when persisting or rendering surfaces.
**[9.3.8]** 4. **Sign convention (normative):** on disk **`CutFill = Proposed − Existing`** (negative = **cut**, positive = **fill**). The application **shall** compute internal **`delta = Existing − Proposed`** (positive = **cut**) from columns **[2]** and **[3]** and **shall not** trust column **[4]** when **[2]** and **[3]** are both present.
**[9.3.9]** 5. If **CutFill** is missing or zero but both elevations are present, **`CutFill`** **shall** be computed as **`Proposed − Existing`** on export.

**[9.3.10]** **Code classification:**

**[9.3.11]** 1. **Benchmark** — **Code** contains **`MB`** or **`BM`** (case-insensitive). Export **should** use **`0MB`** for master benchmark and **`0BMn`** or stored benchmark name for others, matching **`fielddesign.py`** **`agd_code_for_benchmark`**.
**[9.3.12]** 2. **Boundary** — **Code** equals **`2PER`** (case-insensitive).
**[9.3.13]** 3. **Interior** — typically **`3GRD`**; any non-benchmark row that is not **`2PER`**.

**[9.3.14]** **Boundary handling:** When **`2PER`** rows are present they **shall** define the field perimeter polygon; grid cells whose centres fall outside **shall** be excluded. When no **`2PER`** rows are present, the valid field mask **shall** be inferred from interior points using the same rules as **`fielddesign.py`** / **`AGDLoader.cs`**.

**[9.3.15]** **Export row order (normative):**

**[9.3.16]** 1. **Master benchmark** then other **benchmark** rows (**Elevation Proposed** and **CutFill** empty).
**[9.3.17]** 2. All valid **interior** and **boundary** grid points sorted by grid index (row-major), each with **Elevation Existing**, **Elevation Proposed**, and **CutFill = Proposed − Existing**.

**[9.3.18]** **Numeric formatting on export (normative):** latitude and longitude **shall** be written with **eight** digits after the decimal point; elevations and **CutFill** with **three** digits after the decimal point, matching **`fielddesign.py`** **`write_agd`**.

**[9.3.19]** **Grid bin size** for design points intended for in-cab **AgGrade** use **shall** be **2 ft** (**0.6096 m**) in UTM coordinates anchored at the field southwest corner (**§8.4.2**).

---

## Appendix A — Proposed `.agdesign` XML schema (normative)

**[A.0.1]** This appendix defines the **proposed** XML layout for **`.agdesign`** project files (**§4.2**, **§4.3**). Element names and optional sections **may** evolve in later **MINOR** revisions; incompatible changes **shall** increment **`schemaVersion`**.

**[A.0.2]** All floating-point values **shall** use **`InvariantCulture`** (**`G17`** or fixed decimal as noted). Elevations **shall** be in **metres**. Angles **shall** be in **degrees**. Volumes **shall** be in **cubic metres (m³)** so the file remains entirely metric.

### A.1 Document structure

**[A.1.1]** **Root element:**

```xml
<?xml version="1.0" encoding="UTF-8"?>
<AgDesignProject
    xmlns="https://aggrade.com/schemas/agdesign/1.0"
    schemaVersion="1.0">
  <!-- child elements in §A.2–§A.5 -->
</AgDesignProject>
```

**[A.1.2]** | Attribute | Required | Description |
|---|---|---|
| **`xmlns`** | yes | **`https://aggrade.com/schemas/agdesign/1.0`** for **`schemaVersion="1.0"`**. |
| **`schemaVersion`** | yes | Schema semver; **`1.0`** for this appendix. |

**[A.1.3]** Child elements **shall** appear in this order when present: **`Metadata`**, **`Survey`**, **`Design`**, **`ViewerState`**.

### A.2 Metadata

**[A.2.1]**

```xml
<Metadata>
  <CreatedUtc>2026-06-29T12:00:00Z</CreatedUtc>
  <ModifiedUtc>2026-06-29T14:30:00Z</ModifiedUtc>
  <Application>AgGrade Designer</Application>
  <ApplicationVersion>0.1.0</ApplicationVersion>
</Metadata>
```

**[A.2.2]** **`CreatedUtc`** and **`ModifiedUtc`** **shall** be ISO-8601 UTC timestamps. **`Application`** and **`ApplicationVersion`** **should** identify the writing application.

### A.3 Survey (existing topology)

**[A.3.1]** When the project contains survey data, a **`Survey`** element **shall** be present. When absent, the project has no existing topology.

**[A.3.2]**

```xml
<Survey sourceFormat="Ags" masterAbsoluteElevationM="412.345678">
  <Point role="Interior" latitude="49.12345678" longitude="-122.98765432" elevationM="412.100000" code=""/>
  <Point role="Boundary" latitude="49.12350000" longitude="-122.98700000" elevationM="411.950000" code="B"/>
  <Benchmark name="MB" latitude="49.12340000" longitude="-122.98720000" elevationM="412.345678"/>
</Survey>
```

**[A.3.3]** | Element / attribute | Description |
|---|---|
| **`Survey/@sourceFormat`** | **`Ags`**, **`Multiplane`**, **`Agd`**, or **`None`**. Last import source for the embedded survey set. |
| **`Survey/@masterAbsoluteElevationM`** | Optional. Master benchmark absolute GPS elevation (m), matching **`Survey.MasterAbsoluteElevationM`** (**§9.2**). |
| **`Point/@role`** | **`Interior`** or **`Boundary`**. |
| **`Point/@latitude`**, **`@longitude`** | WGS-84 decimal degrees (**§9.1**). |
| **`Point/@elevationM`** | Measured elevation in metres. |
| **`Point/@code`** | Optional classification string from source file. |
| **`Benchmark/@name`** | Benchmark name (**§3.3**). |
| **`Benchmark`** attributes | Same lat/lon/elevation as **`Point`**. |

**[A.3.4]** **`Point`** elements **shall** be sufficient to render **Existing topology** after **Open** (**§4.2.2**). Order **may** be arbitrary; duplicates **shall not** be written.

### A.4 Design (proposed topology)

**[A.4.1]** When the project contains a **design**, a **`Design`** element **shall** be present. When absent, the project has no proposed topology.

**[A.4.2]** **Parameters** — persisted dialog values (**§5.2.2**). **Import to field** and **Export from field** **shall** be stored in **m³** (**§A.0.2**). On **Save**, the application **shall** convert from **CY** (**§5.2.3**); on **Open**, **shall** convert back to **CY** for the design dialog if the UI presents **CY**.

```xml
<Design>
  <Parameters
      mainSlopeDirectionDeg="90"
      mainSlopePercent="0.15"
      crossSlopePercent="0.05"
      cutFillRatio="1.2"
      importToFieldM3="0"
      exportFromFieldM3="0"/>
  <!-- Grid in §A.4.3 -->
</Design>
```

**[A.4.3]** **Computed grid** — result of **§5.2.4**; required when a design exists so **Open** restores proposed and cut/fill surfaces without re-running the solver unless parameters change:

```xml
  <Grid
      binSizeM="0.6096"
      width="128"
      height="96"
      utmZone="10"
      utmNorth="true"
      originEastingM="500000.0"
      originNorthingM="5400000.0"
      planeA="-0.0015"
      planeB="0.0008"
      planeC="412.50">
    <Bin x="0" y="0" existingM="411.2" proposedM="411.0" valid="true"/>
    <Bin x="1" y="0" existingM="411.3" proposedM="411.1" valid="true"/>
    <!-- one Bin per grid cell; omit or valid="false" for no-data cells -->
  </Grid>
</Design>
```

**[A.4.4]** | Element / attribute | Description |
|---|---|
| **`Grid/@binSizeM`** | **`0.6096`** (**§8.4.2**). |
| **`Grid/@width`**, **`@height`** | Bin counts in X and Y. |
| **`Grid/@utmZone`**, **`@utmNorth`** | UTM zone for the field (**§8.4.2**). |
| **`Grid/@originEastingM`**, **`@originNorthingM`** | UTM of grid origin (**`minX`**, **`minY`** from **§5.2.6**). |
| **`Grid/@planeA`**, **`@planeB`**, **`@planeC`** | Solved design plane coefficients (**§5.2.9**, **§5.2.10**). |
| **`Bin/@x`**, **`@y`** | Bin indices (**0**-based). |
| **`Bin/@existingM`**, **`@proposedM`** | Existing and proposed elevation (m) at bin centre. Use **`-1000`** for no-data (**`Field.BIN_NO_DATA_SENTINEL`**). |
| **`Bin/@valid`** | **`true`** when bin is inside the valid field mask. |

**[A.4.5]** **Alternative compact encoding:** implementations **may** store **`Grid`** elevations as **Base64** **`float32`** row-major arrays **`Existing`** and **`Proposed`** with the same attributes as **§A.4.4**, provided **Open** yields identical bin values. If both **`Bin`** children and Base64 arrays are present, **`Bin`** **shall** take precedence.

### A.5 ViewerState (UI persistence)

**[A.5.1]** **`ViewerState`** **should** be written on **Save** so **Open** restores the workspace. All children are optional; omitted elements **shall** use application defaults.

**[A.5.2]**

```xml
<ViewerState>
  <ViewMode mode="2D"/>
  <FieldContentView view="Proposed"/>
  <DisplayToggles surveyPoints="true" contours="true" hillshade="true"
                   benchmarks="true" designOverlay="true" satellite="false"/>
  <VerticalExaggeration value="2.0"/>
  <Camera2D centreLat="49.12345" centreLon="-122.98765" zoom="1.0"/>
  <Camera3D orbitHeadingDeg="45" orbitPitchDeg="30" distanceM="500"/>
</ViewerState>
```

**[A.5.3]** | Element | Values |
|---|---|
| **`ViewMode/@mode`** | **`2D`** or **`3D`**. |
| **`FieldContentView/@view`** | **`Existing`**, **`Proposed`**, or **`CutFill`**. On **Open**, **§2.4.10** (case 4) **shall** override this when project data dictates a default. |
| **`DisplayToggles`** attributes | Booleans matching **§7.5.2** toggle names. |
| **`VerticalExaggeration/@value`** | **3D** exaggeration factor (**§2.3**). |
| **`Camera2D`** | Last **2D** pan/zoom centre and scale (**§2.1.2**, **§2.1.3**). |
| **`Camera3D`** | Last **3D** camera parameters — orbit centre, azimuth, pitch, and distance (**§2.2.2**, **§2.2.3**). |

### A.6 Minimal examples

**[A.6.1]** **Empty project** ( **Untitled** at startup, **§4.0.4** ):

```xml
<?xml version="1.0" encoding="UTF-8"?>
<AgDesignProject xmlns="https://aggrade.com/schemas/agdesign/1.0" schemaVersion="1.0">
  <Metadata>
    <CreatedUtc>2026-06-29T12:00:00Z</CreatedUtc>
    <ModifiedUtc>2026-06-29T12:00:00Z</ModifiedUtc>
    <Application>AgGrade Designer</Application>
    <ApplicationVersion>0.1.0</ApplicationVersion>
  </Metadata>
</AgDesignProject>
```

**[A.6.2]** **Survey only** — after survey import, **`FieldContentView`** **should** be **`Existing`** (**§2.4.10**); **`Survey`** present, **`Design`** absent.

**[A.6.3]** **Survey and design** — **`Survey`**, **`Design`** with **`Parameters`** and **`Grid`**; **`FieldContentView`** **should** reflect last session or **§2.4.10** (case 4) on **Open**.

**[A.6.4]** **Validation:** **`Open`** **shall** reject documents that are not well-formed XML, whose root is not **`AgDesignProject`**, whose **`schemaVersion`** is unsupported, or whose required child data is internally inconsistent (for example **`Design/Grid`** dimensions that do not match **`Bin`** count when **`Bin`** encoding is used).

---
