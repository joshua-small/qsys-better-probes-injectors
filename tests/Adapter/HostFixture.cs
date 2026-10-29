// Minimal compile-time stand-ins. Tests execute the actual DesignerHost focus methods,
// but do not claim WPF event delivery, native parent shapes or Designer acceptance.
namespace System.Windows
{
    internal class DependencyObject { internal DependencyObject? Parent; }
    internal class UIElement : DependencyObject
    {
        public bool IsVisible { get; set; } = true;
        public bool IsEnabled { get; set; } = true;
    }
    internal sealed class Window : UIElement
    {
        public bool IsLoaded { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public Threading.Dispatcher Dispatcher { get; } = new();
        public event Input.KeyboardFocusChangedEventHandler PreviewGotKeyboardFocus { add { } remove { } }
        internal static Window? GetWindow(DependencyObject value)
        {
            for (DependencyObject? current = value; current is not null; current = current.Parent)
                if (current is Window window) return window;
            return null;
        }
    }
}
namespace System.Windows.Controls.Primitives { internal class MenuBase : UIElement; }
namespace System.Windows.Controls
{
    internal sealed class MenuItem : UIElement;
    internal sealed class ToolBar : UIElement;
}
namespace System.Windows.Input
{
    internal static class Keyboard { internal static object? FocusedElement; }
    internal sealed class KeyboardFocusChangedEventArgs { internal object? NewFocus { get; init; } }
    internal delegate void KeyboardFocusChangedEventHandler(object sender, KeyboardFocusChangedEventArgs args);
}
namespace System.Windows.Threading
{
    internal enum DispatcherPriority { ContextIdle }
    internal sealed class Dispatcher
    {
        internal void VerifyAccess() => throw new NotSupportedException("Native operations are outside the focus fixture");
        internal void Invoke(Action action) => throw new NotSupportedException();
        internal void BeginInvoke(DispatcherPriority priority, Action action) => throw new NotSupportedException();
    }
}
namespace System.Windows.Media
{
    internal sealed class DrawingVisual { internal object ContentBounds => throw new NotSupportedException(); }
}
namespace QsysMods.Core
{
    internal static class ControlInput
    {
        internal static bool TextInput;
        internal static bool IsTextInput() => TextInput;
        internal static System.Windows.DependencyObject? Parent(System.Windows.DependencyObject value) => value.Parent;
    }
    internal static class Host
    {
        internal static bool Enabled = true;
        internal const System.Reflection.BindingFlags Instance = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        internal const System.Reflection.BindingFlags Static = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        internal static Type Type(string name) => name switch
        {
            "QSC.QSys.App" => typeof(Fixture.App),
            "QSC.QSys.Design.WebPanels.Page" => typeof(Fixture.UciPage),
            _ => throw new NotSupportedException("Unexpected native lookup: " + name)
        };
        internal static void RequiredReceipt(string id, string action, object data) => throw new NotSupportedException();
        internal static void Receipt(string id, string action, object data) => throw new NotSupportedException();
        internal static void Error(string id, Exception error) => throw new NotSupportedException();
    }
    internal static class HostSelection { internal static object GlobalSelection => throw new NotSupportedException(); }
}
namespace Designer.Modules.BetterProbesInjectors
{
    internal static class ProbeModule { internal const string Id = "better-probes-injectors"; }
    internal static class NativeFindIntegration
    {
        internal static void HighlightTip(object tip) => throw new NotSupportedException();
        internal static void ClearTipHighlight() => throw new NotSupportedException();
    }
}
namespace Fixture
{
    internal static class App { public static Manager Manager { get; } = new(); }
    internal sealed class Manager { public object Document { get; set; } = new(); }
    internal class UciPage;
    internal sealed class DerivedUciPage : UciPage;
    internal class GraphicCanvas : System.Windows.UIElement { public object? Root { get; set; } = new(); }
    internal sealed class DerivedGraphicCanvas : GraphicCanvas;
}
