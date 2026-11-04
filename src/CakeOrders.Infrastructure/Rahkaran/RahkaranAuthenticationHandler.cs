using System.Net;

namespace CakeOrders.Infrastructure.Rahkaran;

public sealed class RahkaranAuthenticationHandler(
    RahkaranAuthenticationSession session) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        await session.EnsureAuthenticatedAsync(cancellationToken);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            session.Invalidate();
        return response;
    }
}