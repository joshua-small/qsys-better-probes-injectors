using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Designer.Modules.BetterProbesInjectors;
using Fixture;
using QsysMods.Core;

int checks = 0, failures = 0;
void Check(bool value, string name)
{
    checks++;
    Console.WriteLine((value ? "PASS " : "FAIL ") + name);
    if (!value) failures++;
}
const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
var type = typeof(DesignerHost);
var remember = type.GetMethod("RememberFocus", Private)!;
var interaction = type.GetMethod("Interaction", Private)!;

// Native member shapes are checked separately with PE metadata. Bypass the constructor
// solely to isolate these real adapter branches from native assembly loading.
var host = (DesignerHost)RuntimeHelpers.GetUninitializedObject(type);
var main = new Window();
type.GetField("main", Private)!.SetValue(host, main);
type.GetField("graphicCanvasType", Private)!.SetValue(host, typeof(GraphicCanvas));
void Focus(DependencyObject focus, bool notify = true)
{
    Keyboard.FocusedElement = focus;
    if (notify) remember.Invoke(host, [focus]);
}
bool Allowed(bool fromFind = false) => (bool)interaction.Invoke(host, [fromFind])!;
object? Remembered() => type.GetField("lastCanvas", Private)!.GetValue(host);

var schematic = new GraphicCanvas { Parent = main };
var control = new UIElement { Parent = schematic };
Focus(control);
Check(Allowed() && ReferenceEquals(Remembered(), schematic), "Actual adapter accepts and remembers schematic focus");

foreach (UIElement menu in new UIElement[] { new MenuItem(), new MenuBase(), new ToolBar() })
{
    menu.Parent = main;
    schematic.Root = new object();
    Focus(control);
    Focus(menu);
    Check(Allowed(), menu.GetType().Name + " reuses a current supported canvas");
    schematic.Root = new DerivedUciPage(); // No focus callback occurs after this change.
    Check(!Allowed(), menu.GetType().Name + " rechecks a remembered root changed to a UCI page");
    schematic.Root = new object();
    Check(Allowed(), menu.GetType().Name + " recovers when the remembered canvas is supported again");
}

Focus(control);
Focus(new MenuItem { Parent = main });
var document = App.Manager.Document;
App.Manager.Document = new object();
Check(!Allowed(), "Menu rejects a remembered canvas from another document");
App.Manager.Document = document;
schematic.IsVisible = false;
Check(!Allowed(), "Menu rejects an invisible remembered canvas");
schematic.IsVisible = true;
schematic.IsEnabled = false;
Check(!Allowed(), "Menu rejects a disabled remembered canvas");
schematic.IsEnabled = true;

foreach (GraphicCanvas uci in new GraphicCanvas[]
{
    new() { Root = new UciPage(), Parent = schematic },
    new DerivedGraphicCanvas { Root = new DerivedUciPage(), Parent = schematic }
})
{
    var innerControl = new UIElement { Parent = uci };
    Focus(innerControl);
    Check(!Allowed(), "First UCI canvas cannot fall through to an enclosing schematic canvas");
    Check(Remembered() is null, "Focus memory does not skip a first UCI canvas to remember an enclosing schematic");
    Focus(new MenuItem { Parent = main });
    Check(!Allowed(), "Menu after a nested UCI canvas has no remembered schematic fallback");
}

Focus(control);
ControlInput.TextInput = true;
Check(!Allowed(), "Actual adapter rejects text input");
remember.Invoke(host, [control]);
Check(Remembered() is null, "Text focus clears remembered canvas context");
ControlInput.TextInput = false;
Focus(control);
Check(Allowed(), "Schematic focus recovers after excluded input");
Focus(new UIElement { Parent = main });
Check(!Allowed() && Remembered() is null, "Unrelated main-window focus clears and rejects canvas context");
Focus(new UIElement { Parent = new GraphicCanvas { Parent = new Window() } });
Check(!Allowed(), "Foreign-window focus remains excluded");
Focus(control);
Host.Enabled = false;
Check(!Allowed(), "Disabled host remains excluded");
Host.Enabled = true;
main.IsActive = false;
Check(!Allowed(), "Inactive main window remains excluded");
main.IsActive = true;
Focus(new UIElement { Parent = new GraphicCanvas { Root = new UciPage(), Parent = main } });
Check(Allowed(fromFind: true), "Native Find retains its separately guarded callback route");

Console.WriteLine($"{checks - failures}/{checks} adapter checks passed; {failures} failed. Synthetic framework objects; no WPF, Designer or hardware execution.");
return failures == 0 ? 0 : 1;
