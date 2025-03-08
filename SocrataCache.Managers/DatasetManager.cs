using Microsoft.EntityFrameworkCore;
using SocrataCache.Managers.Models;

namespace SocrataCache.Managers;

public class DatasetManager
{
    private readonly ManagerContext _managerContext;

    public DatasetManager(ManagerContext managerContext)
    {
        _managerContext = managerContext;
    }

    public async Task<bool> IsFreshDatasetKnown(DateTime referenceDate, string resourceId)
    {
        return await _managerContext.Datasets.FirstOrDefaultAsync(dataset =>
            dataset.ReferenceDate == referenceDate && dataset.ResourceId == resourceId) != null;
    }

    public async Task<string> RegisterFreshDataset(DateTime referenceDate, string resourceId)
    {
        var freshDataset = new DatasetModel
        {
            DatasetId = Guid.NewGuid().ToString(),
            ResourceId = resourceId,
            ReferenceDate = referenceDate,
            Status = DatasetStatus.Pending,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
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