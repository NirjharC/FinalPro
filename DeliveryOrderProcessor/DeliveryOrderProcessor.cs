using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.Azure.Cosmos;

namespace DeliveryOrderProcessor
{
    public static class DeliveryOrderProcessor
    {
        private static readonly string _endpointUri = "https://mycosmosaccount666.documents.azure.com:443/";
        private static readonly string _primaryKey = "PgQRr9sJ8FbL97j2LBP9h7X1SIxOkDtNs0UmCpq1Ymb1QJoQKDMNcJhqipGbEYvdntVwnyQlUhY7KSNIsD330w==";
        [FunctionName("DeliveryOrderProcessor")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string databaseName = "OrderDetails";
            string containerName = "Orders";
            string account = _endpointUri;
            string key = _primaryKey;

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Order items = JsonConvert.DeserializeObject<Order>(requestBody);

            // Preparing to create cosmos DB
            Microsoft.Azure.Cosmos.CosmosClient client = new Microsoft.Azure.Cosmos.CosmosClient(account, key);

            Microsoft.Azure.Cosmos.DatabaseResponse database = await client.CreateDatabaseIfNotExistsAsync(databaseName);
            await database.Database.CreateContainerIfNotExistsAsync(containerName, "/id");

            Container _container = client.GetContainer(databaseName, containerName);
            await _container.CreateItemAsync<Order>(items, new PartitionKey(items.BuyerId));
            string responseMessage = " This HTTP triggered function executed successfully.";

            return new OkObjectResult(responseMessage);
        }
    }
}
