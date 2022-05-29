using Azure.Storage.Sas;
using AzureBatchParallellizationDemo.Extensions;
using AzureBatchParallellizationDemo.Helpers;
using AzureBatchParallellizationDemo.Models;
using AzureBatchParallellizationDemo.Services;
using Microsoft.Azure.Batch;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace AzureBatchParallellizationDemo;

public class App
{
    private readonly ILogger<App> _logger;
    private readonly BatchService _batchService;
    private readonly StorageService _storageService;

    public App(ILogger<App> logger,
        BatchService batchService,
        StorageService storageService)
    {
        _logger = logger.Guard();
        _batchService = batchService.Guard();
        _storageService = storageService.Guard();
    }

    public async Task RunAsync(string[] args)
    {
        _logger.LogInformation("Starting application with args {args}...", string.Join(',', args));

        try
        {
            _logger.LogInformation("Demo start: {demoStart}", DateTime.Now);
            Stopwatch timer = new();
            timer.Start();

            await _storageService.CreateContainersIfNotExistAsync();
            var inputFilePaths = GetInputFilePaths();
            ResourceFile[] resourceFiles = await FileUploadHelper.UploadLocalFiles(_storageService, inputFilePaths);

            Uri outputSasUri = _storageService.GetContainerSasUri(BlobContainerSasPermissions.Write);

            var configuration = BatchJobConfiguration.Default(resourceFiles, outputSasUri);

            using (_batchService)
            {
                await _batchService.StartJobAsync(configuration);
                var jobResult = await _batchService.MonitorTasksAsync(configuration.JobId, TimeSpan.FromMinutes(30));

                _logger.LogInformation("Job result: {jobResult}", jobResult);
                await _batchService.InspectJobResultAsync(configuration.JobId, jobResult);

                timer.Stop();
                _logger.LogInformation("Demo end: {demoEnd}", DateTime.Now);
                _logger.LogInformation("Elapsed time: {demoDuration}", timer.Elapsed);

                _storageService.DeleteInputContainerIfExistsAsync().Wait();

                await CleanUpBatchResourcesOnUserPromptAsync(configuration);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("An error occured: {ex}", ex);
        }

        finally
        {
            Console.WriteLine();
            Console.WriteLine("Sample complete, hit ENTER to exit...");
            Console.ReadLine();
        }

    }

    private static List<string> GetInputFilePaths()
    {
        string inputPath = Path.Combine(Environment.CurrentDirectory, "InputFiles");
        return new(Directory.GetFileSystemEntries(inputPath, "*.mp4", SearchOption.TopDirectoryOnly));
    }

    private async Task CleanUpBatchResourcesOnUserPromptAsync(BatchJobConfiguration configuration)
    {
        Console.Write("Delete job? [yes] no: ");
        string response = Console.ReadLine().ToLower();
        if (response != "n" && response != "no")
        {
            await _batchService.DeleteJobAsync(configuration.JobId);
            _logger.LogInformation("Job {jobId} was deleted by user (input {input})",
                configuration.JobId, response);
        }

        Console.Write("Delete pool? [yes] no: ");
        response = Console.ReadLine().ToLower();
        if (response != "n" && response != "no")
        {
            await _batchService.DeletePoolAsync(configuration.PoolId);
            _logger.LogInformation("Pool {poolId} was deleted by user (input {input})",
                configuration.PoolId, response);
        }
    }

}