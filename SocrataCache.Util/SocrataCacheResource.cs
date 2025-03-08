using Newtonsoft.Json;

namespace SocrataCache.Util;

internal class CheckResponseDto
{
    [JsonProperty("updated_at")] public required string UpdatedAt { get; set; }
}

public class SocrataCacheResource
{
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = "csv";
    public string SocrataId { get; set; } = string.Empty;
    public string[]? ExcludedColumns { get; set; } = [];
    
    public string GetUpdatedAtUrl(string baseUri)
    {
        return
            $"{baseUri}/resource/{SocrataId}.json?$select=max(:updated_at) as updated_at&$limit=1&$group=:updated_at&$order=:updated_at%20DESC";
    }

    public string GetColumnsUrl(string baseUri)
    {
        return $"{baseUri}/resource/{SocrataId}.csv?$select=*&$limit=0";
    }

    public string GetDownloadUrl(string baseUri, string[] columns)
    {
        return $"{baseUri}/resource/{SocrataId}.csv?$limit=100000000&$select={string.Join(",", columns)}";
    }

    public async Task<DateTime> GetLastUpdated(string baseUri)
    {
        var checkResponse = await HttpHelper.GetJsonAsync<CheckResponseDto[]>(GetUpdatedAtUrl(baseUri));

        var checkEntry = checkResponse?[0];

        if (checkEntry == null)
            throw new InvalidOperationException($"Check response is invalid for dataset {ResourceId} ({SocrataId})");

        var lastUpdated = DateTime.Parse(checkEntry.UpdatedAt);

        return lastUpdated;
    }

    public async Task<string[]> GetColumns(string baseUri)
    {
        var columnsResponse = await HttpHelper.GetText(GetColumnsUrl(baseUri));

        var columns = columnsResponse.Split(",").Select(c => c.Replace("\"", ""));
        
        var columnsWithoutExcluded = columns.Where(column => !(ExcludedColumns ?? []).Contains(column));
        
        return columnsWithoutExcluded.ToArray();
    }
}