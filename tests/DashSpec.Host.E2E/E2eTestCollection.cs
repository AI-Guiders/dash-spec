using DashSpec.Host.E2E.Hosting;
using DashSpec.Host.E2E.Playwright;
using Xunit;

namespace DashSpec.Host.E2E;

[CollectionDefinition(nameof(E2eTestCollection))]
public sealed class E2eTestCollection :
    ICollectionFixture<DashSpecWebApplicationFactory>,
    ICollectionFixture<PlaywrightBrowserFixture>;
