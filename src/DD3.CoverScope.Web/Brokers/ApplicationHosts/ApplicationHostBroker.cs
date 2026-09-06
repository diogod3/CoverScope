using DD3.CoverScope.Models;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace DD3.CoverScope.Brokers.ApplicationHosts;

public interface IApplicationHostBroker
{
    ValueTask RunAsync(CoverScopeSessionRequest request, Func<Uri, ValueTask> onStarted, CancellationToken cancellationToken = default);
}

public class ApplicationHostBroker : IApplicationHostBroker
{
    public async ValueTask RunAsync(CoverScopeSessionRequest request, Func<Uri, ValueTask> onStarted,
        CancellationToken cancellationToken = default)
    {
        await using var app = CoverScopeHost.CreateApplication(request);
        await app.StartAsync(cancellationToken);
        try
        {
            var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses;
            var address = addresses?.SingleOrDefault(value => value.StartsWith("http://127.0.0.1:", StringComparison.OrdinalIgnoreCase))
                ?? app.Urls.Single();
            await onStarted(new Uri(address));
            await app.WaitForShutdownAsync(cancellationToken);
        }
        finally
        {
            using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await app.StopAsync(shutdown.Token);
        }
    }
}
