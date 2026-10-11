using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using Microsoft.Identity.Web;

namespace PurchaseRequest.Services
{
    public record DirectoryUser(Guid Id, string DisplayName, string Email, string? Department);

    public interface IDirectoryService
    {
        Task<DirectoryUser?> GetUserAsync(Guid objectId, CancellationToken cancellationToken = default);

        // BR-01: the user's manager as recorded in Entra ID, or null if none is set.
        Task<DirectoryUser?> GetManagerAsync(Guid objectId, CancellationToken cancellationToken = default);
    }

    // Reads users from Microsoft Graph with the app's own identity (User.Read.All application permission).
    public class GraphDirectoryService(HttpClient http, ITokenAcquisition tokenAcquisition) : IDirectoryService
    {
        private const string Select = "?$select=id,displayName,mail,userPrincipalName,department";

        public Task<DirectoryUser?> GetUserAsync(Guid objectId, CancellationToken cancellationToken = default) =>
            GetAsync($"users/{objectId}{Select}", cancellationToken);

        public Task<DirectoryUser?> GetManagerAsync(Guid objectId, CancellationToken cancellationToken = default) =>
            GetAsync($"users/{objectId}/manager{Select}", cancellationToken);

        private async Task<DirectoryUser?> GetAsync(string path, CancellationToken cancellationToken)
        {
            var token = await tokenAcquisition.GetAccessTokenForAppAsync("https://graph.microsoft.com/.default");
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await http.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
            response.EnsureSuccessStatusCode();

            var user = await response.Content.ReadFromJsonAsync<GraphUser>(cancellationToken)
                ?? throw new InvalidOperationException("Microsoft Graph returned an empty user.");
            return new DirectoryUser(user.Id, user.DisplayName ?? "", user.Mail ?? user.UserPrincipalName ?? "", user.Department);
        }

        private sealed record GraphUser(
            [property: JsonPropertyName("id")] Guid Id,
            [property: JsonPropertyName("displayName")] string? DisplayName,
            [property: JsonPropertyName("mail")] string? Mail,
            [property: JsonPropertyName("userPrincipalName")] string? UserPrincipalName,
            [property: JsonPropertyName("department")] string? Department);
    }
}
