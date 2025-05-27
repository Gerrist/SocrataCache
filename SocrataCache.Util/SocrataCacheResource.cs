using Newtonsoft.Json;

namespace SocrataCache.Util;

internal class CheckResponseDto
{
    [JsonProperty("updated_at")] public required string UpdatedAt { get; set; }
}

public class SocrataCacheResource
{
    public string ResourceId { get; set; } = string.Empty;
    public string SocrataId { get; set; } = string.Empty;
    public string[]? ExcludedColumns { get; set; } = [];
    public string[]? Include { get; set; } = [];
    public string[]? RawInclude { get; set; } = [];
    public Dictionary<string, string>? Query { get; set; } = [];
    public string Type { get; set; } = "csv";
    public bool RetainLastFile { get; set; } = false;
    
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
        var queryParams = new Dictionary<string, string>();
        
        queryParams.Add("$select", string.Join(",", columns));
        
        queryParams.Add("$limit", "100000000");
        
        // Add any custom query parameters
        if (Query != null)
        {
            foreach (var param in Query)
            {
                queryParams.Add(param.Key, param.Value);
            }
        }

        var queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        
        return $"{baseUri}/resource/{SocrataId}.{Type}?{queryString}";
    }

    public async Task<DateTime> GetLastUpdated(string baseUri)
    {
        var checkResponse = await HttpHelper.GetJsonAsync<CheckResponseDto[]>(GetUpdatedAtUrl(baseUri));

        if (checkResponse == null || checkResponse.Length == 0)
        {
            throw new InvalidOperationException($"No update information found for dataset {ResourceId} ({SocrataId}). The API returned an empty response.");
        }

        var checkEntry = checkResponse[0];

        if (checkEntry == null)
            throw new InvalidOperationException($"Check response is invalid for dataset {ResourceId} ({SocrataId})");

        var lastUpdated = DateTime.Parse(checkEntry.UpdatedAt);

        return lastUpdated;
    }

    public async Task<string[]> GetColumns(string baseUri)
    {
        var columnsResponse = await HttpHelper.GetText(GetColumnsUrl(baseUri));
        var columns = columnsResponse.Split(",").Select(c => c.Replace("\"", "")).ToList();

        // If Include is specified, only return those columns
        if (Include != null && Include.Length > 0)
        {
            columns = columns.Where(column => Include.Contains(column)).ToList();
        }
        // Otherwise, exclude the ExcludedColumns
        else if (ExcludedColumns != null && ExcludedColumns.Length > 0)
        {
            columns = columns.Where(column => !ExcludedColumns.Contains(column)).ToList();
        }

        // Add any RawInclude columns
        if (RawInclude != null && RawInclude.Length > 0)
        {
            columns.AddRange(RawInclude);
        }

        return columns.ToArray();
    }
}