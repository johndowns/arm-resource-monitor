using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System;
using System.Text.RegularExpressions;

namespace ArmResourceMonitor
{
    public class AddResourceMonitorRequest
    {
        public string ResourceId { get; set; }

        public string ApiVersion { get; set; }

        public int CheckFrequencySeconds { get; set; } = 86400; // TODO change to minutes/days/timespan?
    }

    public static class ArmResourceMonitorFunctions
    {
        public static readonly Regex DisallowedCharsInTableKeys = new Regex(@"[\\\\#%+/?\u0000-\u001F\u007F-\u009F]");

        [FunctionName("AddResourceMonitor")]
        public static async Task<IActionResult> AddResourceMonitor(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] AddResourceMonitorRequest request,
            ILogger log,
            [DurableClient] IDurableEntityClient entityClient)
        {
            // Validate the request body.
            if (request.ResourceId == null)
            {
                return new BadRequestObjectResult(new
                {
                    error = "resourceId must be specified."
                });
            }

            if (request.ApiVersion == null)
            {
                request.ApiVersion = await ArmClient.GetLatestApiVersionForResource(request.ResourceId);

                if (request.ApiVersion == null)
                {
                    return new BadRequestObjectResult(new
                    {
                        error = "apiVersion could not be automatically determined and so must be specified."
                    });
                }
            }

            // Initialize the monitor entity.
            var resourceMonitorId = DisallowedCharsInTableKeys.Replace(request.ResourceId, "|");
            var entityId = new EntityId(nameof(ResourceMonitorEntity), resourceMonitorId);
            await entityClient.SignalEntityAsync(entityId, nameof(ResourceMonitorEntity.Initialize), (request.ResourceId, request.ApiVersion, TimeSpan.FromSeconds(request.CheckFrequencySeconds)));

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
