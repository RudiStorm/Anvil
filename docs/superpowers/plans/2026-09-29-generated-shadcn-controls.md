# Generated shadcn-style Razor Controls Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (native execution) to implement this plan task-by-task.

**Goal:** Generate a complete, editable shadcn-style Razor control catalog in every new Anvil application and use it throughout the starter experience.

**Architecture:** Keep controls application-owned source files under `Components/Controls`, generated from a catalog manifest in `Anvil.Cli`. Use semantic Razor/HTML and CSS first, with one small optional `wwwroot/anvil-controls.js` enhancement layer for interactions that need browser behavior. Add a public `/components` showcase page and migrate the generated landing, auth, and dashboard pages to the controls.

**Tech Stack:** C#/.NET 10, ASP.NET Core, Razor components, server-rendered HTML, CSS custom properties, dependency-free browser JavaScript, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-29-generated-shadcn-controls-design.md`

## Global Constraints

- Generated applications must not require React, Tailwind, npm, or a frontend build pipeline.
- Controls must be application-owned source and independently deletable.
- Components must use semantic HTML, preserve keyboard/focus accessibility, and avoid unsanitized HTML by default.
- Identity behavior and antiforgery conventions remain unchanged.
- Both Identity and default profiles receive the control catalog; only Identity receives authenticated dashboard examples.
- Generated package-mode projects must continue using the current `Raukeld.Anvil` and `Raukeld.Anvil.Razor` package IDs and version.

## Review Focus

- Catalog completeness: every official catalog entry has a generated, discoverable Razor entry point; pinned by Task 1 tests.
- Generated project size and compilation: the full catalog compiles in both profiles without hidden package dependencies; pinned by Tasks 6 and 8.
- Progressive enhancement: advanced controls remain usable without JavaScript; pinned by Task 5 rendering tests.
- Attribute/child-content contracts: representative primitive and compound controls forward classes, attributes, and fragments; pinned by Tasks 2–4.
- Scaffold teaching value: showcase and starter pages use real controls with readable examples; pinned by Task 7 generation tests.

---

### Task 1: Define the control catalog and generator primitives

**Files:**
- Modify: `src/Anvil.Cli/Program.cs`
- Modify: `tests/Anvil.Tests/CliTests.cs`
- Create: `src/Anvil.Cli/GeneratedControls.cs` if the catalog/generator helpers need to leave `Program.cs` focused on orchestration

**Interfaces:**
- Produces an internal catalog model containing the canonical control name, folder, generated files, showcase label, and behavior flags.
- Produces a generator operation that writes all catalog files into `Components/Controls` and returns the generated relative paths for tests/showcase generation.

- [ ] **Step 1: Write failing catalog tests** asserting the complete component-name set from the spec, stable PascalCase folders, generation for both `identity` and `default` profiles, and independent source files.
- [ ] **Step 2: Run the focused CLI tests and verify they fail because the catalog does not exist.**
- [ ] **Step 3: Implement the catalog manifest and file-generation primitives.** Keep the manifest data-driven so adding a control does not require another branch in `CreateProject`.
- [ ] **Step 4: Generate `_Imports.razor` additions and the control namespace conventions without making every component globally coupled.
- [ ] **Step 5: Run the focused catalog tests and verify they pass.**

### Task 2: Generate theme tokens and foundational primitives

**Files:**
- Modify: `src/Anvil.Cli/Program.cs` or the generated-control template file from Task 1
- Modify: `tests/Anvil.Tests/CliTests.cs`
- Generated output under: `Components/Controls/Button`, `Badge`, `Card`, `Avatar`, `Separator`, `Typography`, `AspectRatio`, `Kbd`, `Marker`, `Item`, `Empty`, `Skeleton`, `Spinner`, `Progress`
- Modify generated: `wwwroot/app.css`

**Interfaces:**
- Produces `AnvilButton`, `AnvilBadge`, `AnvilCard*`, and foundational controls with `Class`, attribute forwarding, and `RenderFragment` contracts.
- Produces semantic theme tokens consumed by every later control family.

- [ ] **Step 1: Add failing source-generation assertions for primitive names, representative parameters (`Variant`, `Size`, `Class`, `ChildContent`), and theme tokens.
- [ ] **Step 2: Implement the primitive Razor templates and shared CSS using semantic tokens, not page-specific colors.
- [ ] **Step 3: Add representative attribute forwarding and child-content tests.
- [ ] **Step 4: Generate a minimal fixture app and build it to verify the primitives compile.
- [ ] **Step 5: Run CLI tests and fixture build; verify pass.**

### Task 3: Generate form and input controls

**Files:**
- Modify: control catalog/template source from Task 1
- Modify: `tests/Anvil.Tests/CliTests.cs`
- Generated output under: `Components/Controls/Field`, `Form`, `Label`, `Input`, `InputGroup`, `Textarea`, `Checkbox`, `RadioGroup`, `Switch`, `Slider`, `Select`, `NativeSelect`, `InputOtp`, `Combobox`

**Interfaces:**
- Produces form controls with ordinary HTML names/values and optional `ValueChanged` callbacks, without replacing Anvil server-side validation.
- Produces `AnvilField` composition for label, description, input, and error content.

- [ ] **Step 1: Add failing tests for generated form contracts, labels/ids, required/disabled behavior, `@attributes`, and form-safe rendering.
- [ ] **Step 2: Implement the primitive form controls using native HTML as the baseline.
- [ ] **Step 3: Implement the compound `Field`, `Form`, and `Combobox` templates with stable IDs and ARIA relationships.
- [ ] **Step 4: Verify representative generated markup contains no unsafe raw HTML and remains usable without JavaScript.
- [ ] **Step 5: Run focused tests and a generated-project build.**

### Task 4: Generate layout, navigation, feedback, and overlay controls

**Files:**
- Modify: control catalog/template source from Task 1
- Modify: `tests/Anvil.Tests/CliTests.cs`
- Generated output under: `Components/Controls/Accordion`, `Alert`, `AlertDialog`, `Collapsible`, `ContextMenu`, `Dialog`, `Drawer`, `DropdownMenu`, `HoverCard`, `Menubar`, `NavigationMenu`, `Popover`, `Sheet`, `Sidebar`, `Tabs`, `Toast`, `Toggle`, `ToggleGroup`, `Tooltip`, `ScrollArea`, `Resizable`, `Direction`

**Interfaces:**
- Produces overlay controls with explicit trigger/content IDs, `Open`/`OpenChanged` where state is server-owned, and named child-content fragments.
- Produces disclosure controls with native fallback markup and data attributes for optional enhancement.

- [ ] **Step 1: Add failing tests for native fallback markup, ARIA attributes, stable IDs, trigger/content relationships, and compound fragments.
- [ ] **Step 2: Implement disclosure and navigation controls with semantic HTML first.
- [ ] **Step 3: Implement overlay controls with native `<dialog>`, popover, or disclosure fallbacks and explicit enhancement markers.
- [ ] **Step 4: Implement `Resizable`, `ScrollArea`, and direction-aware primitives without requiring a client framework.
- [ ] **Step 5: Run focused render-contract tests and compile a generated fixture.**

### Task 5: Add the optional browser enhancement layer and advanced data controls

**Files:**
- Modify: control catalog/template source from Task 1
- Create generated: `wwwroot/anvil-controls.js`
- Modify generated: `Components/App.razor`
- Modify: `tests/Anvil.Tests/CliTests.cs`
- Generated output under: `Components/Controls/Calendar`, `Carousel`, `Chart`, `DataTable`, `DatePicker`, `Command`, `Message`, `MessageScroller`, `Questionnaire`, `Attachment`, `Bubble`, `Breadcrumb`, `Pagination`, `Table`

**Interfaces:**
- Produces a dependency-free enhancement script that initializes only `[data-anvil-control]` elements and does not fetch or mutate application data.
- Produces data controls that accept application-owned collections, render accessible fallbacks, and expose query/form hooks rather than implicit persistence.

- [ ] **Step 1: Add failing tests for script inclusion, enhancement markers, no-script fallbacks, accessible SVG chart output, and table/list rendering.
- [ ] **Step 2: Implement the browser helper for keyboard interactions, focus management, carousel movement, command/combobox behavior, toast/tooltip behavior, and resizable/sidebar affordances.
- [ ] **Step 3: Implement calendar/date picker native fallbacks and server-rendered calendar surfaces.
- [ ] **Step 4: Implement chart SVG plus text/table fallback, data table/pagination contracts, and the remaining advanced data/display controls.
- [ ] **Step 5: Verify the controls render without the script and that the script is safe to remove.
- [ ] **Step 6: Run the advanced-control tests and generated-project build.**

### Task 6: Integrate controls into the generated application shell

**Files:**
- Modify: generated template methods in `src/Anvil.Cli/Program.cs` or their extracted template files
- Modify: `tests/Anvil.Tests/CliTests.cs`
- Generated: `Components/Pages/Public/Home.razor`
- Generated: `Components/Pages/Auth/Login.razor`
- Generated: `Components/Pages/Auth/Register.razor`
- Generated: `Components/Pages/App/Dashboard.razor`

**Interfaces:**
- Starter pages consume generated controls through the generated component namespace and preserve the existing routes, Identity handlers, antiforgery tokens, and authorization metadata.

- [ ] **Step 1: Add failing generation assertions that the starter pages contain real control tags and preserve their existing route/auth metadata.
- [ ] **Step 2: Replace page-local button, card, input, alert, metric, and navigation markup with generated controls.
- [ ] **Step 3: Ensure auth forms still submit ordinary URL-encoded forms to `/account/login/submit` and `/account/register/submit`.
- [ ] **Step 4: Build a generated Identity project and verify the pages compile and render through the existing route structure.
- [ ] **Step 5: Run focused generation and Identity tests.**

### Task 7: Generate the public component showcase

**Files:**
- Modify: generator page/template methods
- Modify: `tests/Anvil.Tests/CliTests.cs`
- Generated: `Components/Pages/Public/Components.razor`

**Interfaces:**
- Produces public `/components` with one discoverable showcase section per catalog entry, including rendered examples and readable Razor snippets.

- [ ] **Step 1: Add failing tests that every catalog name appears in the showcase and that the page is anonymous/public.
- [ ] **Step 2: Implement showcase section generation from the same catalog manifest used for source files.
- [ ] **Step 3: Add representative interactive states and static sample data without private data or implicit network calls.
- [ ] **Step 4: Build the generated project and verify the showcase route is present.
- [ ] **Step 5: Run generation tests and inspect the generated showcase markup for readability.**

### Task 8: Documentation, acceptance verification, and plan tracking

**Files:**
- Modify: `README.md`
- Modify: `AnvilDocumentation.md`
- Modify: `docs/releasing.md`
- Modify: `build plan.md`
- Modify: `tests/Anvil.Tests/ReleaseAcceptanceTests.cs`
- Modify: `.superpowers/sdd/2026-09-29-generated-shadcn-controls/progress.md`

**Interfaces:**
- Documents the generated control catalog, customization/deletion workflow, showcase route, and no-Node behavior.

- [ ] **Step 1: Add release acceptance checks for complete catalog generation, package-mode references, no Node files, showcase route, and generated script.
- [ ] **Step 2: Update documentation with control usage, file ownership, customization, deletion, and accessibility guidance.
- [ ] **Step 3: Mark the completed build-plan item in `build plan.md` using accurate wording.
- [ ] **Step 4: Run focused tests, solution build, generated default/Identity builds, package-mode restore, and `git diff --check`.
- [ ] **Step 5: Record verification results and any known full-suite baseline failures in the execution ledger.**
