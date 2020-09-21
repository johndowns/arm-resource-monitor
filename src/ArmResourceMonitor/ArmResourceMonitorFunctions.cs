using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System;
using System.Text.RegularExpressions;

namespace ArmResourceMonitor
{
    public static class ArmResourceMonitorFunctions
    {
        public static readonly Regex DisallowedCharsInTableKeys = new Regex(@"[\\\\#%+/?\u0000-\u001F\u007F-\u009F]");

        [FunctionName("AddResourceMonitor")]
        public static async Task<IActionResult> AddResourceMonitor(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log,
            [DurableClient] IDurableEntityClient entityClient)
        {
            var resourceId = "/subscriptions/d178c7c4-ffb7-467e-a397-042c1d428092/resourceGroups/longlivedforreference/providers/Microsoft.Network/frontdoors/jodownsll";
            var apiVersion = "2019-05-01"; // TODO try to detect if not specified

            // Assemble the request.
            var checkFrequency = TimeSpan.FromSeconds(30);
            // TODO

            // Initialize the monitor entity.
            var resourceMonitorId = DisallowedCharsInTableKeys.Replace(resourceId, "|");
            var entityId = new EntityId(nameof(ResourceMonitorEntity), resourceMonitorId);
            await entityClient.SignalEntityAsync(entityId, nameof(ResourceMonitorEntity.Initialize), (resourceId, apiVersion, checkFrequency));

            return new OkResult();
        }

        [FunctionName("ProcessResourceUpdate")]
        public static Task ProcessResourceUpdate(
            [QueueTrigger("resource-updated", Connection = "AzureWebJobsStorage")] string resourceUpdatedMessage,
            ILogger log)
        {
            // TODO
            return Task.CompletedTask;
        }

        [FunctionName("ProcessResourceUpdateError")]
        public static Task ProcessResourceUpdateError(
            [QueueTrigger("resource-update-error", Connection = "AzureWebJobsStorage")] string errorMessage,
            ILogger log)
        {
            // TODO
            return Task.CompletedTask;
        }
    }
}
