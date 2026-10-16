using System.Collections;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using HarmonyLib;
using QsysMods.Core;

namespace Designer.Modules.BetterProbesInjectors;

internal sealed class NativeFindIntegration : IDisposable
{
    private static NativeFindIntegration? active;
    private readonly DesignerHost host;
    private readonly ToolNavigation navigation;
    private readonly Dispatcher dispatcher;
    private readonly Harmony harmony = new("joshua-small.better-probes-injectors.find");
    private readonly List<WeakReference<object>> areas = [];
    private bool refreshQueued;
    private bool disposed;
    private bool reportedFailure;

    internal NativeFindIntegration(DesignerHost host, ToolNavigation navigation, Dispatcher dispatcher)
    { this.host = host; this.navigation = navigation; this.dispatcher = dispatcher; }

    internal void Install()
    {
        if (active is not null) throw new InvalidOperationException("Probe Find integration is already installed.");
        try
        {
            // Validate the complete patch set before adding the first hook.
            var selectionChanged = DesignerHost.Method(host.FindAreaType, "Global_SelectionChanged", Host.Instance,
                Host.Type("QSC.QSys.Util.Selection"));
            var addTip = DesignerHost.Method(host.TipManagerType, "AddTip", Host.Static,
                Host.Type("QSC.QSys.Graphic.Pin"), Host.Type("QSC.QSys.Graphic.GraphicCanvas"), Host.Type("QSC.QSys.Design.ControlStyle"));
            var removeTip = DesignerHost.Method(host.TipManagerType, "RemoveTip", Host.Static, Host.Type("QSC.QSys.Design.ControlValue"));
            _ = DesignerHost.RequiredField(host.FindAreaType, "_Choices", Host.Instance);
            _ = host.FindChoiceType.GetConstructor(Type.EmptyTypes) ?? throw new MissingMethodException("FindChoice constructor");
            foreach (string name in new[] { "Index", "Label", "IsEnabled", "Run", "Tag" })
                if (host.FindChoiceType.GetProperty(name, Host.Instance)?.SetMethod is null) throw new MissingMemberException("FindChoice", name);
            active = this;
            harmony.Patch(selectionChanged, postfix: new HarmonyMethod(typeof(NativeFindIntegration), nameof(AfterSelection)));
            harmony.Patch(addTip, postfix: new HarmonyMethod(typeof(NativeFindIntegration), nameof(AfterAttachment)));
            harmony.Patch(removeTip, postfix: new HarmonyMethod(typeof(NativeFindIntegration), nameof(AfterAttachment)));
            // Include a Find panel that was already created before Core finished loading mods.
            var window = DesignerHost.Static("QSC.QSys.Commands.Find", "_ToolWindow");
            if (DesignerHost.Get(window, "Content") is { } area && host.FindAreaType.IsInstanceOfType(area))
            { Track(area); host.RefreshFind(area); }
        }
        catch { Dispose(); throw; }
    }

    private static void AfterSelection(object __instance) => active?.Append(__instance);
    private static void AfterAttachment() => active?.QueueRefresh();

    private void Track(object area)
    {
        areas.RemoveAll(a => !a.TryGetTarget(out _));
        if (!areas.Any(a => a.TryGetTarget(out var existing) && ReferenceEquals(existing, area))) areas.Add(new(area));
    }

    private void Append(object area)
    {
        if (disposed || !Host.Enabled) return;
        IList? choices = null;
        object? label1 = null, label2 = null;
        try
        {
            dispatcher.VerifyAccess(); Track(area);
            if (area is not UIElement { IsVisible: true } || DesignerHost.Get(DesignerHost.Get(area, "Spec"), "FindType")?.ToString() != "Context") return;
            var state = host.Capture(true);
            var tool = ToolNavigation.Focused(state);
            if (tool is null || ToolNavigation.Unavailable(state) is not null) return;
            choices = (IList)DesignerHost.Get(area, "_Choices")!;
            label1 = DesignerHost.Get(area, "Label1"); label2 = DesignerHost.Get(area, "Label2");
            // A native refresh normally clears the list; removing only our own markers makes repeated calls idempotent.
            for (int i = choices.Count - 1; i >= 0; i--)
                if (DesignerHost.Get(choices[i], "Tag") is FindAction) choices.RemoveAt(i);
            var additions = new List<object>();
            if (tool.StateKnown && tool.Attachment is { } attachment)
                additions.Add(CreateChoice(choices.Count + 1, area, new(state, tool, ToolAction.Locate, true), $"Find attached {tool.Kind.ToString().ToLowerInvariant()}: {tool.Name} — {attachment.Description}"));
            additions.Add(CreateChoice(choices.Count + additions.Count + 1, area, new(state, tool, ToolAction.HomeComponent, true), $"Find {tool.Kind.ToString().ToLowerInvariant()} home component: {tool.Name}"));
            foreach (var choice in additions) choices.Add(choice);
            var family = ToolNavigation.Ordered(state, tool.Kind);
            int index = Array.FindIndex(family, item => item.Id == tool.Id);
            DesignerHost.Set(area, "Label1", $"{tool.Kind} {index + 1} of {family.Length}: {tool.Name}");
            DesignerHost.Set(area, "Label2", tool.StateKnown
                ? tool.Attachment?.Description ?? "At home — no attached tip" : "Attachment state unavailable; home component can still be located");
        }
        catch (Exception error)
        {
            try
            {
                if (choices is not null)
                {
                    for (int i = choices.Count - 1; i >= 0; i--)
                        if (DesignerHost.Get(choices[i], "Tag") is FindAction) choices.RemoveAt(i);
                    DesignerHost.Set(area, "Label1", label1); DesignerHost.Set(area, "Label2", label2);
                }
            }
            catch (Exception cleanupError) { Host.Error(ProbeModule.Id, cleanupError); }
            ReportOnce(error);
        }
    }

    private object CreateChoice(int index, object area, ToolPlan plan, string label)
    {
        var choice = Activator.CreateInstance(host.FindChoiceType)!;
        var action = new FindAction(this, area, plan);
        DesignerHost.Set(choice, "Index", index);
        DesignerHost.Set(choice, "Label", label);
        DesignerHost.Set(choice, "IsEnabled", true);
        DesignerHost.Set(choice, "Tag", action);
        var run = host.FindChoiceType.GetProperty("Run")!;
        run.SetValue(choice, Delegate.CreateDelegate(run.PropertyType, action, typeof(FindAction).GetMethod(nameof(FindAction.Run))!));
        return choice;
    }

    private sealed class FindAction(NativeFindIntegration owner, object area, ToolPlan plan)
    {
        public void Run(object sender, object? selected)
        {
            if (owner.disposed || area is not UIElement { IsVisible: true }
                || DesignerHost.Get(DesignerHost.Get(area, "Spec"), "FindType")?.ToString() != "Context"
                || plan.Before.Selection.Count != 1 || !ReferenceEquals(selected, plan.Before.Selection[0])) return;
            try
            {
                var result = owner.navigation.Execute(plan);
                if (!result.Succeeded) DesignerHost.Set(area, "Label2", result.Message);
            }
            catch (Exception error) { owner.ReportOnce(error); }
        }
    }

    internal void QueueRefresh()
    {
        if (disposed || refreshQueued) return;
        refreshQueued = true;
        dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            refreshQueued = false;
            if (disposed) return;
            try
            {
                // Re-capture current state after the native attachment operation finishes.
                foreach (var reference in areas.ToArray()) if (reference.TryGetTarget(out var area)) host.RefreshFind(area);
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception error) { ReportOnce(error); }
        }));
    }

    private void ReportOnce(Exception error)
    {
        if (reportedFailure) return;
        reportedFailure = true;
        Host.Error(ProbeModule.Id, error);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (ReferenceEquals(active, this)) active = null;
        harmony.UnpatchAll(harmony.Id);
        foreach (var reference in areas)
            try
            {
                if (reference.TryGetTarget(out var area) && DesignerHost.Get(area, "_Choices") is IList choices)
                    for (int i = choices.Count - 1; i >= 0; i--)
                        if (DesignerHost.Get(choices[i], "Tag") is FindAction) choices.RemoveAt(i);
            }
            catch (Exception cleanupError) { Host.Error(ProbeModule.Id, cleanupError); }
        areas.Clear();
    }
}
