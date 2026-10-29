using Designer.Modules.BetterProbesInjectors;

int checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
ToolItem Tool(string id, ToolKind kind = ToolKind.Probe, string name = "Same", string page = "Page A", bool attached = false) =>
    new(id, kind, name, page, new object(), attached ? new(new object(), new object(), new object(), "signal-1", "Page B / gain / input") : null);

var a = Tool("a", attached: true); var b = Tool("b"); var c = Tool("c", page: "Page B"); var i = Tool("i", ToolKind.Injector, attached: true);
var selection = new object(); var doc = new object();
Workspace Baseline() => new(doc, [c, i, b, a], [selection], "a", Connected: true);
var host = new FakeHost(Baseline()); var nav = new ToolNavigation(host);
Check(ToolNavigation.Ordered(host.State, ToolKind.Probe).Select(t => t.Id).SequenceEqual(["a", "b", "c"]), "Stable page/name/id order handles duplicate names");
Check(nav.Cycle(ToolKind.Probe, false).Succeeded && host.Last?.Tool.Id == "b", "Next probe follows the focused tool");
host.State = host.State with { FocusedId = "c" };
nav.Cycle(ToolKind.Probe, false); Check(host.Last?.Tool.Id == "a", "Next wraps at the end");
host.State = host.State with { FocusedId = "a" };
nav.Cycle(ToolKind.Probe, true); Check(host.Last?.Tool.Id == "c", "Previous wraps at the start");
nav.Cycle(ToolKind.Injector, false); Check(host.Last?.Tool.Id == "i", "Cycling injectors never selects a probe");
Check(host.Returns == 0 && host.Records == 0, "Cycling and navigation do not detach or write operation records");
host.State = host.State with { FocusedId = null };
nav.Cycle(ToolKind.Probe, false); Check(host.Last?.Tool.Id == "a", "Cycle cursor survives unrelated selection without granting Return Home a target");
Check(nav.Plan(ToolAction.ReturnHome) is null, "Remembered cycle position cannot become a destructive fallback");
host.State = host.State with { Document = new object() };
nav.Cycle(ToolKind.Probe, true); Check(host.Last?.Tool.Id == "c", "Document switch resets the cycle cursor");
host.State = host.State with { Tools = [] };
Check(!nav.CanCycle(ToolKind.Probe) && !nav.Cycle(ToolKind.Probe, false).Succeeded, "Empty family is unavailable");
host.State = Baseline() with { Tools = [a, a with { Component = new object() }] };
Check(!nav.CanCycle(ToolKind.Probe) && nav.Plan(ToolAction.ReturnHome) is null, "Duplicate stable identities are rejected");

foreach (var blocked in new[] { Baseline() with { Placing = true }, Baseline() with { Busy = true }, Baseline() with { InteractionAllowed = false } })
{
    host.State = blocked;
    Check(nav.Plan(ToolAction.Locate) is null && nav.Plan(ToolAction.ReturnHome) is null && !nav.Cycle(ToolKind.Probe, false).Succeeded,
        "Busy, placement and editor exclusion reject both navigation and detachment");
}
host.State = Baseline() with { Selection = [selection, new object()] };
Check(nav.Plan(ToolAction.ReturnHome) is null, "Multiple selected objects cannot choose an arbitrary tool");
host.State = Baseline() with { Selection = [] };
Check(nav.Plan(ToolAction.ReturnHome) is null, "Empty selection cannot choose an arbitrary tool");
host.State = Baseline() with { Connected = false };
Check(nav.Plan(ToolAction.ReturnHome) is null && nav.Plan(ToolAction.HomeComponent) is not null, "Disconnected mode permits owner navigation but never detachment");
host.State = Baseline() with { Tools = [a with { StateKnown = false }] };
Check(nav.Plan(ToolAction.Locate) is null && nav.Plan(ToolAction.ReturnHome) is null && nav.Plan(ToolAction.HomeComponent) is not null,
    "Uncertain attachment only permits locating the owning component");
Check(nav.Cycle(ToolKind.Probe, false).Succeeded && host.Last?.Home == true, "Unknown attachment cycles to its owner safely");
host.State = Baseline() with { FocusedId = "b" };
int oldReturns = host.Returns;
Check(nav.Execute(nav.Plan(ToolAction.ReturnHome)!).Succeeded && host.Returns == oldReturns && host.Records == 0,
    "Already-home action is an idempotent no-op");
nav.Execute(nav.Plan(ToolAction.Locate)!); Check(host.Last?.Home == true && host.Last?.Tool.Id == "b", "Unattached tool locates its own component");
host.State = Baseline();
nav.Execute(nav.Plan(ToolAction.HomeComponent)!); Check(host.Last?.Home == true && host.Last?.Tool.Id == "a", "Home-component navigation never detaches an attached tool");
nav.Execute(nav.Plan(ToolAction.Locate)!); Check(host.Last?.Home == false && host.Last?.Tool.Id == "a", "Attached tool navigation uses its actual attachment");

var mutations = new (string Name, Func<Workspace, Workspace> Apply)[]
{
    ("document switched", s => s with { Document = new object() }),
    ("selection changed", s => s with { Selection = [new object()] }),
    ("focused control retargeted", s => s with { FocusedId = "i" }),
    ("selection cleared", s => s with { Selection = [] }),
    ("selection expanded", s => s with { Selection = [selection, new object()] }),
    ("tool deleted", s => s with { Tools = [b, c, i] }),
    ("identity reused", s => s with { Tools = [a with { Component = new object() }] }),
    ("target moved", s => s with { Tools = [a with { Attachment = a.Attachment! with { Pin = new object() } }] }),
    ("control replaced", s => s with { Tools = [a with { Attachment = a.Attachment! with { Control = new object() } }] }),
    ("live value replaced", s => s with { Tools = [a with { Attachment = a.Attachment! with { Value = new object() } }] }),
    ("signal changed", s => s with { Tools = [a with { Attachment = a.Attachment! with { Address = "signal-2" } }] }),
    ("tool returned elsewhere", s => s with { Tools = [a with { Attachment = null }] }),
    ("connection lost", s => s with { Connected = false }),
    ("connection re-established", s => s with { ConnectionEpoch = 2 }),
    ("editor now active", s => s with { InteractionAllowed = false }),
    ("placement started", s => s with { Placing = true }),
    ("host became busy", s => s with { Busy = true }),
    ("state became unknown", s => s with { Tools = [a with { StateKnown = false }] })
};
foreach (var (name, mutate) in mutations)
{
    host.State = Baseline(); var plan = nav.Plan(ToolAction.ReturnHome)!;
    host.State = mutate(host.State); oldReturns = host.Returns;
    Check(!nav.Execute(plan).Succeeded && host.Returns == oldReturns, "Stale return plan rejects: " + name);
}
host.State = Baseline(); var staleFind = nav.Plan(ToolAction.Locate, true)!;
host.State = host.State with { Tools = [a with { Attachment = a.Attachment! with { Pin = new object() } }] };
int oldNavigations = host.Navigations;
Check(!nav.Execute(staleFind).Succeeded && host.Navigations == oldNavigations, "Old native Find row cannot navigate a changed attachment");
Check(host.CaptureFromFind, "Find callbacks retain their explicit context route");

host.State = Baseline(); host.OnRecord = () => host.State = host.State with { Document = new object() };
oldReturns = host.Returns;
Check(!nav.Execute(nav.Plan(ToolAction.ReturnHome)!).Succeeded && host.Returns == oldReturns, "Recheck after operation record rejects a changed document");
host.State = Baseline(); host.OnRecord = () => throw new IOException("record unavailable");
try { nav.Execute(nav.Plan(ToolAction.ReturnHome)!); throw new Exception("Expected receipt failure"); }
catch (IOException) { Check(host.Returns == oldReturns, "Receipt failure prevents mutation"); }
host.OnRecord = null; host.State = Baseline(); host.Events.Clear();
Check(nav.Execute(nav.Plan(ToolAction.ReturnHome)!).Succeeded && host.Returns == oldReturns + 1 && host.ReturnedId == "a",
    "Valid return invokes exactly the chosen tool once");
Check(host.Events.SequenceEqual(["capture", "capture", "record", "capture", "return"]), "Plan, execution and final recheck precede native detach");
Check(host.State.Tools.Single(t => t.Id == "i").Attachment is not null, "Returning a probe leaves the injector attached");
host.State = host.State with { FocusedId = "i" };
Check(nav.Execute(nav.Plan(ToolAction.ReturnHome)!).Succeeded && host.ReturnedId == "i", "The same guarded path supports injectors");
Check(ToolNavigation.CanCompleteNavigation(Baseline(), Baseline() with { Selection = [new object()] }, a),
    "Native provider selection may change while locating an attached tip");
host.State = Baseline() with { Tools = [a with { Attachment = a.Attachment! with { Address = "" } }] };
Check(nav.Plan(ToolAction.ReturnHome) is not null, "A verified tip on an unwired native net remains a returnable attachment");
foreach (var (name, mutate) in mutations.Where(m => m.Name is not ("selection changed" or "focused control retargeted" or "selection cleared" or "selection expanded")))
    Check(!ToolNavigation.CanCompleteNavigation(Baseline(), mutate(Baseline()), a), "Deferred tip selection rejects: " + name);
LifecycleChecks.Run(Check);
CanvasContextChecks.Run(Check);
Console.WriteLine($"{checks} logic checks passed; no Designer, UI or hardware was used.");

internal sealed class FakeHost(Workspace state) : IToolHost
{
    internal Workspace State = state;
    internal (ToolItem Tool, bool Home)? Last;
    internal int Returns, Navigations, Records;
    internal string? ReturnedId;
    internal Action? OnRecord;
    internal Action? OnCapture;
    internal Action? OnNavigate;
    internal bool CaptureFromFind;
    internal readonly List<string> Events = [];
    public Workspace Capture(bool fromFind = false) { CaptureFromFind = fromFind; Events.Add("capture"); OnCapture?.Invoke(); return State; }
    public void Navigate(ToolItem tool, bool homeComponent) { OnNavigate?.Invoke(); Navigations++; Last = (tool, homeComponent); }
    public void RecordReturn(ToolItem tool) { Records++; Events.Add("record"); OnRecord?.Invoke(); }
    public void ReturnHome(ToolItem tool)
    {
        Returns++; ReturnedId = tool.Id; Events.Add("return");
        State = State with { Tools = State.Tools.Select(t => t.Id == tool.Id ? t with { Attachment = null } : t).ToArray() };
    }
}
