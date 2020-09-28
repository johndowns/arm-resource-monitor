using System;
using System.Net.Http;
using System.Threading.Tasks;
using JsonDiffPatchDotNet;
using Microsoft.Azure.Storage.Queue;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace ArmResourceMonitor
{
    public interface IResourceMonitorEntity
    {
        Task Initialize((string resourceId, string apiVersion, TimeSpan checkFrequency) args);

        Task CheckResource();
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class ResourceMonitorEntity : IResourceMonitorEntity
    {
        [JsonProperty("resourceId")]
        private string ResourceId { get; set; }

        [JsonProperty("apiVersion")]
        private string ApiVersion { get; set; }

        [JsonProperty("checkFrequency")]
        private TimeSpan CheckFrequency { get; set; }

        [JsonProperty("currentState")]
        private string CurrentState { get; set; }

        [JsonProperty("lastCheckedTime")]
        private DateTime LastCheckedTime { get; set; }

        [JsonProperty("lastUpdatedTime")]
        private DateTime LastUpdatedTime { get; set; }

        private ILogger Log { get; set; }

        private CloudQueue OutputQueue { get; set; }

        private CloudQueue ErrorQueue { get; set; }

        [FunctionName(nameof(ResourceMonitorEntity))]
        public static Task Run(
            [EntityTrigger] IDurableEntityContext ctx,
            ILogger log,
            [Queue("resource-updated")] CloudQueue outputQueue,
            [Queue("resource-update-error")] CloudQueue errorQueue)
            => ctx.DispatchAsync<ResourceMonitorEntity>(log, outputQueue, errorQueue);

        public ResourceMonitorEntity(ILogger log, CloudQueue outputQueue, CloudQueue errorQueue)
        {
            Log = log;
            OutputQueue = outputQueue;
            ErrorQueue = errorQueue;
        }

        public Task Initialize((string resourceId, string apiVersion, TimeSpan checkFrequency) args)
        {
            // If the entity has already been initialized, this should be a no-op.
            if (ResourceId != null)
            {
                return Task.CompletedTask;
            }

            // Initialize the entity properties.
            ResourceId = args.resourceId;
            ApiVersion = args.apiVersion;
            CheckFrequency = args.checkFrequency;

            // Trigger the first check of the resource.
            Entity.Current.SignalEntity(Entity.Current.EntityId, nameof(CheckResource));

            return Task.CompletedTask;
        }

        public async Task CheckResource()
        {
            var timeRequested = DateTime.UtcNow;

            try
            {
                // Make an ARM API request to get the resource body.
                var newState = await ArmClient.GetResourceAsync(ResourceId, ApiVersion);

                // Check if is different from the old state.
                if (IsJsonDifferent(CurrentState, newState, out var diff))
                {
                    if (CurrentState != null)
                    {
                        // Add a queue entry to send an alert.
                        // We don't send an alert when CurrentState == null since this is just our first time polling.
                        var message = new CloudQueueMessage(ResourceId);
                        await OutputQueue.AddMessageAsync(message);
                    }

                    LastUpdatedTime = timeRequested;
                    CurrentState = newState;
                }
            }
            catch (HttpRequestException ex)
            {
                Log.LogError(ex, "Unable to check resource.");

                // Send errors to a special error queue for later handling.
                var message = new CloudQueueMessage(ResourceId); // TODO work out what message should contain
                await ErrorQueue.AddMessageAsync(message);
            }

            // Make a note that we've just checked.
            LastCheckedTime = timeRequested;

            // Schedule the next check.
            Entity.Current.SignalEntity(Entity.Current.EntityId, DateTime.UtcNow.Add(CheckFrequency), nameof(CheckResource));
        }

        private static bool IsJsonDifferent(string currentState, string newState, out string diff)
        {
            if (currentState == newState)
            {
                diff = null;
                return false;
            }
            else if (currentState == null && newState != null)
            {
                diff = newState;
                return true;
            }
            else if (currentState != null && newState == null)
            {
                diff = currentState;
                return true;
            }

            var jsonPatch = new JsonDiffPatch();
            diff = jsonPatch.Diff(currentState, newState);
            return diff != null;
        }
    }
}
