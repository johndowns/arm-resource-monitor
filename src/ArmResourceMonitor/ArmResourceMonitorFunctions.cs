using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System;
using System.Text.RegularExpressions;
using SendGrid.Helpers.Mail;
using SendGrid;

namespace ArmResourceMonitor
{
    public class AddResourceMonitorRequest
    {
        public string ResourceId { get; set; }

        public string ApiVersion { get; set; }

        public string CheckFrequency { get; set; } = TimeSpan.FromDays(1).ToString();
    }

    public static class ArmResourceMonitorFunctions
    {
        private static string SendGridApiKey = Environment.GetEnvironmentVariable("SendGridApiKey");
        private static string EmailFromAddress = Environment.GetEnvironmentVariable("EmailFromAddress");
        private static string EmailFromName = Environment.GetEnvironmentVariable("EmailFromName");
        private static string EmailToAddress = Environment.GetEnvironmentVariable("EmailToAddress");
        private static string EmailToName = Environment.GetEnvironmentVariable("EmailToName");

        private static readonly Regex DisallowedCharsInTableKeys = new Regex(@"[\\\\#%+/?\u0000-\u001F\u007F-\u009F]");
        private static readonly SendGridClient EmailClient = new SendGridClient(SendGridApiKey);

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
                return new BadRequestObjectResult(new
                {
                    error = "apiVersion must be specified."
                });
            }
            TimeSpan checkFrequency;
            if (! TimeSpan.TryParse(request.CheckFrequency, out checkFrequency))
            {
                return new BadRequestObjectResult(new
                {
                    error = "checkFrequency is invalid."
                });
            }                

            // Initialize the monitor entity.
            var resourceMonitorId = DisallowedCharsInTableKeys.Replace(request.ResourceId, "|");
            var entityId = new EntityId(nameof(ResourceMonitorEntity), resourceMonitorId);
            await entityClient.SignalEntityAsync(entityId, nameof(ResourceMonitorEntity.Initialize), (request.ResourceId, request.ApiVersion, checkFrequency));

            return new OkResult();
        }

        [FunctionName("ProcessResourceUpdate")]
        public static Task ProcessResourceUpdate(
            [QueueTrigger("resource-updated", Connection = "AzureWebJobsStorage")] string resourceUpdatedMessage,
            ILogger log)
        {
            // TODO
            return SendEmail(resourceUpdatedMessage, log);
        }

        [FunctionName("ProcessResourceUpdateError")]
        public static Task ProcessResourceUpdateError(
            [QueueTrigger("resource-update-error", Connection = "AzureWebJobsStorage")] string errorMessage,
            ILogger log)
        {
            // TODO
            return SendEmail(errorMessage, log);
        }

        private static async Task SendEmail(string body, ILogger log)
        {
            // Prepare the email message.
            var emailMessage = new SendGridMessage();
            emailMessage.SetFrom(new EmailAddress(EmailFromAddress, EmailFromName));
            emailMessage.AddTo(new EmailAddress(EmailToAddress, EmailToName));
            emailMessage.Subject = "TODO";
            emailMessage.PlainTextContent = body;

            // Send the message.
            var response = await EmailClient.SendEmailAsync(emailMessage);
            log.LogInformation("Sent mail via SendGrid and received status code {SendGridStatusCode} and headers {SendGridHeaders}.", response.StatusCode, response.Headers.ToString());
            
            if ((int)response.StatusCode > 299)
            {
                var responseBody = await response.Body.ReadAsStringAsync();
                log.LogError("Received error from SendGrid: {SendGridErrorBody}", responseBody);
                throw new Exception("Unable to send email.");
            }
        }
    }
}
