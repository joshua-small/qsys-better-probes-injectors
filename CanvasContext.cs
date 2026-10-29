namespace Designer.Modules.BetterProbesInjectors;

internal static class CanvasContext
{
    // Classify by runtime identity so derived UCI pages retain their own navigation.
    internal static bool IsSupported(object? value, Type canvasType, Type uciPageType, Func<object, object?> readRoot) =>
        canvasType.IsInstanceOfType(value) && !uciPageType.IsInstanceOfType(readRoot(value!));
}
