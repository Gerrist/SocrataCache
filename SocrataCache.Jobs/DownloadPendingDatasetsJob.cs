using System.IO.Compression;
using Microsoft.Extensions.Logging;
using Quartz;
using SocrataCache.Config;
using SocrataCache.Managers.Models;

namespace SocrataCache.Jobs;

public class DownloadPendingDatasetsJob : IJob
{
    private readonly Config.Config _config;
    private readonly Managers.DatasetManager _datasetManager;
    private readonly ILogger<FreshDatasetLookupJob> _logger;

    public DownloadPendingDatasetsJob(Config.Config config, Managers.DatasetManager datasetManager,
        ILogger<FreshDatasetLookupJob> logger)
    {
        _config = config;
        _datasetManager = datasetManager;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Running Download Pending Datasets Job");

        foreach (var resource in _config.GetResources())
        {
            var pendingDataset = await _datasetManager.GetDatasetByStatus(DatasetStatus.Pending, resource.ResourceId);

            if (pendingDataset == null)
            {
                _logger.LogInformation($"Resource {resource.ResourceId} has no pending dataset.");
                continue;
            }

            var baseUri = _config.GetBaseUri();
            var downloadUrl = resource.GetDownloadUrl(baseUri, await resource.GetColumns(baseUri));

            try
            {
                _logger.LogInformation($"Downloading dataset for {resource.ResourceId}");

                await _datasetManager.UpdateDatasetStatus(pendingDataset.DatasetId, DatasetStatus.Downloading);

                using var httpClient = new HttpClient();

                var datasetsDirectory = Env.DownloadsRootPath.Value;

                if (!Directory.Exists(datasetsDirectory)) Directory.CreateDirectory(datasetsDirectory);

                var fileNameDownload = $"{resource.ResourceId}-{pendingDataset.DatasetId}.{resource.Type}";
                var fileNameExisting = $"{resource.ResourceId}.{resource.Type}";

                var fileNameDownloadCompressed = $"{resource.ResourceId}-{pendingDataset.DatasetId}.{resource.Type}.gz";
                var fileNameExistingCompressed = $"{resource.ResourceId}.{resource.Type}.gz";

                var filePathDownload = $"{datasetsDirectory}/{fileNameDownload}";
                var filePathExisting = $"{datasetsDirectory}/{fileNameExisting}";
                var filePathDownloadCompressed = $"{datasetsDirectory}/{fileNameDownloadCompressed}";
                var filePathExistingCompressed = $"{datasetsDirectory}/{fileNameExistingCompressed}";

                await _datasetManager.UpdateDatasetStatus(pendingDataset.DatasetId, DatasetStatus.Downloading);

                await using (var stream = await httpClient.GetStreamAsync(downloadUrl))
                await using (var fileStream = File.Create(filePathDownload))
                {
                    await stream.CopyToAsync(fileStream);
                }

                _logger.LogInformation("Downloaded");

                if (File.Exists(filePathExisting)) File.Delete(filePathExisting);

                _logger.LogInformation("Completing download");
                File.Copy(filePathDownload, filePathExisting);

                _logger.LogInformation("Compressing download");

                await using (var downloadFileStream = File.OpenRead(filePathExisting))
                await using (var compressedFileStream = File.Create(filePathDownloadCompressed))
                await using (var gzipStream = new GZipStream(compressedFileStream, CompressionLevel.Optimal))
                {
                    await downloadFileStream.CopyToAsync(gzipStream);
                }

                if (File.Exists(filePathExistingCompressed)) File.Delete(filePathExistingCompressed);

                File.Copy(filePathDownloadCompressed, filePathExistingCompressed);

                await _datasetManager.UpdateDatasetStatus(pendingDataset.DatasetId, DatasetStatus.Downloaded);

                _logger.LogInformation("Compressing complete");

                _logger.LogInformation("Checking if cleanup is needed for retention settings");
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"Error downloading dataset {resource.ResourceId} ({downloadUrl}): {ex.Message}");
                await _datasetManager.UpdateDatasetStatus(pendingDataset.DatasetId, DatasetStatus.Failed);
            }
        }
    }
}