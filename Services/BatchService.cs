using AzureBatchParallellizationDemo.Extensions;
using AzureBatchParallellizationDemo.Models;
using AzureBatchParallellizationDemo.Settings;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace AzureBatchParallellizationDemo.Services;

public class BatchService : IDisposable
{
    private readonly ILogger<BatchService> _logger;
    private readonly BatchSettings _batchSettings;
    private readonly BatchJobSettings _batchJobSettings;
    private readonly BatchClient _batchClient;

    public BatchService(ILogger<BatchService> logger,
        IOptions<BatchSettings> batchOptions,
        IOptions<BatchJobSettings> batchJobOptions)
    {
        _logger = logger;
        _batchSettings = batchOptions.GuardedValue();
        _batchJobSettings = batchJobOptions.GuardedValue();
        _batchClient = BatchClient.Open(new BatchSharedKeyCredentials(
            _batchSettings.Url, _batchSettings.Name, _batchSettings.Key));
    }

    public async Task StartJobAsync(BatchJobConfiguration batchJobConfiguration)
    {
        await CreatePoolIfNotExistsAsync(batchJobConfiguration.PoolId);
        await CreateJobAsync(batchJobConfiguration.JobId, batchJobConfiguration.PoolId);
        await AddTasksAsync(batchJobConfiguration);
    }

    public async Task<JobResult> MonitorTasksAsync(string jobId, TimeSpan timeout)
    {
        const string timeOutMessage = "One or more tasks failed to reach the Completed state within the timeout period.";

        ODATADetailLevel detail = new(selectClause: "id");
        List<CloudTask> addedTasks = await _batchClient.JobOperations.ListTasks(jobId, detail).ToListAsync();

        _logger.LogInformation("Monitoring all tasks for 'Completed' state, timeout in {timeOut}...", timeout.ToString());

        TaskStateMonitor taskStateMonitor = _batchClient.Utilities.CreateTaskStateMonitor();

        try
        {
            await taskStateMonitor.WhenAll(addedTasks, TaskState.Completed, timeout);
        }
        catch (TimeoutException)
        {
            await _batchClient.JobOperations.TerminateJobAsync(jobId);
            Console.WriteLine(timeOutMessage);
            return JobResult.TimedOut;
        }

        return JobResult.Completed;
    }

    public async Task InspectJobResultAsync(string jobId, JobResult jobResult)
    {
        if (jobResult == JobResult.Completed)
        {
            IEnumerable<CloudTask> completedtasks = _batchClient.JobOperations.ListTasks(jobId);
            foreach (CloudTask task in completedtasks)
            {
                string nodeId = string.Format(task.ComputeNodeInformation.ComputeNodeId);
                _logger.LogInformation("Task: {task}, Node: {node}, Status: {status}",
                    task.Id, nodeId, task.ExecutionInformation?.Result?.ToString());

                if (task.ExecutionInformation.Result == TaskExecutionResult.Failure)
                {
                    _logger.LogError("Errors: {errors}", task.GetNodeFile(Constants.StandardErrorFileName).ReadAsString());
                }
                else
                {
                    _logger.LogDebug("Output: {output}", task.GetNodeFile(Constants.StandardOutFileName).ReadAsString());
                }
            }
        }

        await _batchClient.JobOperations.TerminateJobAsync(jobId);
        _logger.LogInformation("Job [{jobId}] was terminated.", jobId);
    }

    public async Task DeleteJobAsync(string jobId)
    {
        await _batchClient.JobOperations.DeleteJobAsync(jobId);
    }

    public async Task DeletePoolAsync(string poolId)
    {
        await _batchClient.PoolOperations.DeletePoolAsync(poolId);
    }

    private async Task CreateJobAsync(string jobId, string poolId)
    {
        Console.WriteLine("Creating job [{0}]...", jobId);

        CloudJob job = _batchClient.JobOperations.CreateJob();
        job.Id = jobId;
        job.PoolInformation = new PoolInformation { PoolId = poolId };

        await job.CommitAsync();
    }

    private async Task CreatePoolIfNotExistsAsync(string poolId)
    {
        try
        {
            _logger.LogInformation("Creating pool [{poolId}]...", poolId);

            ImageReference imageReference = new(
                    publisher: "MicrosoftWindowsServer",
                    offer: "WindowsServer",
                    sku: "2012-R2-Datacenter-smalldisk",
                    version: "latest");

            VirtualMachineConfiguration virtualMachineConfiguration = new(
                imageReference: imageReference,
                nodeAgentSkuId: "batch.node.windows amd64");

            // Create an unbound pool. No pool is actually created in the Batch service until we call
            // CloudPool.Commit(). This CloudPool instance is therefore considered "unbound," and we can
            // modify its properties.
            CloudPool pool = _batchClient.PoolOperations.CreatePool(
                poolId: poolId,
                targetDedicatedComputeNodes: _batchJobSettings.DedicatedNodeCount,
                targetLowPriorityComputeNodes: _batchJobSettings.LowPriorityNodeCount,
                virtualMachineSize: _batchJobSettings.PoolVMSize,
                virtualMachineConfiguration: virtualMachineConfiguration);

            // Specify the application and version to install on the compute nodes.
            // This assumes that a zipfile matching the id and version has
            // already been added to Batch account.
            pool.ApplicationPackageReferences = new List<ApplicationPackageReference>
            {
                new ApplicationPackageReference
                {
                    ApplicationId = _batchJobSettings.AppPackageId,
                    Version = _batchJobSettings.AppPackageVersion
                }
            };

            await pool.CommitAsync();
        }
        catch (BatchException be)
        {
            // Accept the specific error code PoolExists as that is expected if the pool already exists
            if (be.RequestInformation?.BatchError?.Code == BatchErrorCodeStrings.PoolExists)
            {
                _logger.LogInformation("The pool [{poolId}] already exists.", poolId);
            }
            else
            {
                throw; // Any other exception is unexpected
            }
        }
    }

    private async Task<List<CloudTask>> AddTasksAsync(BatchJobConfiguration batchJobConfiguration)
    {
        var jobId = batchJobConfiguration.JobId;
        var inputFiles = batchJobConfiguration.ResourceFiles;
        var outputSasUri = batchJobConfiguration.OutputSasUri;
        _logger.LogInformation("Adding {taskCount} tasks to job [{jobId}]...", inputFiles.Length, jobId);

        List<CloudTask> tasks = new();

        for (int i = 0; i < inputFiles.Length; i++)
        {
            string taskId = string.Format("Task{0}", i);

            string appPath = string.Format("%AZ_BATCH_APP_PACKAGE_{0}#{1}%",
                _batchJobSettings.AppPackageId,
                _batchJobSettings.AppPackageVersion);
            string inputMediaFile = inputFiles[i].FilePath;
            string outputMediaFile = string.Format("{0}{1}",
                Path.GetFileNameWithoutExtension(inputMediaFile),
                ".mp3");

            string taskCommandLine = string.Format(
                "cmd /c {0}\\ffmpeg-3.4-win64-static\\bin\\ffmpeg.exe -i {1} {2}",
                appPath, inputMediaFile, outputMediaFile);

            CloudTask task = new(taskId, taskCommandLine)
            {
                ResourceFiles = new List<ResourceFile> { inputFiles[i] }
            };

            OutputFileBlobContainerDestination outputContainer = new(outputSasUri.ToString());
            OutputFile outputFile = new(outputMediaFile,
                new OutputFileDestination(outputContainer),
                new OutputFileUploadOptions(OutputFileUploadCondition.TaskSuccess));
            task.OutputFiles = new List<OutputFile> { outputFile };
            tasks.Add(task);
        }

        await _batchClient.JobOperations.AddTaskAsync(jobId, tasks);
        return tasks;
    }

    void IDisposable.Dispose()
    {
        _batchClient?.Dispose();
        GC.SuppressFinalize(this);
    }
}
