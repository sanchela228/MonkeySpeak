namespace Core.Application.Abstractions;

public interface IHttpApiClient
{
    Task<HttpResponseMessage> GetAsync(string relativeUrl, CancellationToken ct);
}