using Designer.Modules.BetterProbesInjectors;

internal static class LifecycleChecks
{
    internal static void Run(Action<bool, string> check)
    {
        CycleTransitions(check);
        ReturnTransitions(check);
    }

    private static Workspace Fixture()
    {
        ToolItem Tool(string id, ToolKind kind) => new(id, kind, "Duplicate label", "Main", new object(),
            new(new object(), new object(), new object(), id + "-signal", id + "-destination"));
        return new(new object(),
            [Tool("p4", ToolKind.Probe), Tool("i3", ToolKind.Injector),
             Tool("p2", ToolKind.Probe), Tool("i1", ToolKind.Injector),
             Tool("p3", ToolKind.Probe), Tool("i4", ToolKind.Injector),
             Tool("p1", ToolKind.Probe), Tool("i2", ToolKind.Injector)],
            [new object()], null, Connected: true);
    }

    private static void CycleTransitions(Action<bool, string> check)
    {
        foreach (var kind in new[] { ToolKind.Probe, ToolKind.Injector })
        {
            string prefix = kind == ToolKind.Probe ? "p" : "i";
            var state = Fixture();
            var host = new FakeHost(state);
            var nav = new ToolNavigation(host);
            void Cycle(bool backwards, string expected, string name)
            {
                int before = host.Navigations;
                check(nav.Cycle(kind, backwards).Succeeded && host.Navigations == before + 1 &&
                    host.Last?.Tool.Id == prefix + expected && host.Last?.Home == false,
                    $"{kind} cycle: {name}");
            }

            Cycle(false, "1", "starts at first of four duplicate labels");
            Cycle(false, "2", "moves forward without a focused tool");
            host.State = host.State with { Tools = host.State.Tools.Reverse().ToArray() };
            Cycle(false, "3", "enumeration reorder preserves stable position");
            Cycle(false, "4", "reaches fourth duplicate label");
            Cycle(false, "1", "wraps forward");
            Cycle(true, "4", "wraps backward");

            host.State = host.State with { Tools = host.State.Tools.Where(t => t.Id != prefix + "4").ToArray() };
            Cycle(false, "1", "deleted remembered tool restarts forward");
            host.State = host.State with { Tools = host.State.Tools.Where(t => t.Id != prefix + "1").ToArray() };
            Cycle(true, "3", "deleted remembered tool restarts backward");
            host.State = host.State with { Tools = host.State.Tools.Where(t => t.Kind != kind || t.Id == prefix + "2").ToArray() };
            Cycle(false, "2", "single remaining tool is selected");
            Cycle(true, "2", "single remaining tool wraps backward");
            host.State = host.State with { Tools = host.State.Tools.Where(t => t.Kind != kind).ToArray() };
            int navigations = host.Navigations;
            check(!nav.CanCycle(kind) && !nav.Cycle(kind, false).Succeeded && host.Navigations == navigations,
                $"{kind} cycle: deleting the last tool performs no navigation");
            host.State = state;
            Cycle(false, "3", "repopulated family resumes a surviving remembered identity");
            host.State = state with { FocusedId = prefix + "1" };
            Cycle(false, "2", "explicit focus takes precedence over remembered position");
            check(host.Returns == 0 && host.Records == 0 &&
                host.State.Tools.All(t => t.Attachment is not null),
                $"{kind} cycle: all transitions preserve attachments and never record a return");
            host.State = state with { Selection = [], FocusedId = null };
            check(nav.Plan(ToolAction.ReturnHome) is null,
                $"{kind} cycle: lifecycle history cannot supply a Return Home target");
        }

        var original = Fixture();
        var mixedHost = new FakeHost(original);
        var mixed = new ToolNavigation(mixedHost);
        void Step(ToolKind kind, bool backwards, string expected, string name) =>
            check(mixed.Cycle(kind, backwards).Succeeded && mixedHost.Last?.Tool.Id == expected, name);
        Step(ToolKind.Probe, false, "p1", "Interleaved cycles start the probe cursor");
        Step(ToolKind.Injector, false, "i1", "Interleaved cycles start a separate injector cursor");
        Step(ToolKind.Probe, false, "p2", "Interleaved probe cycle resumes independently");
        Step(ToolKind.Injector, true, "i4", "Interleaved injector cycle wraps independently");
        mixedHost.State = original with { Document = new object() };
        Step(ToolKind.Probe, false, "p1", "Switching documents resets the probe cursor");
        Step(ToolKind.Injector, true, "i4", "Switching documents resets the injector cursor");
        mixedHost.State = original;
        Step(ToolKind.Probe, true, "p4", "Returning to the original document starts a new cursor");

        foreach (bool throwInNavigate in new[] { false, true })
        {
            var host = new FakeHost(Fixture());
            var nav = new ToolNavigation(host);
            check(nav.Cycle(ToolKind.Injector, false).Succeeded && host.Last?.Tool.Id == "i1",
                "Failed-cycle fixture starts at the first injector");
            int navigations = host.Navigations;
            if (throwInNavigate)
            {
                host.OnNavigate = () => throw new InvalidOperationException("Synthetic navigation failure");
                bool threw = false;
                try { nav.Cycle(ToolKind.Injector, false); }
                catch (InvalidOperationException) { threw = true; }
                check(threw && host.Navigations == navigations, "Native navigation failure is observable");
                host.OnNavigate = null;
            }
            else
            {
                int captures = 0;
                host.OnCapture = () => { if (++captures == 2) host.State = host.State with { Busy = true }; };
                check(!nav.Cycle(ToolKind.Injector, false).Succeeded && host.Navigations == navigations,
                    "Context becoming busy between capture and execution rejects cycling");
                host.OnCapture = null;
                host.State = host.State with { Busy = false };
            }
            check(nav.Cycle(ToolKind.Injector, false).Succeeded && host.Last?.Tool.Id == "i2",
                "Failed cycling does not advance the remembered position");
            check(host.Returns == 0 && host.Records == 0,
                "Failed cycling cannot detach a tool or record a return");
        }
    }

    private static void ReturnTransitions(Action<bool, string> check)
    {
        foreach (var kind in new[] { ToolKind.Probe, ToolKind.Injector })
        {
            string id = kind == ToolKind.Probe ? "p1" : "i1";
            string otherId = kind == ToolKind.Probe ? "i1" : "p1";
            Workspace ChangeTool(Workspace state, Func<ToolItem, ToolItem> change) =>
                state with { Tools = state.Tools.Select(t => t.Id == id ? change(t) : t).ToArray() };
            var changes = new (string Name, Func<Workspace, Workspace> Change)[]
            {
                ("document switched", s => s with { Document = new object() }),
                ("selection cleared", s => s with { Selection = [] }),
                ("selection replaced", s => s with { Selection = [new object()] }),
                ("selection expanded", s => s with { Selection = [s.Selection[0], new object()] }),
                ("focus retargeted", s => s with { FocusedId = otherId }),
                ("tool deleted", s => s with { Tools = s.Tools.Where(t => t.Id != id).ToArray() }),
                ("component replaced", s => ChangeTool(s, t => t with { Component = new object() })),
                ("identity duplicated", s => s with { Tools = [.. s.Tools, s.Tools.Single(t => t.Id == id) with { Component = new object() }] }),
                ("attachment unknown", s => ChangeTool(s, t => t with { StateKnown = false })),
                ("pin retargeted", s => ChangeTool(s, t => t with { Attachment = t.Attachment! with { Pin = new object() } })),
                ("control replaced", s => ChangeTool(s, t => t with { Attachment = t.Attachment! with { Control = new object() } })),
                ("live value replaced", s => ChangeTool(s, t => t with { Attachment = t.Attachment! with { Value = new object() } })),
                ("signal retargeted", s => ChangeTool(s, t => t with { Attachment = t.Attachment! with { Address = "different-signal" } })),
                ("already detached", s => ChangeTool(s, t => t with { Attachment = null })),
                ("connection lost", s => s with { Connected = false }),
                ("disconnect and reconnect", s => s with { ConnectionEpoch = s.ConnectionEpoch + 1 }),
                ("editor activated", s => s with { InteractionAllowed = false }),
                ("host busy", s => s with { Busy = true }),
                ("placement started", s => s with { Placing = true })
            };
            foreach (var (name, change) in changes)
            {
                var baseline = Fixture() with { FocusedId = id };
                var host = new FakeHost(baseline);
                var nav = new ToolNavigation(host);
                var plan = nav.Plan(ToolAction.ReturnHome)!;
                host.OnRecord = () => host.State = change(host.State);
                check(!nav.Execute(plan).Succeeded && host.Records == 1 && host.Returns == 0 && host.Navigations == 0,
                    $"{kind} return after receipt rejects: {name}");
                check(ReferenceEquals(host.State.Tools.Single(t => t.Id == otherId).Attachment,
                    baseline.Tools.Single(t => t.Id == otherId).Attachment),
                    $"{kind} rejected return preserves other family: {name}");
            }

            var stable = Fixture() with { FocusedId = id };
            var acceptedHost = new FakeHost(stable);
            var accepted = new ToolNavigation(acceptedHost);
            acceptedHost.OnRecord = () => acceptedHost.State = acceptedHost.State with
            {
                Tools = acceptedHost.State.Tools.Select(t => t with { Name = "Renamed", Page = "Other page" }).Reverse().ToArray()
            };
            check(accepted.Execute(accepted.Plan(ToolAction.ReturnHome)!).Succeeded &&
                acceptedHost.Returns == 1 && acceptedHost.Records == 1 && acceptedHost.ReturnedId == id,
                $"{kind} return permits label, page-label and enumeration changes with stable target identity");
            check(acceptedHost.State.Tools.Where(t => t.Id != id).All(t => t.Attachment is not null),
                $"{kind} successful return preserves all seven other attachments");
            check(accepted.Execute(accepted.Plan(ToolAction.ReturnHome)!).Succeeded &&
                acceptedHost.Returns == 1 && acceptedHost.Records == 1,
                $"{kind} repeated Return Home is an idempotent no-op");
        }
    }
}
