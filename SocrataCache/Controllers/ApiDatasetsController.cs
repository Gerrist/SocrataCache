using Microsoft.AspNetCore.Mvc;
using SocrataCache.Managers.Models;

namespace SocrataCache.Controllers;

public class ApiDatasetsController : Controller
{
    private readonly Managers.DatasetManager _datasetManager;

    public ApiDatasetsController(Managers.DatasetManager datasetManager)
    {
        _datasetManager = datasetManager;
    }
    
    public async Task<ActionResult<DatasetModel[]>> Index()
    {
        return await _datasetManager.GetDatasets();
    }
}