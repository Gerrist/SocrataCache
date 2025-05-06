using Microsoft.Extensions.Logging;
using Quartz;
using SocrataCache.Config;
using SocrataCache.Managers.Models;

namespace SocrataCache.Jobs;

public class RetentionCleanupJob : IJob
{
    private readonly Config.Config _config;
    private readonly Managers.DatasetManager _datasetManager;
    private readonly ILogger<FreshDatasetLookupJob> _logger;

    public RetentionCleanupJob(
        Config.Config config,
        Managers.DatasetManager datasetManager,
        ILogger<FreshDatasetLookupJob> logger)
    {
        _config = config;
        _datasetManager = datasetManager;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Running retention cleanup job");

        var fileCleanupCount = 0;

        fileCleanupCount += await AgeThresholdCleanup();
        fileCleanupCount += await SizeThresholdCleanup();

        _logger.LogInformation("Finished cleanup. Deleted {DeletedCount} file{DeletedPlural}", fileCleanupCount,
            fileCleanupCount == 1 ? "" : "s");
    }

    public async Task<int> AgeThresholdCleanup()
    {
        var retentionAgeDays = _config.GetRetentionDays();
        _logger.LogInformation("Cleaning up files older than {Day} day{DayPlural}", retentionAgeDays,
            retentionAgeDays == 1 ? "" : "s");

        var allKnownDatasets = await _datasetManager.GetDatasets();

        var deletableCandidatesByAge = allKnownDatasets
            .Where(dataset =>
                ShouldFlagForDeletion(retentionAgeDays, dataset.CreatedAt) &&
                dataset.Status == DatasetStatus.Downloaded)
            .ToList();

        _logger.LogInformation("Found {Count} candidate dataset{CountPlural} older than {RetentionDays} day{DayPlural}",
            deletableCandidatesByAge.Count, deletableCandidatesByAge.Count == 1 ? "" : "s", retentionAgeDays,
            retentionAgeDays == 1 ? "" : "s");

        var datasetsActuallyDeleted = new List<DatasetModel>();
        var datasetsDirectory = Env.DownloadsRootPath.Value;
        var deletedFilesCount = 0;

        var candidatesByResource = deletableCandidatesByAge.GroupBy(d => d.ResourceId);

        foreach (var group in candidatesByResource)
        {
            var resourceId = group.Key;
            var resourceConfig = _config.GetResources().FirstOrDefault(r => r.ResourceId == resourceId);
            // Order by CreatedAt to delete oldest first if we are only deleting some
            var candidatesForThisResource = group.OrderBy(d => d.CreatedAt).ToList(); 

            List<DatasetModel> datasetsToConsiderDeletingForThisResource;

            if (resourceConfig?.RetainLastFile == true)
            {
                var totalDownloadedForThisResource = allKnownDatasets
                    .Count(d => d.ResourceId == resourceId && d.Status == DatasetStatus.Downloaded);
                
                // Number of items from candidatesForThisResource we can delete
                // We want to leave at least 1, so we can delete (totalDownloadedForThisResource - 1)
                // This must be at least 0, and no more than the number of candidates we have.
                var numToDelete = Math.Min(candidatesForThisResource.Count, Math.Max(0, totalDownloadedForThisResource - 1));
                
                datasetsToConsiderDeletingForThisResource = candidatesForThisResource.Take(numToDelete).ToList();

                if (numToDelete < candidatesForThisResource.Count)
                {
                    _logger.LogDebug(
                        "For resource {ResourceId} (retainLastFile=true), {TotalDownloaded} downloaded, {Candidates} candidates. Will delete {NumToDelete} oldest, keeping {NumKept}.",
                        resourceId, totalDownloadedForThisResource, candidatesForThisResource.Count, numToDelete, totalDownloadedForThisResource - numToDelete);
                }
            }
            else
            {
                // Not retaining last file, all candidates for this resource are up for deletion
                datasetsToConsiderDeletingForThisResource = candidatesForThisResource;
            }
            datasetsActuallyDeleted.AddRange(datasetsToConsiderDeletingForThisResource);
        }
        
        _logger.LogInformation("After retainLastFile logic, {Count} dataset{Plural} selected for age-based deletion.", 
            datasetsActuallyDeleted.Count, datasetsActuallyDeleted.Count == 1 ? "" : "s");

        foreach (var dataset in datasetsActuallyDeleted)
        {
            var daysOld = (DateTime.Now - dataset.CreatedAt).TotalDays;
            _logger.LogDebug(
                "Deleting dataset {ResouceID}-{DatasetId} (CreatedAt is {CreatedAt} = {DaysDiff} days old)",
                dataset.ResourceId, dataset.DatasetId, dataset.CreatedAt, daysOld);

            var fileNameDownload = $"{dataset.ResourceId}-{dataset.DatasetId}.{dataset.Type}";
            var fileNameDownloadCompressed = $"{dataset.ResourceId}-{dataset.DatasetId}.{dataset.Type}.gz";

            var filePathDownload = $"{datasetsDirectory}/{fileNameDownload}";
            var filePathDownloadCompressed = $"{datasetsDirectory}/{fileNameDownloadCompressed}";

            if (File.Exists(filePathDownload))
            {
                File.Delete(filePathDownload);
                deletedFilesCount++;
            }

            if (File.Exists(filePathDownloadCompressed))
            {
                File.Delete(filePathDownloadCompressed);
                deletedFilesCount++;
            }

            await _datasetManager.UpdateDatasetStatus(dataset.DatasetId, DatasetStatus.Deleted);
        }

        return deletedFilesCount;
    }

    public async Task<int> SizeThresholdCleanup()
    {
        var retentionSizeGigabytes = _config.GetRetentionSize();

        _logger.LogInformation(
            "Cleaning up files which are outside total size of {RetentionSize} Gigabyte{RetentionPlural}",
            retentionSizeGigabytes, retentionSizeGigabytes == 1 ? "" : "s");

        var datasetsDirectory = Env.DownloadsRootPath.Value;
        var deletedFilesSizeMb = 0;
        var deletedFilesCount = 0;

        var totalSizeGigabytes = DirectorySize(datasetsDirectory) / (1024.0 * 1024.0 * 1024.0);
        var overThresholdSize = Math.Max(0, totalSizeGigabytes - retentionSizeGigabytes);

        _logger.LogInformation(
            "Total size of files in directory: {TotalSizeGigabytes} GB - Over threshold: {OverThresholdSize} GB (Actual diff.: {DiffGB} GB)",
            totalSizeGigabytes, overThresholdSize, totalSizeGigabytes - retentionSizeGigabytes);

        if (overThresholdSize <= 0)
        {
            _logger.LogInformation("Datasets volume not exceeding retention size threshold");
            return deletedFilesCount;
        }

        var allKnownDatasets = await _datasetManager.GetDatasets(); // Fetched once for consistent view
        var datasetsProcessedForDeletionThisRun = new HashSet<string>(); // Track IDs of datasets deleted in this run

        // Iterate over downloaded datasets, ordered by creation date (oldest first)
        var downloadedForIteration = allKnownDatasets
            .Where(dataset => dataset.Status == DatasetStatus.Downloaded)
            .OrderBy(d => d.CreatedAt)
            .ToList();

        foreach (var dataset in downloadedForIteration)
        {
            if (overThresholdSize <= 0)
            {
                _logger.LogInformation("Dataset volume not exceeding retention size threshold anymore: {Remaining} GB to clear.",
                    overThresholdSize);
                break;
            }
            
            var resource = _config.GetResources().FirstOrDefault(r => r.ResourceId == dataset.ResourceId);
            if (resource?.RetainLastFile == true)
            {
                var initialDownloadedCountForResource = allKnownDatasets
                    .Count(d => d.ResourceId == dataset.ResourceId && d.Status == DatasetStatus.Downloaded);

                var alreadyDeletedThisRunForResource = allKnownDatasets
                    .Count(d => d.ResourceId == dataset.ResourceId && datasetsProcessedForDeletionThisRun.Contains(d.DatasetId));
                
                var currentEffectiveDownloadedCount = initialDownloadedCountForResource - alreadyDeletedThisRunForResource;

                if (currentEffectiveDownloadedCount <= 1)
                {
                    _logger.LogDebug(
                        "Skipping size-based deletion of dataset {ResourceID}-{DatasetId} due to retainLastFile. Effective count for resource: {EffectiveCount}",
                        dataset.ResourceId, dataset.DatasetId, currentEffectiveDownloadedCount);
                    continue; 
                }
            }

            _logger.LogDebug("Considering dataset {DatasetId} for size-based deletion. Current overThresholdSize: {ThresholdSize} GB", 
                dataset.DatasetId, overThresholdSize);

            var combinedFileSizeMb = 0;
            bool filesWereDeletedForThisDataset = false;

            var fileNameDownload = $"{dataset.ResourceId}-{dataset.DatasetId}.{dataset.Type}";
            var fileNameDownloadCompressed = $"{dataset.ResourceId}-{dataset.DatasetId}.{dataset.Type}.gz";

            var filePathDownload = $"{datasetsDirectory}/{fileNameDownload}";
            var filePathDownloadCompressed = $"{datasetsDirectory}/{fileNameDownloadCompressed}";

            if (File.Exists(filePathDownload))
            {
                var fileInfo = new FileInfo(filePathDownload);
                var fileSizeMb = (double)fileInfo.Length / 1024 / 1024;
                combinedFileSizeMb += Convert.ToInt32(Math.Ceiling(fileSizeMb));
                
                File.Delete(filePathDownload);
                _logger.LogInformation("Deleted file {FileName} (size: {FileSizeMb}MB) for size threshold cleanup.", fileNameDownload, fileSizeMb.ToString("F2"));
                filesWereDeletedForThisDataset = true;
            }

            if (File.Exists(filePathDownloadCompressed))
            {
                var fileInfo = new FileInfo(filePathDownloadCompressed);
                var fileSizeMb = (double)fileInfo.Length / 1024 / 1024;
                combinedFileSizeMb += Convert.ToInt32(Math.Ceiling(fileSizeMb));
                
                File.Delete(filePathDownloadCompressed); // Corrected from filePathDownload
                 _logger.LogInformation("Deleted file {FileName} (size: {FileSizeMb}MB) for size threshold cleanup.", fileNameDownloadCompressed, fileSizeMb.ToString("F2"));
                filesWereDeletedForThisDataset = true;
            }

            if (filesWereDeletedForThisDataset)
            {
                deletedFilesCount++; // Count datasets processed, not individual files
                datasetsProcessedForDeletionThisRun.Add(dataset.DatasetId);
                await _datasetManager.UpdateDatasetStatus(dataset.DatasetId, DatasetStatus.Deleted);
                
                deletedFilesSizeMb += combinedFileSizeMb;
                overThresholdSize -= (double)combinedFileSizeMb / 1024; // Convert MB to GB for overThresholdSize
            }
        }

        _logger.LogInformation("Deleted {DeletedFilesMb} MB across {DeletedFileCount} dataset(s) for size threshold.", deletedFilesSizeMb, datasetsProcessedForDeletionThisRun.Count);

        return datasetsProcessedForDeletionThisRun.Count;
    }

    private static long DirectorySize(string directoryPath)
    {
        var directory = new DirectoryInfo(directoryPath);

        long size = 0;

        var fis = directory.GetFiles();

        foreach (var fi in fis) size += fi.Length;

        return size;
    }

    private bool ShouldFlagForDeletion(int maxDaysOld, DateTime fileDate)
    {
        var fileAgeInDays = (DateTime.Now - fileDate).TotalDays;

        return fileAgeInDays > maxDaysOld;
    }
}