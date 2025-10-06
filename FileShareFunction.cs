using Azure;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ABCRetail.Functions
{
    public class FileShareFunction
    {
        private readonly ILogger<FileShareFunction> _logger;
        private readonly string _connectionString;
        private readonly string _fileShareName = "abcfileshare";

        public FileShareFunction(ILogger<FileShareFunction> logger)
        {
            _logger = logger;
            _connectionString = "DefaultEndpointsProtocol=https;AccountName=st10444974;AccountKey=oQWk2OV403Q2geFVdovW18BZpfbomUOR6tnY+obmwiCVulYR0NHTugVLOE78kUUs0X41ARWt0BKY+AStHFNvRg==;EndpointSuffix=core.windows.net";
        }

        [Function("UploadFileToShare")]
        public async Task<IActionResult> UploadFileToShare(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "files/upload")] HttpRequest req)
        {
            _logger.LogInformation("UploadFileToShare function triggered");

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

                // Get directory name from form data or use default
                string directoryName = req.Form["directoryName"].FirstOrDefault() ?? "uploads";

                // Initialize Azure File Share clients
                var serviceClient = new ShareServiceClient(_connectionString);
                var shareClient = serviceClient.GetShareClient(_fileShareName);

                // Create share if it doesn't exist
                await shareClient.CreateIfNotExistsAsync();

                // Get directory client
                var directoryClient = shareClient.GetDirectoryClient(directoryName);
                await directoryClient.CreateIfNotExistsAsync();

                // Get file client
                var fileClient = directoryClient.GetFileClient(file.FileName);

                // Upload file
                using (var stream = file.OpenReadStream())
                {
                    await fileClient.CreateAsync(stream.Length);
                    await fileClient.UploadRangeAsync(
                        new HttpRange(0, stream.Length),
                        stream);
                }

                _logger.LogInformation($"File uploaded successfully: {file.FileName}");

                return new OkObjectResult(new
                {
                    message = "File uploaded successfully",
                    fileName = file.FileName,
                    directoryName = directoryName,
                    fileShareName = _fileShareName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error uploading file: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        [Function("ListFilesInShare")]
        public async Task<IActionResult> ListFilesInShare(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "files")] HttpRequest req)
        {
            _logger.LogInformation("ListFilesInShare function triggered");

            try
            {
                string directoryName = req.Query["directoryName"].FirstOrDefault() ?? "uploads";

                var serviceClient = new ShareServiceClient(_connectionString);
                var shareClient = serviceClient.GetShareClient(_fileShareName);
                var directoryClient = shareClient.GetDirectoryClient(directoryName);

                var files = new List<object>();

                await foreach (ShareFileItem item in directoryClient.GetFilesAndDirectoriesAsync())
                {
                    if (!item.IsDirectory)
                    {
                        var fileClient = directoryClient.GetFileClient(item.Name);
                        var properties = await fileClient.GetPropertiesAsync();

                        files.Add(new
                        {
                            fileName = item.Name,
                            size = properties.Value.ContentLength,
                            lastModified = properties.Value.LastModified
                        });
                    }
                }

                _logger.LogInformation($"Listed {files.Count} files from directory: {directoryName}");

                return new OkObjectResult(files);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error listing files: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        [Function("DownloadFileFromShare")]
        public async Task<IActionResult> DownloadFileFromShare(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "files/download")] HttpRequest req)
        {
            _logger.LogInformation("DownloadFileFromShare function triggered");

            try
            {
                string fileName = req.Query["fileName"];
                string directoryName = req.Query["directoryName"].FirstOrDefault() ?? "uploads";

                if (string.IsNullOrWhiteSpace(fileName))
                {
                    return new BadRequestObjectResult("fileName parameter is required");
                }

                var serviceClient = new ShareServiceClient(_connectionString);
                var shareClient = serviceClient.GetShareClient(_fileShareName);
                var directoryClient = shareClient.GetDirectoryClient(directoryName);
                var fileClient = directoryClient.GetFileClient(fileName);

                var downloadInfo = await fileClient.DownloadAsync();

                _logger.LogInformation($"File downloaded: {fileName}");

                return new FileStreamResult(downloadInfo.Value.Content, "application/octet-stream")
                {
                    FileDownloadName = fileName
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error downloading file: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}