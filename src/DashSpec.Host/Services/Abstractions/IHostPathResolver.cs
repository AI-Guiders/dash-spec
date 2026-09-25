namespace DashSpec.Host.Services.Abstractions;

public interface IHostPathResolver
{
    string ResolveSpecPath(string contentRoot, string specPath);

    string ResolveCatalogPath(string contentRoot, string catalogPath);

    string? ResolveDashhostPath(string contentRoot, string? dashhostReference);

    string ToHostSpecReference(string contentRoot, string specFullPath);

    string ResolveSpecLibraryPath(
        string specFullPath,
        string? libraryRelative,
        string? libraryFallbackDirectory = null);

    string ResolveRuntimeConfigPath(
        string specFullPath,
        string specText,
        string? configFallbackDirectory = null);
}
