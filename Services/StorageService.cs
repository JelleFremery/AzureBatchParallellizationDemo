using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using AzureBatchParallellizationDemo.Extensions;
using AzureBatchParallellizationDemo.Settings;
using Microsoft.Azure.Batch;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AzureBatchParallellizationDemo.Services;

public class StorageService
{
    private readonly ILogger<StorageService> _logger;
    private readonly StorageSettings _storageSettings;
    private readonly BlobServiceClient _blobServiceClient;

    public StorageService(ILogger<StorageService> logger, IOptions<StorageSettings> storageOptions)
    {
        _logger = logger.Guard();
        _storageSettings = storageOptions.GuardedValue();
        _blobServiceClient = GetBlobServiceClient();
    }

    public async Task CreateContainersIfNotExistAsync()
    {
        await CreateContainerIfNotExistAsync(_storageSettings.InputContainerName);
        await CreateContainerIfNotExistAsync(_storageSettings.OutputContainerName);
    }

    public async Task<ResourceFile> UploadInputFileToContainerAsync(string filePath)
    {
        var containerName = _storageSettings.InputContainerName;
        _logger.LogInformation("Uploading file {0} to container [{1}]...", filePath,
            containerName);

        string blobName = Path.GetFileName(filePath);

        filePath = Path.Combine(Environment.CurrentDirectory, filePath);

        BlobContainerClient blobContainerCLient = _blobServiceClient.GetBlobContainerClient(containerName);
        BlobClient blobClient = blobContainerCLient.GetBlobClient(blobName);
        await blobClient.UploadAsync(filePath, true);

        Uri sasBlobToken = blobClient.GenerateSasUri(BlobSasPermissions.Read, DateTime.UtcNow.AddHours(2));
        var url = sasBlobToken.AbsoluteUri;
        return ResourceFile.FromUrl(url, filePath);
    }

    public Uri GetContainerSasUri(BlobContainerSasPermissions permissions)
    {
        BlobContainerClient container = _blobServiceClient.GetBlobContainerClient(_storageSettings.OutputContainerName);
        Uri sasUri = container.GenerateSasUri(permissions, DateTimeOffset.UtcNow.Add(TimeSpan.FromHours(2)));
        return sasUri;
    }

    public async Task DeleteInputContainerIfExistsAsync()
    {
        BlobContainerClient container = _blobServiceClient.GetBlobContainerClient(_storageSettings.InputContainerName);
        await container.DeleteIfExistsAsync();
        _logger.LogInformation("Container [{0}] deleted.", _storageSettings.InputContainerName);
    }

    private BlobServiceClient GetBlobServiceClient()
    {
        string storageConnectionString = string.Format(
            "DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}",
                _storageSettings.Name,
                _storageSettings.Key);
        return new BlobServiceClient(storageConnectionString);
    }

    private async Task CreateContainerIfNotExistAsync(string containerName)
    {
        BlobContainerClient container = _blobServiceClient.GetBlobContainerClient(containerName);
        await container.CreateIfNotExistsAsync();
        _logger.LogInformation("Creating container [{0}].", containerName);
    }
}
