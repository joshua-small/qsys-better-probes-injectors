using System.Windows;
using System.Windows.Input;
using QsysMods.Core;

namespace Designer.Modules.BetterProbesInjectors;

public sealed class ProbeModule : ModModule
{
    internal const string Id = "better-probes-injectors";
    internal const string Name = "Better Probes & Injectors";
    private DesignerHost? host;
    private NativeFindIntegration? integration;

    public ProbeModule() => Mods.Register(Id, main =>
    {
        var coreVersion = typeof(Mods).Assembly.GetName().Version!;
        if (coreVersion < new Version(0, 1, 6, 0) || coreVersion >= new Version(0, 2, 0, 0))
            throw new InvalidOperationException("Better Probes & Injectors requires Mods Core 0.1.6 or newer within 0.1.x.");
        try
        {
            host = new DesignerHost(main);
            var navigation = new ToolNavigation(host);
            integration = new(host, navigation, main.Dispatcher);
            integration.Install();
            foreach (var kind in new[] { ToolKind.Probe, ToolKind.Injector })
                foreach (bool backwards in new[] { false, true })
                {
                    var capturedKind = kind; bool previous = backwards;
                    Register($"{(previous ? "previous" : "next")}-{kind.ToString().ToLowerInvariant()}",
                        $"{(previous ? "Previous" : "Next")} {kind}",
                        () => Run(navigation.Cycle(capturedKind, previous)), () => navigation.CanCycle(capturedKind),
                        "Current document; schematic or component canvas; no placement cursor");
                }
            foreach (var (id, title, action) in new[]
            {
                ("find-focused", "Find Focused Probe/Injector", ToolAction.Locate),
                ("find-home", "Find Probe/Injector Home Component", ToolAction.HomeComponent),
                ("return-home", "Return Focused Probe/Injector Home", ToolAction.ReturnHome)
            })
            {
                var captured = action;
                Register(id, title, () =>
                {
                    if (navigation.Plan(captured) is { } plan) Run(navigation.Execute(plan));
                }, () => navigation.Plan(captured) is not null,
                    action == ToolAction.ReturnHome ? "One focused tool; connected Emulate/Run; no placement cursor"
                    : "One focused tool; schematic or component canvas; no placement cursor");
            }
            main.Closed += Closed;
            Host.Receipt(Id, "initialized", new { version = typeof(ProbeModule).Assembly.GetName().Version?.ToString(),
                core = coreVersion.ToString(), nativeFind = "hook registered", acceptance = "not established by initialization" });
        }
        catch { integration?.Dispose(); host?.Dispose(); throw; }
    });

    private static void Register(string id, string name, Action run, Func<bool> canRun, string when) =>
        Commands.Register(new(Id + "/" + id, name, Name, run, () => Host.Enabled && canRun(), InMenu: true)
        { When = when, ShortcutContext = () => !ControlInput.IsTextInput() });

    private void Run(Outcome result)
    {
        // Keep routine navigation nonmodal. Native Find displays the target and its attachment information.
        Host.Receipt(Id, result.Succeeded ? "action-requested" : "action-canceled", new { result.Message });
        integration?.QueueRefresh();
        CommandManager.InvalidateRequerySuggested();
    }

    private void Closed(object? sender, EventArgs e)
    {
        if (sender is Window window) window.Closed -= Closed;
        integration?.Dispose(); host?.Dispose();
    }
}
