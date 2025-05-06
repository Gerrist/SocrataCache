using Microsoft.Extensions.Logging;
using Quartz;
using SocrataCache.Managers.Models;

namespace SocrataCache.Jobs;

public class FreshDatasetLookupJob : IJob
{
    private readonly Config.Config _config;
    private readonly Managers.DatasetManager _datasetManager;
    private readonly ILogger<FreshDatasetLookupJob> _logger;

    public FreshDatasetLookupJob(
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
        _logger.LogInformation("Running Socrata fresh dataset lookup job");

        foreach (var resource in _config.GetResources())
        {
            var lastUpdated = await resource.GetLastUpdated(_config.GetBaseUri());
            _logger.LogInformation(
                $"Resource {resource.ResourceId} ({resource.SocrataId}) was last updated on {lastUpdated:yyyy-MM-dd HH:mm:ss}");

            var isDatasetKnown = await _datasetManager.IsFreshDatasetKnown(lastUpdated, resource.ResourceId);
            
            if (isDatasetKnown)
            {
                var knownDataset = await _datasetManager.GetDatasetByStatus(DatasetStatus.Downloaded, resource.ResourceId);

                if (resource.RetainLastFile && knownDataset?.Status == DatasetStatus.Deleted)
                {
                    await _datasetManager.RegisterFreshDataset(lastUpdated, resource.ResourceId, resource.Type);
                    _logger.LogInformation("Dataset {ResourceID}-{DatasetId} is known but deleted. Retaining last file.", resource.ResourceId, knownDataset.DatasetId);
                } else {
                    _logger.LogInformation("Dataset {ResourceID} is already known with date.", resource.ResourceId);
                    continue;
                }
            }

            var obsoleteDatasets =
                await _datasetManager.GetDatasetsByStatus(DatasetStatus.Pending, resource.ResourceId);

            foreach (var obsoleteDataset in obsoleteDatasets)
                await _datasetManager.UpdateDatasetStatus(obsoleteDataset.DatasetId, DatasetStatus.Obsolete);

            _logger.LogInformation($"Marked {obsoleteDatasets.Length} dataset(s) as obsolete.");

            await _datasetManager.RegisterFreshDataset(lastUpdated, resource.ResourceId, resource.Type);
            _logger.LogInformation("Registered dataset as pending.");
        }
    }
}