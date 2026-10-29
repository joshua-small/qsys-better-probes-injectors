using Designer.Modules.BetterProbesInjectors;

internal static class CanvasContextChecks
{
    internal static void Run(Action<bool, string> check)
    {
        bool Supported(object? value) => CanvasContext.IsSupported(value, typeof(GraphicCanvas), typeof(UciPage),
            canvas => ((GraphicCanvas)canvas).Root);
        check(!Supported(null), "No canvas means no schematic command context");
        check(!CanvasContext.IsSupported(new object(), typeof(GraphicCanvas), typeof(UciPage),
            _ => throw new Exception("A non-canvas must not read Root")), "Unrelated focus does not access a canvas root");
        check(Supported(new GraphicCanvas(new Schematic())), "Schematic root retains navigation");
        check(Supported(new GraphicCanvas(new ComponentPanel())), "Component control panel retains navigation");
        check(Supported(new GraphicCanvas(null)), "Null root retains the existing GraphicCanvas classification");
        check(Supported(new DerivedCanvas(new Schematic())), "Derived graphic canvas retains schematic navigation");
        check(Supported(new GraphicCanvas(new Page())), "An unrelated type named Page is not mistaken for a UCI page");

        foreach (var canvas in new GraphicCanvas[]
        {
            new(new UciPage()), new(new DerivedUciPage()), new(new FurtherDerivedUciPage()),
            new DerivedCanvas(new DerivedUciPage())
        })
        {
            string name = canvas.GetType().Name + "/" + canvas.Root!.GetType().Name;
            check(!Supported(canvas), name + " is excluded by runtime assignability");

            // Feed the classification into the real navigation guard. This does not emulate WPF focus delivery.
            var tool = new ToolItem("probe", ToolKind.Probe, "Probe", "Main", new object(),
                new(new object(), new object(), new object(), "signal", "target"));
            var host = new FakeHost(new(new object(), [tool], [new object()], tool.Id,
                Connected: true, InteractionAllowed: Supported(canvas)));
            var navigation = new ToolNavigation(host);
            check(!navigation.CanCycle(ToolKind.Probe) && !navigation.Cycle(ToolKind.Probe, false).Succeeded,
                name + " prevents cycling a retained schematic selection");
            foreach (var action in new[] { ToolAction.Locate, ToolAction.HomeComponent, ToolAction.ReturnHome })
                check(navigation.Plan(action) is null, name + " prevents " + action);
            check(host.Navigations == 0 && host.Records == 0 && host.Returns == 0,
                name + " performs no navigation or detachment");
        }

        var changing = new GraphicCanvas(new Schematic());
        check(Supported(changing), "Changing-root fixture begins as a schematic");
        changing.Root = new DerivedUciPage();
        check(!Supported(changing), "Rechecking a changed root does not reuse its old schematic classification");
        changing.Root = new ComponentPanel();
        check(Supported(changing), "Returning to a component panel restores its canvas classification");
        bool threw = false;
        try
        {
            CanvasContext.IsSupported(changing, typeof(GraphicCanvas), typeof(UciPage),
                _ => throw new MissingMemberException("Root"));
        }
        catch (MissingMemberException) { threw = true; }
        check(threw, "A missing native root member is not silently treated as a supported canvas");
    }

    private class GraphicCanvas(object? root) { internal object? Root = root; }
    private sealed class DerivedCanvas(object? root) : GraphicCanvas(root);
    private class UciPage;
    private class DerivedUciPage : UciPage;
    private sealed class FurtherDerivedUciPage : DerivedUciPage;
    private sealed class Schematic;
    private sealed class ComponentPanel;
    private sealed class Page;
}
