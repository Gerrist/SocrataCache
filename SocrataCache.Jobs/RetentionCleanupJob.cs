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

        var knownDatasets = await _datasetManager.GetDatasets();

        var overThresholdDatasets = knownDatasets
            .Where(dataset =>
                ShouldFlagForDeletion(retentionAgeDays, dataset.CreatedAt) &&
                dataset.Status == DatasetStatus.Downloaded);

        var overThresholdDatasetsCount = overThresholdDatasets.Count();

        _logger.LogInformation("Found {Count} dataset{CountPlural} older than {RetentionDays} day{DayPlural}",
            overThresholdDatasetsCount, overThresholdDatasetsCount == 1 ? "" : "s", retentionAgeDays,
            retentionAgeDays == 1 ? "" : "s");

        var datasetsDirectory = Env.DownloadsRootPath.Value;
        var deletedFiles = 0;

        foreach (var dataset in overThresholdDatasets)
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
                deletedFiles++;
            }

            if (File.Exists(filePathDownloadCompressed))
            {
                File.Delete(filePathDownloadCompressed);
                deletedFiles++;
            }

            await _datasetManager.UpdateDatasetStatus(dataset.DatasetId, DatasetStatus.Deleted);
        }

        return deletedFiles;
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

        var knownDatasets = await _datasetManager.GetDatasets();

        var downloadedDatasets = knownDatasets
            .Where(dataset => dataset.Status == DatasetStatus.Downloaded);

        foreach (var dataset in downloadedDatasets)
        {
            _logger.LogDebug("Possibly dataset {Dataset}", dataset);

            if (overThresholdSize <= 0)
            {
                _logger.LogInformation("Dataset volume not exceeding retention size threshold anymore: {Remaining}",
                    overThresholdSize);
                break;
            }

            _logger.LogDebug("Deleting dataset because of overThresholdSize: {ThresholdSize}", overThresholdSize);

            var combinedFileSizeMb = 0;

            var fileNameDownload = $"{dataset.ResourceId}-{dataset.DatasetId}.{dataset.Type}";
            var fileNameDownloadCompressed = $"{dataset.ResourceId}-{dataset.DatasetId}.{dataset.Type}.gz";

            var filePathDownload = $"{datasetsDirectory}/{fileNameDownload}";
            var filePathDownloadCompressed = $"{datasetsDirectory}/{fileNameDownloadCompressed}";

            _logger.LogDebug("Checking if normal file at '{Path}' exists'", filePathDownload);

            if (File.Exists(filePathDownload))
            {
                _logger.LogDebug("Normal file at '{Path}' exists'", filePathDownload);

                var fileInfo = new FileInfo(filePathDownload);

                combinedFileSizeMb = +Convert.ToInt16(Math.Ceiling((double)fileInfo.Length / 1024 / 1024));
                deletedFilesCount++;

                File.Delete(filePathDownload);

                _logger.LogInformation("Deleted file {FileName}", fileNameDownload);
            }
            else
            {
                _logger.LogDebug("Normal file at '{Path}' does NOT exists'", filePathDownload);
            }

            _logger.LogDebug("Checking if compressed file at '{Path}' exists'", filePathDownloadCompressed);

            if (File.Exists(filePathDownloadCompressed))
            {
                _logger.LogDebug("Compressed file at '{Path}' exists'", filePathDownloadCompressed);

                var fileInfo = new FileInfo(filePathDownloadCompressed);

                combinedFileSizeMb = +Convert.ToInt16(Math.Ceiling((double)fileInfo.Length / 1024 / 1024));
                deletedFilesCount++;

                File.Delete(filePathDownload);

                _logger.LogInformation("Deleted file {FileName}", filePathDownloadCompressed);
            }
            else
            {
                _logger.LogDebug("Compressed file at '{Path}' does NOT exists'", filePathDownload);
            }

            await _datasetManager.UpdateDatasetStatus(dataset.DatasetId, DatasetStatus.Deleted);

            deletedFilesSizeMb += combinedFileSizeMb;
            overThresholdSize -= combinedFileSizeMb;
        }

        _logger.LogInformation("Deleted {DeletedFilesMb} MB", deletedFilesSizeMb);

        return deletedFilesCount;
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