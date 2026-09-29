# Generated shadcn-style Razor Controls Design

## Goal

Make every new Anvil application ship with an editable, server-rendered Razor
control catalog inspired by shadcn/ui. Generated applications should be useful
immediately, teach users how the controls are composed, and let users customize
or delete controls without taking a dependency on a separate frontend package.

The catalog baseline follows the official shadcn/ui component list as of this
design: <https://ui.shadcn.com/docs/components>. It is a Razor translation of
the design language and interaction patterns, not a React or Tailwind runtime.

## Scope

This applies to the generated Identity profile. The default unauthenticated
profile receives the same `Components/Controls` catalog and shared theme, but
does not receive Identity-only dashboard examples. The work includes:

- Generated source files under `Components/Controls`.
- Shared CSS tokens and control styles in `wwwroot/app.css`.
- A small optional browser helper at `wwwroot/anvil-controls.js` for behavior
  that native HTML and Razor cannot provide alone.
- A generated `/components` showcase page demonstrating the catalog.
- Updates to the landing, auth, and dashboard pages so they use the controls.
- Generator tests, generated-project compilation checks, and documentation.

The controls are intentionally application-owned source. Anvil does not add a
runtime component assembly, React, Tailwind, npm, or a required frontend build
pipeline to generated applications.

## Generated structure

The generated project contains focused component folders. Component names use
PascalCase and each component exposes ordinary Razor parameters and child
content rather than stringly typed configuration.

```text
Components/
  Controls/
    Accordion/
    Alert/
    AlertDialog/
    AspectRatio/
    Attachment/
    Avatar/
    Badge/
    Breadcrumb/
    Bubble/
    Button/
    ButtonGroup/
    Calendar/
    Card/
    Carousel/
    Chart/
    Checkbox/
    Collapsible/
    Combobox/
    Command/
    ContextMenu/
    DataTable/
    DatePicker/
    Dialog/
    Direction/
    Drawer/
    DropdownMenu/
    Empty/
    Field/
    Form/
    HoverCard/
    Input/
    InputGroup/
    InputOtp/
    Item/
    Kbd/
    Label/
    Marker/
    Menubar/
    Message/
    MessageScroller/
    NativeSelect/
    NavigationMenu/
    Pagination/
    Popover/
    Progress/
    Questionnaire/
    RadioGroup/
    Resizable/
    ScrollArea/
    Select/
    Separator/
    Sheet/
    Sidebar/
    Skeleton/
    Slider/
    Spinner/
    Switch/
    Table/
    Tabs/
    Textarea/
    Toast/
    Toggle/
    ToggleGroup/
    Tooltip/
    Typography/
```

The implementation may group small primitives into a folder, but every named
control must have a discoverable Razor entry point and be independently
deletable. Shared helpers must remain small and obvious; deleting one control
must not require deleting unrelated controls.

## Component contracts

All controls follow these conventions:

- `Class` is available where the root element can accept custom classes.
- Additional attributes are forwarded with `@attributes` where valid.
- Content is represented by `RenderFragment` or named fragments for compound
  controls.
- Enumerated visual choices use explicit C# enums or static option values.
- Inputs expose `Name`, `Value`, `ValueChanged`, `Id`, `Disabled`, and standard
  HTML attributes where applicable.
- Interactive controls expose stable `Id`/`TriggerId` values and ARIA metadata.
- Form controls render labels, descriptions, and errors without taking over
  the application's validation model.
- Table, chart, calendar, and list controls accept application-owned data and
  do not fetch data implicitly.

Representative APIs:

```razor
<AnvilButton Variant="AnvilButtonVariant.Primary" Type="submit">
    Save changes
</AnvilButton>

<AnvilCard>
    <AnvilCardHeader>
        <AnvilCardTitle>Projects</AnvilCardTitle>
        <AnvilCardDescription>Your latest work.</AnvilCardDescription>
    </AnvilCardHeader>
    <AnvilCardContent>...</AnvilCardContent>
</AnvilCard>

<AnvilDialog TriggerLabel="Open details">
    <AnvilDialogContent>...</AnvilDialogContent>
</AnvilDialog>
```

Exact parameter names may vary when native HTML requires a more appropriate
contract, but the component must remain understandable from its `.razor` file
and usage example.

## Behavior strategy

The default behavior is progressive enhancement:

- Accordion and Collapsible use `details`/`summary` semantics where possible.
- Dialog, AlertDialog, Drawer, Sheet, Popover, HoverCard, and ContextMenu use
  semantic HTML, ARIA state, and the native Popover/Dialog APIs where suitable.
- Carousel uses CSS scroll snapping and buttons that work as ordinary links or
  buttons before enhancement.
- Calendar and DatePicker use native date inputs as the baseline and add a
  server-rendered calendar surface for richer selection.
- Chart renders accessible SVG from supplied data and includes a text/table
  fallback; it does not require a charting dependency.
- DataTable, Pagination, Table, and Command remain server-rendered and accept
  application data; optional filtering/sorting hooks use ordinary form posts
  or query strings.
- Combobox, Command, Resizable, Sidebar, Toast, Tooltip, and keyboard-driven
  overlays may use the small generated `anvil-controls.js` helper. The helper
  only enhances elements marked with `data-anvil-control` attributes and may be
  deleted if the application does not need those behaviors.

No component silently performs network requests, mutates application state, or
introduces a server-side data dependency.

## Showcase and starter views

The generated project includes `/components`, a public catalog page grouped by
control family. Every showcase entry includes:

- The rendered control.
- A short explanation of its purpose.
- A compact Razor usage example in a `<code>` block.
- The relevant interactive states where safe to demonstrate.

The generated starter pages use controls as real examples:

- Home: Badge, Button, ButtonGroup, Card, Avatar, Separator, and Typography.
- Login/register: Form, Field, Label, Input, Checkbox, Button, and Alert.
- Dashboard: Card, Badge, Tabs, Table, Progress, Skeleton, DropdownMenu,
  Chart, and Empty.
- Components page: all generated controls, including advanced controls.

The showcase route is public and does not expose private user data. Dashboard
examples use static sample data and clearly label it as starter content.

## Theme and assets

The generated stylesheet defines shadcn-style semantic tokens for light and
dark modes, including background, foreground, card, popover, primary,
secondary, muted, accent, destructive, border, input, ring, and radius. Control
styles consume tokens instead of hardcoded page-specific colors.

The generated `App.razor` includes `wwwroot/anvil-controls.js` after the normal
Anvil/browser runtime. The script is small, dependency-free, and safe to omit
when no enhanced control is used.

## Compatibility and security

- All controls compile in the generated .NET 10 Razor application without
  additional package references.
- The default profile receives controls that do not require Identity services.
- Route authorization remains explicit; the public showcase uses
  `[AllowAnonymous]`.
- Mutating examples retain antiforgery tokens and use existing Anvil form
  conventions.
- Interactive controls must preserve keyboard access, focus visibility, labels,
  and appropriate ARIA attributes.
- Generated components must not render unsanitized application HTML by default.

## Verification

Tests and verification must prove:

- Every catalog control has a generated source entry point.
- Identity and default profiles generate the expected control structure.
- The generated project compiles with the complete catalog and script.
- The showcase page references every catalog control and uses real component
  tags rather than placeholder prose only.
- Home, auth, and dashboard pages use generated controls.
- Generated controls forward classes/attributes and render child content for
  representative primitive and compound controls.
- Advanced controls render a usable native/server fallback when JavaScript is
  unavailable.
- The generated package-mode project restores with the current Raukeld package
  IDs and does not require a Node toolchain.
