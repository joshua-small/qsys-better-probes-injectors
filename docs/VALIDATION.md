# Validation and deferred application checks

The owner authorized implementation while deferring anything requiring Computer
Use. This milestone uses no Computer Use, launches no Designer instance, changes
no installed module or user preferences, and touches no original design or Core.

## Offline verification

`scripts/check.sh` builds Release output with warnings treated as errors and runs:

- `tests/Logic`: stable typed cycles, wrapping, duplicate labels/IDs, unattached
  and unknown state, selection exclusions, stale document/identity/attachment
  rejection, retargeting, reconnect invalidation, operation-record failures,
  exactly-one native detach request and preservation of other tools. This uses
  a fake host; it does not exercise Designer.
- `tests/HostContract`: SHA-256 guards, reflected member/signature shapes,
  attachment dictionary types, native Find callback shape, injector inheritance,
  status notifications, mod assembly identity/version and references. This reads
  PE metadata without loading or executing vendor assemblies.

Build/test logs and the candidate payload hash are retained alongside the local
build. The selected issue records the final counts and source commit. Successful
compilation and metadata checks do not establish Harmony or application acceptance.

## Required next application checks — deferred

Use a disposable supported Designer copy and a synthetic design after the owner
resumes application validation. Keep normal installation and publishing separate.

1. Verify normal module startup, exact host/Core guards, hook registration,
   duplicate-load handling and clean shutdown. Verify initialization failure
   removes all mod hooks and leaves native Find usable.
2. Attach a probe and an injector, select each owning component and open native
   Find in Context mode. Check the new destinations, counts, names and attachment
   locations alongside existing snapshot/control-copy results. Reopen/float Find,
   change selection and update attachments without duplicate or stale rows.
3. Exercise same-page, other-page and nested schematic destinations, pin labels,
   closed control panels, locked/password-protected containers, identical labels,
   copied controls and previously unloaded target pages. Verify actual highlight
   and selected object, not merely a returned native call.
4. Test both cycle directions independently for each type, wrapping, empty lists,
   single tools, unattached and unknown tools. Verify navigation changes no signal
   attachment, gain or mute value.
5. In eligible Emulate mode, return exactly one focused tool home. Verify its
   graphic/control-panel appearance, attachment value and other tools' unchanged
   state. Observe native Undo behavior without assuming it is available. Run-mode
   and hardware acceptance remain separate and require an appropriate test system.
6. Exercise command-menu clicks, custom keys through Better Shortcuts and Command
   Palette execution/focus restoration. Check stale-selection/retargeting/deletion,
   disconnect/reconnect, placement cursors, ordinary text, embedded script/browser
   controls, main/floating windows and UI transitions. Floating command dispatch
   is currently deliberately excluded, not implicitly accepted.
7. Verify the expected native result after return-home errors and log failures.
   Do not blindly retry an uncertain detach. Hash-check the preserved originals.

Default key selection, catalog onboarding, installable package validation,
publication and installation are future delivery steps. The current issue remains
open until the applicable acceptance and agreed delivery scope are fulfilled.
