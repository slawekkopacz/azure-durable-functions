using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Chaining
{
    public static class ChainingFunctions
    {
        [FunctionName("Chaining")]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var outputs = new List<string>();

            // Replace "hello" with the name of your Durable Activity Function.
            var task1 = context.CallActivityAsync<string>("Chaining_Hello", "Tokyo");
            var task2 = context.CallActivityAsync<string>("Chaining_Hello", "Seattle");
            var task3 = context.CallActivityAsync<string>("Chaining_Hello", "London");

            outputs.Add(await task1);
            outputs.Add(await task2);
            outputs.Add(await task3);

            var x = await context.CallActivityAsync<string>("Chaining_Hello", "X");
            var y = await context.CallActivityAsync<string>("Chaining_Hello", x);
            var z = await context.CallActivityAsync<string>("Chaining_Hello", y);

            outputs.Add(z);
            // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
            return outputs;
        }

        [FunctionName("Chaining_Hello")]
        public static string SayHello([ActivityTrigger] string name, ILogger log)
        {
            log.LogInformation($"Saying hello to {name}.");
            return $"Hello {name}!";
        }

        [FunctionName("Chaining_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("Chaining", null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}
