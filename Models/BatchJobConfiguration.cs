using Microsoft.Azure.Batch;
using System;

namespace AzureBatchParallellizationDemo.Models;

public class BatchJobConfiguration
{
    public string JobId { get; }
    public string PoolId { get; }
    public ResourceFile[] ResourceFiles { get; }
    public Uri OutputSasUri { get; }

    private BatchJobConfiguration(string jobId,
        string poolId, ResourceFile[] resourceFiles, Uri outputSasUri)
    {
        JobId = jobId;
        PoolId = poolId;
        ResourceFiles = resourceFiles;
        OutputSasUri = outputSasUri;
    }

    public static BatchJobConfiguration Default(ResourceFile[] resourceFiles, Uri outputSasUri)
    {
        return new BatchJobConfiguration("batchDemoJob", "batchDemoPool", resourceFiles, outputSasUri);
    }

    public static BatchJobConfiguration Create(string jobId, string poolId,
        ResourceFile[] resourceFiles, Uri outputSasUri)
    {
        return new BatchJobConfiguration(jobId, poolId, resourceFiles, outputSasUri);
    }
}
