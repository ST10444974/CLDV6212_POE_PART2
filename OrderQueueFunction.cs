using Azure.Data.Tables;
using Azure.Storage.Queues;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ABCRetail.Functions
{
    public class OrderQueueFunction
    {
        private readonly ILogger<OrderQueueFunction> _logger;
        private readonly QueueClient _queueClient;
        private readonly TableClient _tableClient;

        public OrderQueueFunction(ILogger<OrderQueueFunction> logger)
        {
            _logger = logger;
            var connectionString = "DefaultEndpointsProtocol=https;AccountName=st10444974;AccountKey=oQWk2OV403Q2geFVdovW18BZpfbomUOR6tnY+obmwiCVulYR0NHTugVLOE78kUUs0X41ARWt0BKY+AStHFNvRg==;EndpointSuffix=core.windows.net";

            // Initialize Queue Client (don't create here - Azure Functions manages the queue)
            _queueClient = new QueueClient(connectionString, "orders");

            // Initialize Table Client
            _tableClient = new TableClient(connectionString, "Storage");
        }

        // HTTP Function - Add order to queue
        [Function("AddOrderToQueue")]
        public async Task<IActionResult> AddOrderToQueue(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "orders")] HttpRequest req)
        {
            _logger.LogInformation("AddOrderToQueue function triggered");

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var orderData = JsonSerializer.Deserialize<OrderDto>(requestBody);

                if (orderData == null)
                {
                    return new BadRequestObjectResult("Invalid order data");
                }

                // Ensure queue exists before sending
                await _queueClient.CreateIfNotExistsAsync();

                // Create message with order details
                var messageContent = JsonSerializer.Serialize(orderData);

                // Send to queue
                await _queueClient.SendMessageAsync(messageContent);

                _logger.LogInformation($"Order queued: Customer {orderData.CustomerRowKey}, Product {orderData.ProductRowKey}");

                return new OkObjectResult(new
                {
                    message = "Order added to queue successfully",
                    queueName = "orders"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error adding order to queue: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        // Queue Trigger Function - Process queue and write to Table Storage
        [Function("ProcessOrderQueue")]
        public void ProcessOrderQueue(
            [QueueTrigger("orders", Connection = "AzureWebJobsStorage")] string queueMessage)
        {
            _logger.LogInformation("========================================");
            _logger.LogInformation("QUEUE TRIGGER FIRED!!!");
            _logger.LogInformation($"Message: {queueMessage}");
            _logger.LogInformation("========================================");

            try
            {
                if (string.IsNullOrEmpty(queueMessage))
                {
                    _logger.LogError("Queue message is null or empty");
                    return;
                }

                // Deserialize the queue message
                var orderData = JsonSerializer.Deserialize<OrderDto>(queueMessage, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (orderData == null)
                {
                    _logger.LogError("Failed to deserialize order data from queue");
                    return;
                }

                _logger.LogInformation($"Deserialized order: Customer={orderData.CustomerRowKey}, Product={orderData.ProductRowKey}, Qty={orderData.Quantity}");

                // Create table entity for order
                var orderEntity = new TableEntity("OrderPartition", Guid.NewGuid().ToString())
                {
                    { "CustomerRowKey", orderData.CustomerRowKey },
                    { "ProductRowKey", orderData.ProductRowKey },
                    { "Quantity", orderData.Quantity }
                };

                // Write to Table Storage (synchronous for testing)
                _tableClient.AddEntity(orderEntity);

                _logger.LogInformation($"✓ Order saved to Table Storage: RowKey={orderEntity.RowKey}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR in ProcessOrderQueue: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }
    }

    // DTO for Order
    public class OrderDto
    {
        public string CustomerRowKey { get; set; }
        public string ProductRowKey { get; set; }
        public int Quantity { get; set; }
    }
}