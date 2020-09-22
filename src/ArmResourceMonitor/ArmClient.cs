using Azure.Identity;
using Microsoft.Azure.Services.AppAuthentication;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace ArmResourceMonitor
{
    public static class ArmClient
    {
        private static HttpClient HttpClient { get; set; } = new HttpClient();

        public static async Task<string> GetResourceAsync(string resourceId, string apiVersion)
        {
            // Initialize the request authorization using the app's managed identity.
            var azureServiceTokenProvider = new AzureServiceTokenProvider();
            var token = await azureServiceTokenProvider.GetAccessTokenAsync("https://management.azure.com");
            HttpClient.DefaultRequestHeaders.Remove("Authorization");
            HttpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);

            // Build the URL of the API to access.
            var uriBuilder = new UriBuilder("https://management.azure.com/");
            uriBuilder.Path = resourceId;
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query["api-version"] = apiVersion;
            uriBuilder.Query = query.ToString();

            // Submit the request.
            var response = await HttpClient.GetAsync(uriBuilder.Uri);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public static async Task<string> GetLatestApiVersionForResource(string resourceId)
        {
            // Parse the resource provider and subscription ID out of the resource ID.
            // TODO
            var resourceProviderNamespaceName = "Microsoft.Network";
            var resourceTypeName = "frontDoors";
            var subscriptionId = "TODO";

            var client = new Azure.ResourceManager.Resources.ResourcesManagementClient(subscriptionId, new DefaultAzureCredential());
            var resourceProviderNamespace = client.Providers.Get(resourceProviderNamespaceName).Value;
            var resourceType = resourceProviderNamespace?.ResourceTypes.SingleOrDefault(t => string.Equals(t.ResourceType, resourceTypeName, StringComparison.InvariantCultureIgnoreCase));
            return resourceType?.ApiVersions.First(); // TODO sort order
        }
    }
}
