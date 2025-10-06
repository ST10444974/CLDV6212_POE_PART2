using Azure.Data.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ABCRetail.Functions
{
    public class CustomerFunction
    {
        private readonly ILogger<CustomerFunction> _logger;
        private readonly TableClient _tableClient;

        public CustomerFunction(ILogger<CustomerFunction> logger)
        {
            _logger = logger;

            // Get connection string from environment variables
            var connectionString = "DefaultEndpointsProtocol=https;AccountName=st10444974;AccountKey=oQWk2OV403Q2geFVdovW18BZpfbomUOR6tnY+obmwiCVulYR0NHTugVLOE78kUUs0X41ARWt0BKY+AStHFNvRg==;EndpointSuffix=core.windows.net";
            _tableClient = new TableClient(connectionString, "Storage");
        }

        [Function("AddCustomer")]
        public async Task<IActionResult> AddCustomer(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "customers")] HttpRequest req)
        {
            _logger.LogInformation("AddCustomer function triggered");

            try
            {
                // Read the request body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var customerData = JsonSerializer.Deserialize<CustomerDto>(requestBody);

                if (customerData == null)
                {
                    return new BadRequestObjectResult("Invalid customer data");
                }

                // Create table entity
                var customerEntity = new TableEntity("CustomerPartition", Guid.NewGuid().ToString())
                {
                    { "CustomerName", customerData.CustomerName },
                    { "Email", customerData.Email },
                    { "Phone", customerData.Phone ?? string.Empty },
                    { "Address", customerData.Address ?? string.Empty }
                };

                // Add to table storage
                await _tableClient.AddEntityAsync(customerEntity);

                _logger.LogInformation($"Customer {customerData.CustomerName} added successfully");

                return new OkObjectResult(new
                {
                    message = "Customer added successfully",
                    rowKey = customerEntity.RowKey
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error adding customer: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }

    // DTO for deserializing request
    public class CustomerDto
    {
        public string CustomerName { get; set; }
        public string Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
    }
}