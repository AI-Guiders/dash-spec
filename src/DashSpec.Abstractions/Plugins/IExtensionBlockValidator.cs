namespace DashSpec.Abstractions.Plugins;

/// <summary>Semantic validation for a parsed extension block (implemented by plugin assemblies).</summary>
public interface IExtensionBlockValidator
{
    string BlockKeyword { get; }

    void Validate(ExtensionBlockValidationRequest request);
}

public sealed record ExtensionBlockValidationRequest(
    string BlockKeyword,
    IReadOnlyDictionary<string, string> Properties,
    IReadOnlyList<ExtensionBlockValidationRequest> Nested);
