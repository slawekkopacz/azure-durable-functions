using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace FanOutFanIn
{
    public static class FanOutFanInFunctions
    {
        [FunctionName("FanOutFanIn")]
        public static async Task<string> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var parallelTasks = new List<Task<string>>();

            // Get a list of N work items to process in parallel.
            string[] workBatch = await context.CallActivityAsync<string[]>("F1", null);
            for (int i = 0; i < workBatch.Length; i++)
            {
                Task<string> task = context.CallActivityAsync<string>("F2", workBatch[i]);
                parallelTasks.Add(task);
            }

            await Task.WhenAll(parallelTasks);

            // Aggregate all N outputs and send the result to F3.
            var words = parallelTasks.Select(t => t.Result).ToArray();
            return await context.CallActivityAsync<string>("F3", words);
        }

        [FunctionName("F1")]
        public static string[] F1([ActivityTrigger] object input, ILogger log)
        {
            log.LogInformation("Generating words..");
            return new[]
            {
                "oak",
                "maple",
                "ash",
                "rosewood",
            };
        }

        [FunctionName("F2")]
        public static async Task<string> F2([ActivityTrigger] string word, ILogger log)
        {
            return word.ToUpper();
        }

        [FunctionName("F3")]
        public static async Task<string> F3([ActivityTrigger] string[] words, ILogger log)
        {
            return string.Join(", ", words);
        }

        [FunctionName("FanOutFanIn_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("FanOutFanIn", null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}
