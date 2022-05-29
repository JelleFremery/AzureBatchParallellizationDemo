using AzureBatchParallellizationDemo.Services;
using Microsoft.Azure.Batch;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AzureBatchParallellizationDemo.Helpers;

public static class FileUploadHelper
{
    public static async Task<ResourceFile[]> UploadLocalFiles(StorageService storageService, List<string> inputFilePaths)
    {
        var uploadTasks = inputFilePaths.Select(filePath =>
                        storageService.UploadInputFileToContainerAsync(filePath)
                    );

        var resourceFiles = await Task.WhenAll(uploadTasks);
        return resourceFiles;
    }
}