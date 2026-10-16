namespace Designer.Modules.BetterProbesInjectors;

internal enum ToolKind { Probe, Injector }
internal enum ToolAction { Locate, HomeComponent, ReturnHome }
internal sealed record Attachment(object Pin, object Control, object Value, string Address, string Description);
internal sealed record ToolItem(string Id, ToolKind Kind, string Name, string Page, object Component,
    Attachment? Attachment = null, bool StateKnown = true);
internal sealed record Workspace(object Document, IReadOnlyList<ToolItem> Tools, IReadOnlyList<object> Selection,
    string? FocusedId, bool Connected = false, bool InteractionAllowed = true, bool Busy = false, bool Placing = false,
    long ConnectionEpoch = 0);
internal sealed record ToolPlan(Workspace Before, ToolItem Tool, ToolAction Action, bool FromFind, bool RequiresFocus = true);
internal sealed record Outcome(bool Succeeded, string Message);

internal interface IToolHost
{
    Workspace Capture(bool fromFind = false);
    void Navigate(ToolItem tool, bool homeComponent);
    void RecordReturn(ToolItem tool);
    void ReturnHome(ToolItem tool);
}

// No Designer/WPF dependency: selection and attachment checks are exercised with an adversarial fake host.
internal sealed class ToolNavigation(IToolHost host)
{
    private object? cycleDocument;
    private readonly Dictionary<ToolKind, string> last = [];

    internal static ToolItem[] Ordered(Workspace state, ToolKind kind) => state.Tools
        .Where(t => t.Kind == kind).GroupBy(t => t.Id, StringComparer.Ordinal)
        .Where(g => g.Count() == 1).Select(g => g.Single())
        .OrderBy(t => t.Page, StringComparer.OrdinalIgnoreCase)
        .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.Id, StringComparer.Ordinal).ToArray();

    internal static string? Unavailable(Workspace state) => !state.InteractionAllowed ? "Focus a schematic or the native Find panel."
        : state.Busy ? "Designer is busy." : state.Placing ? "Finish or cancel the active placement first." : null;

    internal static ToolItem? Focused(Workspace state) => state.Selection.Count == 1 && state.FocusedId is { } id
        ? state.Tools.SingleOrDefaultSafe(t => t.Id == id) : null;

    internal ToolPlan? Plan(ToolAction action, bool fromFind = false)
    {
        var state = host.Capture(fromFind);
        var tool = Focused(state);
        return Unavailable(state) is null && tool is not null && CanUse(state, tool, action)
            ? new(state, tool, action, fromFind) : null;
    }

    internal bool CanCycle(ToolKind kind)
    {
        var state = host.Capture();
        return Unavailable(state) is null && Ordered(state, kind).Length > 0;
    }

    internal Outcome Cycle(ToolKind kind, bool backwards)
    {
        var state = host.Capture();
        if (Unavailable(state) is { } reason) return new(false, reason);
        if (!ReferenceEquals(cycleDocument, state.Document)) { last.Clear(); cycleDocument = state.Document; }
        var ordered = Ordered(state, kind);
        if (ordered.Length == 0) return new(false, $"No {kind.ToString().ToLowerInvariant()}s in this document.");
        var focused = Focused(state);
        var current = focused?.Kind == kind ? focused.Id : last.GetValueOrDefault(kind);
        int index = Array.FindIndex(ordered, t => t.Id == current);
        index = index < 0 ? (backwards ? ordered.Length - 1 : 0)
            : (index + (backwards ? -1 : 1) + ordered.Length) % ordered.Length;
        var target = ordered[index];
        // Unknown attachment state still permits navigation to the owning schematic component.
        var action = target.StateKnown ? ToolAction.Locate : ToolAction.HomeComponent;
        var result = Execute(new(state, target, action, false, RequiresFocus: false));
        if (result.Succeeded) last[kind] = target.Id;
        return result.Succeeded ? result with { Message = $"{kind} {index + 1} of {ordered.Length}: {result.Message}" } : result;
    }

    internal Outcome Execute(ToolPlan plan)
    {
        var now = host.Capture(plan.FromFind);
        if (!Valid(plan, now, out var tool)) return new(false, "The document, selection or tool changed. Choose the action again.");
        if (plan.Action == ToolAction.ReturnHome)
        {
            if (tool!.Attachment is null) return new(true, $"{tool.Name} is already home.");
            host.RecordReturn(tool); // Failure to preserve the operation record must prevent detachment.
            now = host.Capture(plan.FromFind);
            if (!Valid(plan, now, out tool)) return new(false, "The tool changed before detaching. Nothing was returned home.");
            host.ReturnHome(tool!);
            return new(true, $"Returned {tool!.Name} home.");
        }
        host.Navigate(tool!, plan.Action == ToolAction.HomeComponent || tool!.Attachment is null);
        return new(true, tool!.Attachment is null || plan.Action == ToolAction.HomeComponent
            ? $"{tool.Name} — home component" : $"{tool.Name} — {tool.Attachment.Description}");
    }

    private static bool CanUse(Workspace state, ToolItem tool, ToolAction action) =>
        action == ToolAction.HomeComponent || tool.StateKnown && (action != ToolAction.ReturnHome || state.Connected);

    private static bool Valid(ToolPlan plan, Workspace now, out ToolItem? tool)
    {
        tool = now.Tools.SingleOrDefaultSafe(t => t.Id == plan.Tool.Id && ReferenceEquals(t.Component, plan.Tool.Component));
        return tool is not null && ReferenceEquals(plan.Before.Document, now.Document) && Unavailable(now) is null
            && plan.Before.Connected == now.Connected && plan.Before.ConnectionEpoch == now.ConnectionEpoch
            && (!plan.RequiresFocus || ReferenceEquals(Focused(now)?.Component, tool.Component))
            && CanUse(now, tool, plan.Action)
            && SameSelection(plan.Before.Selection, now.Selection)
            && (plan.Action == ToolAction.HomeComponent || SameAttachment(plan.Tool.Attachment, tool.Attachment));
    }

    internal static bool SameSelection(IReadOnlyList<object> a, IReadOnlyList<object> b) => a.Count == b.Count
        && a.All(item => b.Any(other => ReferenceEquals(item, other)));
    private static bool SameAttachment(Attachment? a, Attachment? b) => a is null ? b is null : b is not null
        && ReferenceEquals(a.Pin, b.Pin) && ReferenceEquals(a.Control, b.Control) && ReferenceEquals(a.Value, b.Value)
        && StringComparer.Ordinal.Equals(a.Address, b.Address);
}

internal static class UniqueMatch
{
    internal static T? SingleOrDefaultSafe<T>(this IEnumerable<T> values, Func<T, bool> predicate) where T : class
    {
        using var matches = values.Where(predicate).GetEnumerator();
        if (!matches.MoveNext()) return null;
        var first = matches.Current;
        return matches.MoveNext() ? null : first;
    }
}
