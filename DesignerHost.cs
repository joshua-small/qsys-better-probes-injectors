using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using QsysMods.Core;

namespace Designer.Modules.BetterProbesInjectors;

internal sealed class DesignerHost : IToolHost, IDisposable
{
    private readonly Window main;
    private DependencyObject? lastCanvas;
    private object? focusDocument;
    internal readonly Type FindAreaType = Host.Type("QSC.QSys.windows.Find.FindArea");
    internal readonly Type FindChoiceType = Host.Type("QSC.QSys.windows.Find.FindChoice");
    internal readonly Type TipManagerType = Host.Type("QSC.QSys.Tools.ProbeTipManager");
    private readonly Type componentType = Host.Type("QSC.QSys.Design.Component");
    private readonly Type pinType = Host.Type("QSC.QSys.Design.Pin");
    private readonly Type styleType = Host.Type("QSC.QSys.Design.ControlStyle");
    private readonly Type findType = Host.Type("QSC.QSys.Util.Find");
    private readonly Type graphicCanvasType = Host.Type("QSC.QSys.Graphic.GraphicCanvas");
    private readonly FieldInfo activeProbes;
    private readonly FieldInfo activeTips;
    private readonly MethodInfo clearTip;
    private readonly MethodInfo findObject;
    private readonly MethodInfo findThen;
    private readonly MethodInfo highlight, clearHighlight, select, selectionChanged;
    private readonly MethodInfo pageName;
    private readonly MethodInfo pinNetName;
    private readonly MethodInfo resolveComponent;
    private readonly INotifyPropertyChanged connectionStatus;
    private long connectionEpoch;
    private long navigationGeneration;
    private bool disposed;

    internal DesignerHost(Window main)
    {
        this.main = main;
        activeProbes = RequiredField(TipManagerType, "_ActiveProbes", Host.Static);
        activeTips = RequiredField(TipManagerType, "_ActiveTips", Host.Static);
        clearTip = Method(Host.Type("QSC.QSys.Tools.ProbeTool"), "ClearTip", Host.Static, styleType);
        findObject = Method(findType, "FindAndSelect", Host.Static, Host.Type("QSC.QSys.Design.Object"));
        findThen = Method(findType, "FindAndSelect", Host.Static, Host.Type("QSC.QSys.Design.Object"), typeof(Action));
        var elementType = Host.Type("QSC.QSys.Graphic.Element");
        var highlights = Host.Type("QSC.QSys.Graphic.HilightManager");
        highlight = Method(highlights, "Add", Host.Static, elementType);
        clearHighlight = Method(highlights, "Clear", Host.Static);
        select = Method(Selection.GetType(), "Add", Host.Instance, Host.Type("QSC.QSys.Util.ISelectable"), typeof(bool));
        selectionChanged = Method(Selection.GetType(), "OnSelectionChanged", Host.Instance);
        pageName = Method(findType, "PageNameComponentIsOn", Host.Static, componentType);
        pinNetName = Method(Host.Type("QSC.QSys.Extract.DesignNet"), "GetPinNetName", Host.Static, pinType);
        resolveComponent = Method(Host.Type("QSC.QSys.Design.ComponentCache"), "GetComponent", Host.Static,
            Host.Type("QSC.QSys.Design.UniqueId"));
        connectionStatus = (INotifyPropertyChanged)Static("QSC.QSys.ControlConnection.Manager", "Status")!;
        RememberFocus(Keyboard.FocusedElement as DependencyObject);
        connectionStatus.PropertyChanged += ConnectionChanged;
        main.PreviewGotKeyboardFocus += FocusChanged;
    }

    internal static object Manager => Static("QSC.QSys.App", "Manager")!;
    internal static object Document => Get(Manager, "Document")!;
    internal static object Selection => HostSelection.GlobalSelection;
    internal static object[] Items(object? value) => value is IEnumerable values ? values.Cast<object>().ToArray() : [];
    internal static object? Get(object? instance, string name)
    {
        if (instance is null) return null;
        var type = instance.GetType();
        if (type.GetProperty(name, Host.Instance) is { } property) return property.GetValue(instance);
        if (type.GetField(name, Host.Instance) is { } field) return field.GetValue(instance);
        throw new MissingMemberException(type.FullName, name);
    }
    internal static object? Static(string typeName, string name)
    {
        var type = Host.Type(typeName);
        return type.GetProperty(name, Host.Static) is { } property ? property.GetValue(null)
            : RequiredField(type, name, Host.Static).GetValue(null);
    }
    internal static void Set(object value, string name, object? content) =>
        (value.GetType().GetProperty(name, Host.Instance) ?? throw new MissingMemberException(value.GetType().FullName, name)).SetValue(value, content);
    internal static FieldInfo RequiredField(Type type, string name, BindingFlags flags) => type.GetField(name, flags)
        ?? throw new MissingFieldException(type.FullName, name);
    internal static MethodInfo Method(Type type, string name, BindingFlags flags, params Type[] args) =>
        type.GetMethod(name, flags, null, args, null) ?? throw new MissingMethodException(type.FullName, name);

    private void FocusChanged(object sender, KeyboardFocusChangedEventArgs e) => RememberFocus(e.NewFocus as DependencyObject);
    private void ConnectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == "IsConnected") Interlocked.Increment(ref connectionEpoch);
    }
    private bool IsSupportedCanvas(object canvas) => CanvasContext.IsSupported(canvas, graphicCanvasType,
        Host.Type("QSC.QSys.Design.WebPanels.Page"), value => Get(value, "Root"));

    private void RememberFocus(DependencyObject? focus)
    {
        if (focus is null) return;
        for (var current = focus; current is not null; current = ControlInput.Parent(current))
            if (current is MenuBase or MenuItem or ToolBar) return; // A menu retains its prior editor context.
        lastCanvas = null; focusDocument = null;
        if (ControlInput.IsTextInput()) return;
        for (var current = focus; current is not null; current = ControlInput.Parent(current))
            if (graphicCanvasType.IsInstanceOfType(current))
            {
                // The nearest canvas owns focus, including when it excludes our commands.
                if (IsSupportedCanvas(current)) { lastCanvas = current; focusDocument = Document; }
                return;
            }
    }

    private bool Interaction(bool fromFind)
    {
        if (!Host.Enabled || !main.IsLoaded || !main.IsEnabled || !main.IsVisible) return false;
        if (fromFind) return true; // Only native Find callbacks use this path; their panel/selection is rechecked separately.
        if (!main.IsActive || ControlInput.IsTextInput() || Keyboard.FocusedElement is not DependencyObject focus
            || Window.GetWindow(focus) != main) return false;
        for (var current = focus; current is not null; current = ControlInput.Parent(current))
            if (graphicCanvasType.IsInstanceOfType(current)) return IsSupportedCanvas(current);
            else if (current is MenuBase or MenuItem or ToolBar)
                return ReferenceEquals(focusDocument, Document) && lastCanvas is UIElement { IsVisible: true, IsEnabled: true }
                    && IsSupportedCanvas(lastCanvas);
        return false;
    }

    public Workspace Capture(bool fromFind = false)
    {
        main.Dispatcher.VerifyAccess();
        long epochBefore = Interlocked.Read(ref connectionEpoch);
        var manager = Manager;
        var document = Get(manager, "Document")!;
        var selection = Items(Selection);
        bool busy = new[] { "IsLoading", "IsInUndo", "IsInEndOperation" }.Any(name => Get(manager, name) is true);
        bool placing = Static("QSC.QSys.Tools.ChargedCursorTool", "Command") is not null;
        bool connected = Get(Static("QSC.QSys.ControlConnection.Manager", "Status"), "IsConnected") is true;
        var getElements = manager.GetType().GetMethods(Host.Instance).Single(m => m.Name == "GetElementsOfType"
            && m.IsGenericMethodDefinition && m.GetParameters().Length == 0).MakeGenericMethod(componentType);
        var components = Items(getElements.Invoke(manager, null)).Where(c => Get(c, "IsAttached") is true)
            .Distinct(ReferenceEqualityComparer.Instance).ToArray();
        var membership = new HashSet<object>(components, ReferenceEqualityComparer.Instance);
        var probes = new List<DictionaryEntry>();
        foreach (DictionaryEntry entry in (IDictionary)activeProbes.GetValue(null)!) probes.Add(entry);
        var tips = (IDictionary)activeTips.GetValue(null)!;
        var tools = new List<ToolItem>();
        foreach (var component in components)
        {
            string className = Get(component, "BaseClassName")?.ToString() ?? "";
            if (className is not ("probe" or "injector")) continue;
            var kind = className == "probe" ? ToolKind.Probe : ToolKind.Injector;
            string id = Get(Get(component, "Id"), "RawString")!.ToString()!;
            string name = Get(component, "UserLabel")?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(name)) name = Get(component, "Label")?.ToString() ?? kind.ToString();
            string page = pageName.Invoke(null, [component])?.ToString() ?? "";
            var mappings = probes.Where(p => ReferenceEquals(Owner(p.Value), component)).ToArray();
            bool known = mappings.Length <= 1;
            Attachment? attachment = null;
            if (mappings.Length == 1)
            {
                var pin = mappings[0].Key; var control = mappings[0].Value!;
                var value = Get(control, "Value");
                var tip = value is not null && tips.Contains(value) ? tips[value] : null;
                known = connected && value is not null && Get(control, "IsOrphaned") is false
                    && Get(control, "VisualStyle")?.ToString() == kind.ToString()
                    && Get(pin, "IsAttached") is true && membership.Contains(Get(pin, "Provider")!)
                    && tip is not null && ReferenceEquals(Get(tip, "Control"), control)
                    // Native placement also permits an unwired input whose net name is empty.
                    // Validate against the native pin net instead of treating empty as detached.
                    && StringComparer.Ordinal.Equals(Get(value, "StringNext")?.ToString(), pinNetName.Invoke(null, [pin])?.ToString());
                if (known)
                {
                    var provider = Get(pin, "Provider")!;
                    string destination = $"{pageName.Invoke(null, [provider])}: {Get(provider, "Label")} / {Get(pin, "PrettyName")}";
                    attachment = new(pin, control, value!, Get(value, "StringNext")?.ToString() ?? "", destination);
                }
            }
            else if (mappings.Length == 0)
            {
                // An untracked, nonempty attachment value is not evidence that the tool is home.
                var controls = Items(Get(component, "Canvases")).SelectMany(c => Items(Get(c, "AllChildren")))
                    .Where(c => styleType.IsInstanceOfType(c) && Get(c, "VisualStyle")?.ToString() == kind.ToString()
                        && ReferenceEquals(Owner(c), component)).ToArray();
                known = controls.Length > 0 && controls.All(c => Get(c, "IsOrphaned") is false && Get(c, "Value") is { } value
                    && string.IsNullOrEmpty(Get(value, "StringNext")?.ToString()) && string.IsNullOrEmpty(Get(value, "String")?.ToString()));
            }
            tools.Add(new(id, kind, name, page, component, attachment, known));
        }
        string? focused = selection.Length == 1 ? FocusedId(selection[0], tools) : null;
        long epochAfter = Interlocked.Read(ref connectionEpoch);
        return new(document, tools, selection, focused, connected, Interaction(fromFind), busy || epochBefore != epochAfter, placing, epochAfter);
    }

    private object? Owner(object? control) => control is not null && styleType.IsInstanceOfType(control)
        ? resolveComponent.Invoke(null, [Get(control, "Id")]) : null;

    private string? FocusedId(object selected, List<ToolItem> tools)
    {
        object? component = null; object? pin = null;
        if (componentType.IsInstanceOfType(selected)) component = selected;
        else if (Host.Type("QSC.QSys.Graphic.Component").IsInstanceOfType(selected)) component = Get(selected, "InnerComponent");
        else if (Host.Type("QSC.QSys.Graphic.ProbeTip").IsInstanceOfType(selected)) component = Owner(Get(selected, "Control"));
        else if (Host.Type("QSC.QSys.Graphic.Controls.Probe").IsInstanceOfType(selected)) component = Owner(Get(selected, "Style"));
        else if (styleType.IsInstanceOfType(selected)) component = Owner(selected);
        else if (Host.Type("QSC.QSys.Graphic.Pin").IsInstanceOfType(selected)) pin = Get(selected, "InnerPin");
        else if (Host.Type("QSC.QSys.Graphic.Pins.PinLabel").IsInstanceOfType(selected)) pin = Get(Get(selected, "Pin"), "InnerPin");
        else if (pinType.IsInstanceOfType(selected)) pin = selected;
        return component is not null ? tools.SingleOrDefaultSafe(t => ReferenceEquals(t.Component, component))?.Id
            : pin is not null ? tools.SingleOrDefaultSafe(t => t.Attachment is { } a && ReferenceEquals(a.Pin, pin))?.Id : null;
    }

    public void Navigate(ToolItem tool, bool homeComponent)
    {
        main.Dispatcher.VerifyAccess();
        long generation = ++navigationGeneration;
        if (homeComponent)
        {
            if (findObject.Invoke(null, [tool.Component]) is false) throw new InvalidOperationException("Designer could not locate the home component.");
        }
        else
        {
            var before = Capture(true);
            var provider = Get(tool.Attachment!.Pin, "Provider")!;
            // The native Pin overload can highlight an empty PinLabel at the canvas origin.
            // Let native Find open the provider, then select the live tip in canvas coordinates.
            findThen.Invoke(null, [provider, new Action(() => main.Dispatcher.Invoke(() =>
            {
                try
                {
                    if (disposed || generation != navigationGeneration || !Host.Enabled || !main.IsLoaded) return;
                    var now = Capture(true);
                    if (!ToolNavigation.CanCompleteNavigation(before, now, tool)) return;
                    var selected = now.Selection.SingleOrDefault();
                    if (selected is null || !Host.Type("QSC.QSys.Graphic.Component").IsInstanceOfType(selected)
                        || !ReferenceEquals(Get(selected, "InnerComponent"), provider)) return;
                    var tips = (IDictionary)activeTips.GetValue(null)!;
                    if (tips[tool.Attachment.Value] is not { } tip
                        || !ReferenceEquals(Get(tip, "Control"), tool.Attachment.Control)
                        || !ReferenceEquals(Get(Get(tip, "Pin"), "InnerPin"), tool.Attachment.Pin)) return;
                    select.Invoke(Selection, [tip, true]);
                    selectionChanged.Invoke(Selection, null);
                    main.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() =>
                    {
                        try
                        {
                            if (disposed || generation != navigationGeneration || !Host.Enabled) return;
                            var final = Capture(true);
                            if (!ToolNavigation.CanCompleteNavigation(before, final, tool)
                                || final.Selection.Count != 1 || !ReferenceEquals(final.Selection[0], tip)) return;
                            clearHighlight.Invoke(null, null);
                            NativeFindIntegration.HighlightTip(tip);
                            highlight.Invoke(null, [tip]);
                            Host.Receipt(ProbeModule.Id, "tip-selected", new { tool = tool.Id, destination = tool.Attachment.Description,
                                position = Get(tip, "Position")?.ToString(), bounds = (tip as System.Windows.Media.DrawingVisual)?.ContentBounds.ToString(),
                                geometry = Get(tip, "FocusGeometry")?.ToString() });
                        }
                        catch (Exception error) { Host.Error(ProbeModule.Id, error); }
                    }));
                }
                catch (Exception error) { Host.Error(ProbeModule.Id, error); }
            }))]);
        }
    }

    public void RecordReturn(ToolItem tool) => Host.RequiredReceipt(ProbeModule.Id, "return-home-start", new
    { tool = tool.Id, kind = tool.Kind.ToString(), source = tool.Attachment?.Description });

    public void ReturnHome(ToolItem tool)
    {
        main.Dispatcher.VerifyAccess();
        clearTip.Invoke(null, [tool.Attachment!.Control]);
        if (!string.IsNullOrEmpty(Get(tool.Attachment.Value, "StringNext")?.ToString()))
            throw new InvalidOperationException("Designer did not clear the attachment value. Inspect the tool before retrying.");
        foreach (DictionaryEntry entry in (IDictionary)activeProbes.GetValue(null)!)
            if (ReferenceEquals(entry.Value, tool.Attachment.Control))
                throw new InvalidOperationException("Designer retained the tool's attachment entry. Inspect the tool before retrying.");
        navigationGeneration++;
        clearHighlight.Invoke(null, null);
        NativeFindIntegration.ClearTipHighlight();
        findObject.Invoke(null, [tool.Component]);
        Host.Receipt(ProbeModule.Id, "return-home-requested", new { tool = tool.Id, runtimeConfirmation = "not observed" });
    }

    internal void RefreshFind(object area)
    {
        if (area is UIElement { IsVisible: true })
            Method(FindAreaType, "Global_SelectionChanged", Host.Instance, Selection.GetType()).Invoke(area, [Selection]);
    }
    public void Dispose()
    { disposed = true; navigationGeneration++; main.PreviewGotKeyboardFocus -= FocusChanged; connectionStatus.PropertyChanged -= ConnectionChanged; }
}
