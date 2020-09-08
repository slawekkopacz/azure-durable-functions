using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace DurableEntityAggregator
{
    public static class CounterFunctions
    {
        [FunctionName("Flow")]
        public static async Task RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            [DurableClient] IDurableEntityClient entityClient)
        {

            IList<Task> tasks = new List<Task>();
            // The "Counter/{metricType}" entity is created on-demand.
            var entityA = new EntityId("Counter", "A");
            var entityB = new EntityId("Counter", "B");
            var entityC = new EntityId("Counter", "C");

            await entityClient.SignalEntityAsync(entityA, "Reset");
            await entityClient.SignalEntityAsync(entityB, "Reset");
            await entityClient.SignalEntityAsync(entityC, "Reset");


            tasks.Add(entityClient.SignalEntityAsync(entityA, "Add", 1));
            tasks.Add(entityClient.SignalEntityAsync(entityA, "Add", 2));
            tasks.Add(entityClient.SignalEntityAsync(entityA, "Add", 3));
            tasks.Add(entityClient.SignalEntityAsync(entityB, "Add", 11));
            tasks.Add(entityClient.SignalEntityAsync(entityB, "Add", 12));
            tasks.Add(entityClient.SignalEntityAsync(entityB, "Add", 13));
            tasks.Add(entityClient.SignalEntityAsync(entityC, "Add", 21));
            tasks.Add(entityClient.SignalEntityAsync(entityC, "Add", 22));
            tasks.Add(entityClient.SignalEntityAsync(entityC, "Add", 23));

            await Task.WhenAll(tasks);
            await entityClient.SignalEntityAsync(entityB, "Reset");
        }

        [FunctionName("Flow_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("Flow", null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName("Results_Http")]
        public static async Task<int> GetResults(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableEntityClient entityClient,
            ILogger log)
        {
            return (await entityClient.ReadEntityStateAsync<Counter>(new EntityId("Counter", "A"))).EntityState
                .CurrentValue;
        }
    }
}
