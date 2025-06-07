using Microsoft.EntityFrameworkCore;
using SocrataCache.Managers.Models;
using SocrataCache.Util;

namespace SocrataCache.Managers;

public class DatasetManager
{
    private readonly ManagerContext _managerContext;
    private readonly WebhookService _webhookService;

    public DatasetManager(ManagerContext managerContext, WebhookService webhookService)
    {
        _managerContext = managerContext;
        _webhookService = webhookService;
    }

    public async Task<bool> IsFreshDatasetKnown(DateTime referenceDate, string resourceId)
    {
        return await _managerContext.Datasets.FirstOrDefaultAsync(dataset =>
            dataset.ReferenceDate == referenceDate && dataset.ResourceId == resourceId) != null;
    }

    public async Task<string> RegisterFreshDataset(DateTime referenceDate, string resourceId, string type)
    {
        var freshDataset = new DatasetModel
        {
            DatasetId = Guid.NewGuid().ToString(),
            ResourceId = resourceId,
            ReferenceDate = referenceDate,
            Status = DatasetStatus.Pending,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            Type = type,
        };

        await _managerContext.AddAsync(freshDataset);
        await _managerContext.SaveChangesAsync();

        return freshDataset.DatasetId;
    }

    public async Task UpdateDatasetStatus(string datasetId, DatasetStatus status)
    {
        var dataset = await _managerContext.Datasets.FindAsync(datasetId);

        if (dataset == null)
        {
            throw new Exception("Dataset not found");
        }

        dataset.Status = status;
        dataset.UpdatedAt = DateTime.Now;

        await _managerContext.SaveChangesAsync();

        // Send webhook notification
        await _webhookService.SendWebhookNotification(new DatasetWebhookUpdateDto
        {
            DatasetId = dataset.DatasetId,
            ResourceId = dataset.ResourceId,
            Status = dataset.Status.ToString().ToLower(),
            UpdatedAt = dataset.UpdatedAt,
        });
    }

    public async Task<DatasetModel[]> GetDatasetsByStatus(DatasetStatus status)
    {
        return await _managerContext.Datasets
            .Where(d => d.Status == status)
            .OrderBy(d => d.CreatedAt)
            .ToArrayAsync();
    }

    public async Task<DatasetModel[]> GetDatasetsByStatus(DatasetStatus status, string resourceId)
    {
        return await _managerContext.Datasets
            .Where(d => d.Status == status && d.ResourceId == resourceId)
            .OrderBy(d => d.CreatedAt)
            .ToArrayAsync();
    }

    public async Task<DatasetModel?> GetDatasetByStatus(DatasetStatus status, string resourceId)
    {
        return await _managerContext.Datasets
            .Where(d => d.Status == status && d.ResourceId == resourceId)
            .OrderBy(d => d.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<DatasetModel[]> GetDatasets()
    {
        return await _managerContext.Datasets.OrderBy(d => d.CreatedAt).ToArrayAsync();
    }

}