# Native implementation contract

These findings are scoped to Designer 10.5.0 (0-2609.007 10.5), statically
inspected from a preserved local copy. Source and copied Designer DLL hashes
matched. No original design was read or changed for this implementation.

| Assembly | SHA-256 |
| --- | --- |
| Q-Sys Designer.dll | `0e7150f8dc02e0159e019188780fb003ce025f5ec18c491609cf337d849bda60` |
| ModuleCommon.dll | `a6d1addda06bdcb1ad32b57c7480c947210c3d3ad287f5818c2ea64068bf5aee` |

## Documented behavior

The [Signal Probe](https://help.qsys.com/Content/Schematic_Library/probe.htm)
and [Signal Injector](https://help.qsys.com/Content/Schematic_Library/injector.htm)
Help pages, inspected with footer 10.5.0, describe attaching the tool by clicking
its control and an audio connector. Picking it up and clicking blank schematic
space or its control panel removes/moves it. The documented control-pin lists
contain Gain, Mute and Valid, without a return-home pin or shortcut.

An injector affects its attached audio path. Calling the native detach behavior
is therefore distinct from navigation or graphic removal alone.

## Observed private routes

`FindArea.Global_SelectionChanged` builds Context results from native factory
methods discovered in the Designer assembly. `ComponentChoices.Get` adds snapshot
destinations, without a deployed-tip lookup. This mod appends results after the
native selection handler, using native `FindChoice` objects and its `RunHandler`
delegate. It does not replace the panel or install another Ctrl+F handler.

`ProbeTipManager._ActiveProbes` maps design pins to control styles;
`_ActiveTips` maps control values to graphic tips. Both tool types use these maps.
`Graphic.Controls.Injector` derives from `Probe`. `ComponentCache.GetComponent`
resolves each control style's component identity. `Manager.GetElementsOfType`
provides the current manager's attached elements; component GUIDs come from
`UniqueId.RawString`. Labels are only display/sort values.

`Util.Find.FindAndSelect(Design.Object)` and its `Design.Pin` overload own page
navigation and highlighting. The pin route may select a native pin label. Both
are asynchronous native operations; requesting navigation is not evidence that
a particular page or container was successfully revealed.

`ProbeTool.ClearTip(ControlStyle)` removes the active graphic and clears the
control value's `StringNext`. `ProbeCommand.OnLeftMouseDown` uses it when dropping
away from an audio pin. It calls `RemoveTip` before checking for a null control
value, so the mod requires a verified live value and consistent mapping first.
It also verifies the cleared local value and removed map entry afterward. No
undo transaction is invented for this live-control action.

## Guarded assumptions and limits

- The current document, component object and GUID, focused owner, raw selection,
  connected state, connection epoch, attachment pin/style/value and attachment
  address must still match when a pending action executes. Return Home checks
  again after saving its operation record and before the native call.
- A dictionary miss is insufficient to call the tool home: an available,
  non-orphaned control must report both current and pending attachment strings
  empty. Unloaded/unresolved controls remain unknown and only owner navigation
  is offered.
- A supplied tip graphic, its control and design pin must agree. A stale,
  ambiguous or foreign-document attachment cannot be returned home. Navigation
  callbacks are checked against the selected tool, not an arbitrary first entry.
- Active placement, text/script input, busy document operations and multi-selection
  exclude commands. Floating command dispatch is deferred; native Find callbacks
  may come from its own visible native panel.
- Harmony ownership is `joshua-small.better-probes-injectors.find`. Only the
  native selection handler and attachment add/remove methods are patched.
  Attachment refresh is queued after native work; native search results are
  preserved. Initialization failure removes the mod's hooks.
- Exact metadata checks validate member shapes and assembly identities, not
  reflection execution, runtime attachment restoration, UI appearance, closed-page
  reconstruction, encrypted container navigation or hardware behavior.

Vendor binaries and raw decompilation remain outside this source repository.
