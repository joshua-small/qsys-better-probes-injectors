# Validation

## Candidate 0.1.3 canvas exclusion

Keyboard/menu context rejects UCI page roots using runtime assignability, so
derived page types are excluded too. The same classification is used when
remembering focus and when deciding whether commands can run. Schematic and
component-panel contexts retain their previous behavior; native Find remains a
separately guarded callback path.

The portable suite contains **227 checks**: the 192 navigation/lifecycle checks
below plus 35 canvas checks. The latter cover schematic and component roots,
base and multiply-derived UCI page roots, derived graphic canvases, unrelated
objects with similar names, changed roots and rejection with a retained tool
selection. Both production helpers are compiled directly into the test project.
The new metadata checks verify the GraphicCanvas Root member and UCI page type.

An additional **27 adapter checks** compile the actual `DesignerHost.cs` focus
methods with synthetic framework types. They reproduce a remembered root changing
to UCI without a focus callback, and an excluded UCI nested inside a schematic.
The initial adapter failed nine assertions; root revalidation and stopping at
the nearest graphic canvas resolve those failures. Supported menu/toolbar reuse,
document changes, hidden/disabled canvases, text input, foreign windows and the
separate native Find route are checked too. These objects do not establish that
Designer creates the synthetic nested-parent shape or delivers a particular event.

These tests do not simulate WPF focus events or prove native menu/key behavior.
For the exact rebuilt DLL, application acceptance must still confirm UCI focus
exclusion, menu context after switching from a schematic, recovery when returning
to a schematic/control panel, and unchanged native Find/Return Home behavior.
Use a separate saved fixture, record DLL and host hashes, and verify unrelated
attachments and preferences are preserved. Older application evidence below
does not establish acceptance of the new binary. Designer 10.5.1, live Core and
hardware acceptance are not claimed here.

## Release 0.1.1 default shortcuts

Validated on 2026-10-16 in the isolated Designer 10.5 installation with Mods Core
0.1.6.0 and an empty preference store (no saved shortcut overrides). Physical
Computer Use input verified Ctrl+F11 and Ctrl+Shift+F11 selecting the two probes,
and Ctrl+F12 and Ctrl+Shift+F12 selecting the two injectors, including backward
wraparound. In Emulate, Ctrl+Alt+H detached the focused injector from Mixer 8x8
Input 1, restored its native panel icon and selected its owning component.
Action and return receipts corroborated the visible results. The preference
store remained empty, establishing that the bindings came from the mod defaults.

The same five chords had also passed physical-input checks with explicit user
assignments on 0.1.0, including Return Home for both tool types. This release only
adds default gestures and changes its version; navigation and detach logic are
unchanged. User overrides and default conflict handling remain owned by Mods Core.
Release compilation again passed with zero warnings/errors, 65 portable logic
checks and 73 host metadata checks. No hardware acceptance is implied.

Validated 0.1.1 DLL SHA-256:
`3bac1f943c00f7924417be746d8b821c42a9bec5cc4d46106bcbb8c8c87c7860`.

## Release 0.1.0 acceptance

Validated on 2026-10-16 using native Computer Use in a disposable Designer
10.5.0 (0-2609.007 10.5) installation. A separate copy of the small Audio - Mixers
Meters and Test fixture contained two probes and one injector. The original
fixture was preserved. Test keybindings were confined to the isolated Mods Core
preferences; the package ships no default keys.

Observed in Designer:

- Normal module startup, exact host/Core acceptance and all seven menu commands.
- Native Find by Context from both owning components, with attached and home rows,
  type/count headings and correct attachment descriptions.
- Actual tip highlighting for a probe on Pink Noise output and an injector on an
  unwired Mixer 8x8 Input 1, including both tools attached at once.
- Physical custom shortcuts for both cycle directions, two-probe wraparound,
  single-injector wraparound and independent type selection.
- Find Focused selects/highlights each attached tip. Find Home selects the owner
  without detaching. Return Home removes only the focused tool; the other
  attachment and visible gain/mute values remain unchanged.
- Native return removes the graphic and restores the tool's control-panel icon.

Application testing exposed and fixed empty pin-label navigation, null native
FocusGeometry on both tip types, and valid empty net names on unwired injector
inputs. A final return-home fix clears the removed tip's highlight and locates
its owner.

## Automated verification

Release 0.1.1 compilation succeeded with zero warnings and errors. **65 portable logic
checks** cover typed cycles, duplicate labels/identities, excluded contexts,
stale document/selection/attachment, reconnect epochs, deferred navigation,
receipt failure, exactly-one detach and preservation of other tools.
**73 PE metadata checks** cover exact host hashes, reflected signatures/maps,
Find callbacks, both tip geometry overrides and assembly references/version.
These tests do not execute vendor code and do not substitute for UI acceptance.

### Portable lifecycle regressions

The navigation/lifecycle portion has **192 checks**, including 127 additional lifecycle
checks against the production `ToolNavigation` implementation. The fixture uses
four probes and four injectors with duplicate labels and synthetic component,
pin, control and value identities. It requires no Designer installation, vendor
DLLs or saved design files.

The additional cases cover:

- Forward/backward wrapping, changed enumeration order, deletion down to an
  empty family, repopulation, explicit focus and independent family cursors.
- Document switches, returning to a previous document, a newly busy context
  and a host navigation exception without advancing the cycle cursor.
- Nineteen state changes during Return Home receipt writing, for each tool
  type. The second validation must reject document/selection/focus changes,
  deletion or duplicate identity, unknown or retargeted attachments, replaced
  pins/controls/values, disconnect/reconnect, busy/editing/placement contexts,
  and a tool already detached elsewhere. Rejection must not call native return.
- Stable target identity despite changes to display labels or enumeration;
  exactly one successful return, preservation of the other seven attachments
  and an idempotent repeated return.

Run from a source checkout or clean export:

```sh
dotnet run --project tests/Logic -c Release --artifacts-path ../build/logic
```

The existing CI logic job runs this same suite. These assertions establish the
portable decision logic only. The fake host does not model native page loading,
actual disconnection, keyboard dispatch, tip-map updates or Undo. A changed page
label in a synthetic object is not evidence of successful cross-page navigation.
No additional application or hardware acceptance is claimed.

## Remaining scope

Live Core/Run and hardware behavior, Undo, nested/closed/locked schematic pages,
control copies, native snapshot-result coexistence, fault-injected startup and
floating command dispatch have not been application-validated. Guard logic is
covered offline where applicable; it is not presented as application evidence.
Command Palette execution was attempted, but Computer Use targeting reactivated
the main window and dismissed the overlay before search. Menu and physical-key
execution were verified; palette execution remains a follow-up check.

Raw captures, vendor binaries, build logs and operation receipts remain local.
Only implementation, contracts and this scoped acceptance record are published.

The final candidate (DLL SHA-256
aea0c195aac7049565f88556bcab347f4fb51373009c35f2180dad299ed290d1)
was restarted and rechecked: Find Focused highlighted the attached injector;
Return Home removed its tip and highlight, restored the panel icon and selected
the owning schematic component.
