using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Order_Items_Reserver
{
    public static class OrderItemsReserver
    {
        const string QueueName = "saveorderqueue";
        private static HttpClient httpClient = new HttpClient();
        [FunctionName("OrderItemsReserver")]
        public static async Task Run([ServiceBusTrigger("saveorderqueue", Connection = "AzureBusConnectionString")]string myQueueItem, ILogger log)
        {
            log.LogInformation($"C# ServiceBus queue trigger function processed message: {myQueueItem}");
            var options = new BlobClientOptions();
            options.Diagnostics.IsLoggingEnabled = false;
            options.Diagnostics.IsTelemetryEnabled = false;
            options.Diagnostics.IsDistributedTracingEnabled = false;
            options.Retry.MaxRetries = 3;
            try
            {
                BlobServiceClient blobServiceClient = new BlobServiceClient("DefaultEndpointsProtocol=https;AccountName=myesstorageacc;AccountKey=y6uhAMJUJO0aL/1I/YiQycRTlGpRobC5smo7prAmbzej0G4I5BOjwMtz04txjGYIJo7+SRduv+YBcbUCUoEHYw==;EndpointSuffix=core.windows.net", options);
                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient("eshopcontainer");
                BlobClient blobClient = containerClient.GetBlobClient("Testing"); // Will create a new one with guid

                string requestBody = myQueueItem;
                log.LogInformation(requestBody);
                dynamic data = JsonConvert.DeserializeObject(requestBody);
                log.LogInformation("uploading.");
                var content = Encoding.UTF8.GetBytes(requestBody);
                using (var ms = new MemoryStream(content))
                    blobClient.Upload(ms);
                log.LogInformation($"This was upLoaded to blob storage");
            }
            catch (Exception ex)
            {
                var obj = new { message = ex.Message };
                var content = new StringContent(JsonConvert.SerializeObject(obj), Encoding.UTF8, "application/json");
                string logicAppUrl = "https://esemaillogicapp.azurewebsites.net:443/api/MyEmailLogicWorkflow/triggers/manual/invoke?api-version=2020-05-01-preview&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig=1w1udAt01tFgpkGk7l1m-eAgUoA4PQhv5NVN48HOzZc";
                var response = await httpClient.PostAsync(logicAppUrl, content);
            }
        }
    }



}
