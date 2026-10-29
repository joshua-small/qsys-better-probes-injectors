# Better Probes & Injectors

Find, cycle through, and return Q-SYS Signal Probes and Signal Injectors home.
This source is the 0.1.3 candidate for Designer **10.5.0 (0-2609.007 10.5)** with
Mods Core >=0.1.6 and <0.2.0. See [validation](docs/VALIDATION.md) for the accepted
application scope and remaining checks.

## Native Find

Select a Signal Probe or Signal Injector component and use Designer's existing
**Find > Context** mode. The mod appends attached-tip and home-component results
while retaining native results. The heading identifies the tool, its position
in the type's cycle and its attachment location. Selecting the attached result
uses native page navigation, then selects and highlights the verified tip.
The home result locates its owning schematic component without detaching.

When attachment state is unavailable, the panel still offers the home component.
A missing transient tip dictionary entry alone does not establish that a tool is
home. Same-page native Find is application-validated; closed-page reconstruction
and nested schematic navigation remain untested.

## Commands

Seven actions appear under **Tools > Q-SYS Mods** and register with Mods Core.
Mods Core supplies these defaults. Better Shortcuts can customize them; Command
Palette can discover registered commands. Neither feature mod is required for
menu actions or the default keys. Existing user assignments, including cleared
bindings, take precedence. A conflicting default stays inactive until resolved
in shortcut preferences; an upgrade never overwrites another action's keys.

| Command | Default shortcut | Behavior |
| --- | --- | --- |
| Next Probe | Ctrl+F11 | Cycle probes in the current document. |
| Previous Probe | Ctrl+Shift+F11 | Cycle probes backward. |
| Next Injector | Ctrl+F12 | Cycle injectors independently. |
| Previous Injector | Ctrl+Shift+F12 | Cycle injectors backward. |
| Find Focused Probe/Injector | Unassigned | Reveal the attached tip, or its home when known unattached. |
| Find Probe/Injector Home Component | Unassigned | Reveal the owner without detaching. |
| Return Focused Probe/Injector Home | Ctrl+Alt+H | Detach exactly one resolved tool and locate its owner. |

Cycles sort by page, display name and stable component GUID, wrapping in both
directions. Unknown attachments navigate to the owner. Per-document cycle memory
is never a fallback target for Return Home.

Focused commands require one uniquely resolved component, tool control, deployed
tip, or audio pin/pin label with a verified attachment. Multiple and unrelated
selections are excluded. Commands accept main-window schematic/component canvases
and their menus. Native Find has its own guarded callback. Text/script input and
active placement cursors are excluded. Floating dispatch, UCI and embedded-editor
contexts are not claimed supported.

Return Home requires connected Emulate/Run state and consistent attachment data.
It uses the native detach helper, verifies the local value/map update, clears the
old highlight and locates the owner. It preserves gain/mute and other tools.
Emulate behavior is validated; live Core/hardware and Undo are not established.

## Install

Use Q-SYS Mod Manager's signed catalog. The schema-3 package contains only
`Designer.Modules.BetterProbesInjectors.dll`; Core and Harmony remain managed
dependencies. The mod ID is `better-probes-injectors`. Exact Designer and
ModuleCommon hashes and Core's version are checked before registration.
Private reflection and Harmony hooks are version-specific internals, not a public
Q-SYS plugin API. See the [package contract](https://github.com/joshua-small/qsys-mod-catalog/blob/main/PACKAGE-CONTRACT.md).

## Build

The portable regression suite needs only the .NET SDK from `global.json`:

```sh
dotnet run --project tests/Logic -c Release --artifacts-path ../build/logic
```

It compiles the production navigation and canvas classification logic with
synthetic host objects, without Designer, vendor DLLs or Mods Core. It checks cycling and Return Home across
document, selection, attachment and connection transitions. See
[validation](docs/VALIDATION.md#portable-lifecycle-regressions) for its limits.

Use the .NET SDK selected by `global.json` and an adjacent `qsys-mods-core` checkout
at release v0.1.6 (`e3ecf33918488aab01aefdd9546c9992f0e7b0dc`), or set
`CoreProject` explicitly. Building against that minimum version preserves the
existing Core dependency; newer Core 0.1.x versions remain eligible at runtime.
Keep Windows and WSL outputs separate. From WSL:

```sh
bash scripts/check.sh '/mnt/c/Program Files/QSC/Q-SYS Designer 10.5' ../build
```

This builds Release, runs portable logic checks and reads host PE metadata.
It does not execute vendor assemblies, launch Designer or connect to a Core.
CI runs portable checks; the full local build needs installed vendor references.
The pinned release publisher produces the catalog descriptor from a stable release.

See [native contract](docs/NATIVE-CONTRACT.md), [ownership](NOTICE.md) and the
[original work item](https://github.com/joshua-small/qsys-better-shortcuts/issues/11).

## Window and interaction behavior

Keyboard and Tools menu commands exclude GraphicCanvas roots that are UCI pages,
including derived page types. Schematic canvases and probe control panels retain
their existing command context. Moving focus from a schematic into a UCI clears
the remembered canvas context before a menu can reuse it. Native Find callbacks
retain their separate selection and attachment checks.

The 0.1.3 artifact still requires the scoped application checks described in
[validation](docs/VALIDATION.md#candidate-013-canvas-exclusion). No new Designer
10.5.1 or hardware acceptance is claimed by the portable suite or build.
