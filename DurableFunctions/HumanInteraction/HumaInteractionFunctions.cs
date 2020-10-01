using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace HumanInteraction
{
    public static class HumanInteractionFunctions
    {
        [FunctionName("ApprovalWorkflow")]
        public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            await context.CallActivityAsync("RequestApproval", null);
            using (var timeoutCts = new CancellationTokenSource())
            {
                DateTime dueTime = context.CurrentUtcDateTime.AddMinutes(2);
                Task durableTimeout = context.CreateTimer(dueTime, timeoutCts.Token);

                Task<bool> approvalEvent = context.WaitForExternalEvent<bool>("ApprovalEvent");
                if (approvalEvent == await Task.WhenAny(approvalEvent, durableTimeout))
                {
                    timeoutCts.Cancel();
                    await context.CallActivityAsync("ProcessApproval", approvalEvent.Result);
                }
                else
                {
                    await context.CallActivityAsync("Escalate", null);
                }
            }
        }

        [FunctionName("RequestApproval")]
        public static void RequestApproval([ActivityTrigger] object obj, ILogger log)
        {
            log.LogInformation("Approval request was sent.");
        }

        [FunctionName("ProcessApproval")]
        public static void ProcessApproval([ActivityTrigger] bool isApproved, ILogger log)
        {
            log.LogInformation($"The approval process ({isApproved}) is finished.");
        }

        [FunctionName("Escalate")]
        public static void Escalate([ActivityTrigger] object obj, ILogger log)
        {
            log.LogInformation("Escalation was sent.");
        }

        [FunctionName("ApprovalWorkflow_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("ApprovalWorkflow", null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName("RaiseEventToOrchestration_Http")]
        public static async Task RaiseEvent(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req,
            [DurableClient] IDurableOrchestrationClient client,
            ILogger log)
        {
            bool isApproved = true;
            await client.RaiseEventAsync(req.Query["instanceId"], "ApprovalEvent", isApproved);
            log.LogInformation("Event raised.");
        }
    }
}
