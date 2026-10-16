# Better Probes & Injectors

Find, cycle through, and return Q-SYS Signal Probes and Signal Injectors home.

**Development candidate 0.1.0.** The source builds offline. Designer interaction,
physical shortcuts, native Find appearance and Emulate/Run behavior still need
application validation. This candidate has not been installed, published or
approved for the mod catalog. Computer Use checks are deferred at the owner's
request. See [validation and remaining checks](docs/VALIDATION.md).

## Native Find

Select a Signal Probe or Signal Injector component and use Designer's existing
**Find > Context** mode. The mod appends an attached-tip destination and a home
component destination, while retaining the native results. An attached-tip
destination invokes Designer's native pin navigation, including its page opening
and highlighting behavior. The home destination locates the owning schematic
component; it does not detach the tool or automatically open its control panel.

The Find heading identifies the tool and its position in its type's cycle.
When no reliable attachment state is available, the panel still offers its home
component and reports that the attachment is unavailable. Absence from Designer's
transient tip dictionary is not, by itself, evidence that a tool is home.

Native Find integration, closed-page tip reconstruction and nested schematic
navigation are implemented against inspected 10.5 internals but remain untested
in the application. Existing snapshot and control-copy destinations are retained.

## Commands

The mod registers these actions with Mods Core and adds them to **Tools > Q-SYS
Mods**. Better Shortcuts can assign keys and Command Palette can discover them;
neither feature mod is required for the menu actions.

| Command | Behavior |
| --- | --- |
| Next / Previous Probe | Cycle probes in the current document. |
| Next / Previous Injector | Cycle injectors independently. |
| Find Focused Probe/Injector | Reveal the attached audio pin, or the home component when known to be unattached. |
| Find Probe/Injector Home Component | Reveal the owning schematic component without detaching. |
| Return Focused Probe/Injector Home | Invoke the native detach helper for exactly one resolved tool. |

No default keys are assigned. Existing user assignments and Ctrl+Shift+P are
unchanged. Assign commands in Better Shortcuts after the candidate passes its
application checks.

Cycling orders each type by page name, component display name, then stable
component GUID, and wraps in both directions. It includes tools whose attachment
is unavailable by navigating to their home component. Each type remembers its
cycle position for the current document. This memory is never used as a fallback
target for Return Home.

The focused tool must resolve uniquely from one selected component, probe or
injector control, deployed tip, or an audio pin/pin label with one verified active
attachment. Multiple or unrelated selections disable focused commands. Commands
are limited to main-window schematic/component canvas contexts and their menus;
native Find has its own guarded callback path. Floating command dispatch, UCI and
embedded-editor contexts are not claimed supported. Text/script input and active
placement cursors are excluded.

Return Home requires a connected Emulate/Run state and a consistent native
attachment. It clears the native attachment and verifies the local value/map
update. It does not merely hide a graphic. It neither changes gain/mute nor
returns other tools home. This changes the tool's signal-path attachment; runtime
or hardware confirmation and Undo behavior have not been established.

## Build and offline checks

Use .NET SDK 10.0.112 from this directory. Keep an adjacent `qsys-mods-core`
checkout at `63aa418b8872126be09a0896f25ba260fa30f457` (Core 0.1.6), or set
`CoreProject` explicitly. Runtime support is Core >=0.1.6 and <0.2.0. Do not mix
Windows and WSL generated outputs.

From WSL, with the supported Designer installed on Windows:

```sh
bash scripts/check.sh '/mnt/c/Program Files/QSC/Q-SYS Designer 10.5' ../build
```

The script builds the mod, runs the portable behavior tests and reads PE metadata
to validate host contracts. It does not load vendor assemblies, launch Designer,
use Computer Use or connect to a Core. The test projects have no third-party
package dependencies. CI runs the portable suite; local vendor references are
required for the full build and metadata checks.

## Compatibility and packaging

The supported research target is Q-SYS Designer **10.5.0 (0-2609.007 10.5)**.
Mods Core checks the exact Designer and ModuleCommon hashes before registration.
The mod also checks Core's version before installing hooks. Private reflection
and Harmony hooks are version-specific implementation details, not a public
Q-SYS plugin API.

- Mod ID: `better-probes-injectors`
- Assembly: `Designer.Modules.BetterProbesInjectors.dll`
- Intended repository: `joshua-small/qsys-better-probes-injectors` (not created)
- Proposed payload: only the mod assembly; Core/Harmony remain managed dependencies
- Current work item: [Better Shortcuts #11](https://github.com/joshua-small/qsys-better-shortcuts/issues/11)

Catalog metadata and release-note drafts are included for future onboarding.
The prospective repository URL is reserved metadata, not a published project.
No catalog approval, installable `.qdsmod`, release workflow or installation is
included in this milestone. Onboarding must verify the exact identity, dependency
range, shared Harmony provision and private payload against the current
[package contract](https://github.com/joshua-small/qsys-mod-catalog/blob/main/PACKAGE-CONTRACT.md).

See [implementation evidence](docs/NATIVE-CONTRACT.md) and [ownership](NOTICE.md).
