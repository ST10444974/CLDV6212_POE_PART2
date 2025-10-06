using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ABCRetail.Functions
{
    public class ProductBlobFunction
    {
        private readonly ILogger<ProductBlobFunction> _logger;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _containerName = "product";

        public ProductBlobFunction(ILogger<ProductBlobFunction> logger)
        {
            _logger = logger;
            var connectionString = "DefaultEndpointsProtocol=https;AccountName=st10444974;AccountKey=oQWk2OV403Q2geFVdovW18BZpfbomUOR6tnY+obmwiCVulYR0NHTugVLOE78kUUs0X41ARWt0BKY+AStHFNvRg==;EndpointSuffix=core.windows.net";
            _blobServiceClient = new BlobServiceClient(connectionString);
        }

        [Function("UploadProductImage")]
        public async Task<IActionResult> UploadProductImage(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "products/upload")] HttpRequest req)
        {
            _logger.LogInformation("UploadProductImage function triggered");

            try
            {
                // Check if file exists in request
                if (!req.Form.Files.Any())
                {
                    return new BadRequestObjectResult("No file uploaded");
                }

                var file = req.Form.Files[0];

                if (file.Length == 0)
                {
                    return new BadRequestObjectResult("File is empty");
                }

                // Get or create container
                var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

                // Generate unique filename
                var fileName = $"{Guid.NewGuid()}_{file.FileName}";
                var blobClient = containerClient.GetBlobClient(fileName);

                // Upload file
                using (var stream = file.OpenReadStream())
                {
                    var blobHttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = file.ContentType ?? "application/octet-stream"
                    };

                    await blobClient.UploadAsync(stream, new BlobUploadOptions
                    {
                        HttpHeaders = blobHttpHeaders
                    });
                }

                var blobUrl = blobClient.Uri.ToString();

                _logger.LogInformation($"File uploaded successfully: {blobUrl}");

                return new OkObjectResult(new
                {
                    message = "File uploaded successfully",
                    imageUrl = blobUrl,
                    fileName = fileName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error uploading file: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        [Function("DeleteProductImage")]
        public async Task<IActionResult> DeleteProductImage(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "products/image")] HttpRequest req)
        {
            _logger.LogInformation("DeleteProductImage function triggered");

            try
            {
                string blobUrl = req.Query["blobUrl"];

                if (string.IsNullOrEmpty(blobUrl))
                {
                    return new BadRequestObjectResult("blobUrl parameter is required");
                }

                // Extract blob name from URL
                var uri = new Uri(blobUrl);
                var blobName = uri.Segments[^1];

                var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots);

                _logger.LogInformation($"Blob deleted successfully: {blobName}");

                return new OkObjectResult(new
                {
                    message = "Image deleted successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting blob: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}