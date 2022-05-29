namespace AzureBatchParallellizationDemo.Settings;

public class BatchJobSettings
{
    public int DedicatedNodeCount { get; set; }
    public int LowPriorityNodeCount { get; set; }
    public string PoolVMSize { get; set; }
    public string AppPackageId { get; set; }
    public string AppPackageVersion { get; set; }
}
