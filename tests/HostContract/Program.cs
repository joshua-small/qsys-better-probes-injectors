using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

if (args.Length != 2) throw new ArgumentException("Usage: HostContract <Designer directory> <built mod DLL>");
int checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
string Hash(string path) { using var input = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant(); }
string designer = Path.Combine(args[0], "Q-Sys Designer.dll");
Check(Hash(designer) == "0e7150f8dc02e0159e019188780fb003ce025f5ec18c491609cf337d849bda60", "Exact Designer 10.5 assembly hash");
Check(Hash(Path.Combine(args[0], "ModuleCommon.dll")) == "a6d1addda06bdcb1ad32b57c7480c947210c3d3ad287f5818c2ea64068bf5aee", "Exact ModuleCommon hash");
using var stream = File.OpenRead(designer); using var pe = new PEReader(stream);
var reader = pe.GetMetadataReader(); var names = new TypeNames(reader);
var types = reader.TypeDefinitions.ToDictionary(h => names.GetTypeFromDefinition(reader, h, 0), h => h);
TypeDefinition Type(string name) => reader.GetTypeDefinition(types.TryGetValue(name, out var handle) ? handle : throw new Exception("Missing type " + name));
IEnumerable<TypeDefinition> Hierarchy(string name)
{
    var current = Type(name);
    while (true)
    {
        yield return current;
        if (current.BaseType.Kind != HandleKind.TypeDefinition) yield break;
        current = reader.GetTypeDefinition((TypeDefinitionHandle)current.BaseType);
    }
}
string Field(string type, string name)
{
    var handles = Hierarchy(type).SelectMany(t => t.GetFields()).Where(h => reader.GetString(reader.GetFieldDefinition(h).Name) == name).ToArray();
    Check(handles.Length == 1, type + "." + name + " field");
    return reader.GetFieldDefinition(handles.Single()).DecodeSignature(names, null);
}
void Property(string type, string name) => Check(Hierarchy(type).SelectMany(t => t.GetProperties())
    .Any(h => reader.GetString(reader.GetPropertyDefinition(h).Name) == name), type + "." + name + " property");
void Method(string type, string name, params string[] parameters) => Check(Hierarchy(type).SelectMany(t => t.GetMethods())
    .Any(h => { var method = reader.GetMethodDefinition(h); return reader.GetString(method.Name) == name
        && method.DecodeSignature(names, (object?)null).ParameterTypes.SequenceEqual(parameters); }), type + "." + name + "(" + string.Join(",", parameters) + ")");
const string D = "QSC.QSys.Design.", G = "QSC.QSys.Graphic.", F = "QSC.QSys.windows.Find.", T = "QSC.QSys.Tools.";
string manager = Field("QSC.QSys.App", "Manager");
foreach (string property in new[] { "Document", "IsLoading", "IsInUndo", "IsInEndOperation" }) Property(manager, property);
Method(manager, "GetElementsOfType");
foreach (string property in new[] { "Id", "BaseClassName", "Label", "UserLabel", "Canvases", "IsAttached" }) Property(D + "Component", property);
Property(D + "UniqueId", "RawString");
Property(D + "Canvas", "AllChildren");
Property(D + "Pin", "IsAttached"); Field(D + "Pin", "Provider"); Field(D + "Pin", "PrettyName");
foreach (string property in new[] { "Id", "VisualStyle", "Value", "IsOrphaned" }) Property(D + "ControlStyle", property);
Property(D + "ControlValue", "StringNext"); Property(D + "ControlValue", "String");
Method(D + "ComponentCache", "GetComponent", D + "UniqueId");
Method("QSC.QSys.Util.Find", "FindAndSelect", D + "Object");
Method("QSC.QSys.Util.Find", "FindAndSelect", D + "Object", "System.Action");
Method(G + "HilightManager", "Add", G + "Element");
Method(G + "HilightManager", "Clear");
Method("QSC.QSys.Util.Selection", "Add", "QSC.QSys.Util.ISelectable", "System.Boolean");
Method("QSC.QSys.Util.Selection", "OnSelectionChanged");
Method("QSC.QSys.Util.Find", "PageNameComponentIsOn", D + "Component");
Method("QSC.QSys.Extract.DesignNet", "GetPinNetName", D + "Pin");
Field("QSC.QSys.Util.Selection", "Global");
Property(G + "Component", "InnerComponent"); Property(G + "Controls.Probe", "Style");
Property(G + "Pin", "InnerPin"); Property(G + "Pins.PinLabel", "Pin");
Property(G + "ProbeTip", "Control");
Property(G + "ProbeTip", "Pin");
Method(G + "ProbeTip", "get_FocusGeometry");
Method(G + "InjectorTip", "get_FocusGeometry");
Check(names.Name(Type(G + "Controls.Injector").BaseType) == G + "Controls.Probe", "Injector shares the Probe control path");
Check(Field(T + "ProbeTipManager", "_ActiveProbes") == "System.Collections.Generic.Dictionary`2<" + D + "Pin," + D + "ControlStyle>", "Active attachment dictionary key/value contract");
Check(Field(T + "ProbeTipManager", "_ActiveTips") == "System.Collections.Generic.Dictionary`2<" + D + "ControlValue," + G + "ProbeTip>", "Active tip dictionary key/value contract");
Method(T + "ProbeTool", "ClearTip", D + "ControlStyle");
Method(T + "ProbeTipManager", "AddTip", G + "Pin", G + "GraphicCanvas", D + "ControlStyle");
Method(T + "ProbeTipManager", "RemoveTip", D + "ControlValue");
Property(T + "ChargedCursorTool", "Command");
Property("QSC.QSys.ControlConnection.Manager", "Status");
var statusProp = Type("QSC.QSys.ControlConnection.Manager").GetProperties().Single(h => reader.GetString(reader.GetPropertyDefinition(h).Name) == "Status");
string statusType = reader.GetPropertyDefinition(statusProp).DecodeSignature(names, (object?)null).ReturnType;
Property(statusType, "IsConnected");
Check(Type(statusType).GetInterfaceImplementations().Any(h => names.Name(reader.GetInterfaceImplementation(h).Interface) == "System.ComponentModel.INotifyPropertyChanged"),
    "Connection status exposes change notifications for reconnect invalidation");
Field(F + "FindArea", "_Choices"); Property(F + "FindArea", "Spec"); Property(F + "FindArea", "Label1"); Property(F + "FindArea", "Label2");
Property(F + "FindSpec", "FindType");
Method(F + "FindArea", "Global_SelectionChanged", "QSC.QSys.Util.Selection");
Field("QSC.QSys.Commands.Find", "_ToolWindow");
foreach (string property in new[] { "Index", "Label", "IsEnabled", "Run", "Tag" }) Property(F + "FindChoice", property);
Method(F + "FindChoice+RunHandler", "Invoke", F + "FindChoice", G + "Element");
// Check the framework-only method binding pattern used for the native delegate without loading Designer.
var action = new DelegateReceiver(); var callback = (TestCallback)Delegate.CreateDelegate(typeof(TestCallback), action, typeof(DelegateReceiver).GetMethod("Run")!);
callback("choice", new()); Check(action.Ran, "Contravariant native Find callback binding pattern");
using var moduleStream = File.OpenRead(args[1]); using var modulePe = new PEReader(moduleStream); var module = modulePe.GetMetadataReader();
Check(module.GetString(module.GetAssemblyDefinition().Name) == "Designer.Modules.BetterProbesInjectors", "Mod assembly identity");
Check(module.GetAssemblyDefinition().Version == new Version(0, 1, 0, 0), "Mod assembly version");
var references = module.AssemblyReferences.Select(h => module.GetString(module.GetAssemblyReference(h).Name)).ToArray();
Check(references.Contains("Qmm.mods-core.Runtime") && references.Contains("0Harmony"), "Shared runtime and Harmony references");
Check(!references.Contains("Q-Sys Designer"), "Private host access remains isolated behind the reflection adapter");
Console.WriteLine($"{checks} metadata/contract checks passed. No vendor assembly was loaded or executed.");

internal delegate void TestCallback(string choice, object element);
internal sealed class DelegateReceiver { public bool Ran; public void Run(object choice, object element) => Ran = true; }
internal sealed class TypeNames(MetadataReader root) : ISignatureTypeProvider<string, object?>
{
    public string Name(EntityHandle handle) => handle.Kind switch {
        HandleKind.TypeDefinition => GetTypeFromDefinition(root, (TypeDefinitionHandle)handle, 0),
        HandleKind.TypeReference => GetTypeFromReference(root, (TypeReferenceHandle)handle, 0),
        HandleKind.TypeSpecification => GetTypeFromSpecification(root, null, (TypeSpecificationHandle)handle, 0), _ => "?" };
    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        var type = reader.GetTypeDefinition(handle); string name = reader.GetString(type.Name);
        return type.IsNested ? GetTypeFromDefinition(reader, type.GetDeclaringType(), 0) + "+" + name : Join(reader.GetString(type.Namespace), name);
    }
    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    { var type = reader.GetTypeReference(handle); return Join(reader.GetString(type.Namespace), reader.GetString(type.Name)); }
    private static string Join(string ns, string name) => ns.Length == 0 ? name : ns + "." + name;
    public string GetTypeFromSpecification(MetadataReader reader, object? context, TypeSpecificationHandle handle, byte kind) => reader.GetTypeSpecification(handle).DecodeSignature(this, context);
    public string GetArrayType(string element, ArrayShape shape) => element + "[]";
    public string GetByReferenceType(string element) => element + "&";
    public string GetFunctionPointerType(MethodSignature<string> signature) => "fn";
    public string GetGenericInstantiation(string type, ImmutableArray<string> args) => type + "<" + string.Join(",", args) + ">";
    public string GetGenericMethodParameter(object? context, int index) => "!!" + index;
    public string GetGenericTypeParameter(object? context, int index) => "!" + index;
    public string GetModifiedType(string modifier, string type, bool required) => type;
    public string GetPinnedType(string type) => type;
    public string GetPointerType(string type) => type + "*";
    public string GetPrimitiveType(PrimitiveTypeCode code) => code switch { PrimitiveTypeCode.Void => "System.Void", PrimitiveTypeCode.Object => "System.Object", PrimitiveTypeCode.String => "System.String", _ => "System." + code };
    public string GetSZArrayType(string element) => element + "[]";
}
