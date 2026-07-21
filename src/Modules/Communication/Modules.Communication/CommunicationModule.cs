using FSH.Framework.Eventing;
using FSH.Framework.Persistence;
using FSH.Framework.Web.Modules;
using FSH.Modules.Communication.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

[assembly: FshModule(typeof(FSH.Modules.Communication.CommunicationModule), 760)]

namespace FSH.Modules.Communication;

/// <summary>
/// eProcure communication: integration-event handlers that materialize in-app inbox rows via
/// <c>IInboxNotifier</c> (Notifications schema). No separate inbox tables — Chat stays separate.
/// Order 760: after Notifications (750) so <c>IInboxNotifier</c> is registered.
/// </summary>
public sealed class CommunicationModule : IModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddIntegrationEventHandlers(typeof(CommunicationModule).Assembly);
        builder.Services.AddScoped<IDbInitializer, CommunicationDbInitializer>();
    }

    public void ConfigureMiddleware(IApplicationBuilder app) { }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Inbox API lives under /api/v1/notifications — Communication only owns event handlers.
    }
}
