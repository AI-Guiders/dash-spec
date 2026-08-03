using DashSpec.LanguageServer;
using DashSpec.LanguageServer.Handlers;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server;

var server = await LanguageServer.From(options =>
    options
        .WithInput(Console.OpenStandardInput())
        .WithOutput(Console.OpenStandardOutput())
        .WithServices(services => services.AddSingleton<DashSpecServerState>())
        .WithHandler<DashSpecTextDocumentHandler>()
        .WithHandler<DashSpecCompletionHandler>()
        .WithHandler<DashSpecDefinitionHandler>()
        .OnInitialize((server, request, _) =>
        {
            var state = server.Services.GetRequiredService<DashSpecServerState>();
            state.SetWorkspaceRoots(ResolveWorkspaceRoots(request));
            return Task.CompletedTask;
        })
        .OnInitialized((server, request, _, _) =>
        {
            if (request.WorkspaceFolders is not null && request.WorkspaceFolders.Any())
            {
                var state = server.Services.GetRequiredService<DashSpecServerState>();
                state.RescanWorkspaceRoots(request.WorkspaceFolders.Select(w => w.Uri.GetFileSystemPath()));
            }

            return Task.CompletedTask;
        }));

await server.WaitForExit;

static IEnumerable<string> ResolveWorkspaceRoots(InitializeParams request)
{
    if (request.WorkspaceFolders is not null && request.WorkspaceFolders.Any())
    {
        foreach (var folder in request.WorkspaceFolders)
        {
            yield return folder.Uri.GetFileSystemPath();
        }

        yield break;
    }

    if (request.RootUri is not null)
    {
        yield return request.RootUri.GetFileSystemPath();
    }
    else if (!string.IsNullOrWhiteSpace(request.RootPath))
    {
        yield return Path.GetFullPath(request.RootPath);
    }
}
