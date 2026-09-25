using DashSpec.Host.Configuration;

namespace DashSpec.Host.Services.Abstractions;

public interface IDashSpecTomlLoader
{
    DashSpecTomlRoot LoadFile(string path);

    DashSpecTomlRoot Merge(DashSpecTomlRoot root, DashSpecTomlRoot overlay);

    IEnumerable<KeyValuePair<string, string?>> Flatten(DashSpecTomlRoot root);
}
