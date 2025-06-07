using Newtonsoft.Json;
using System.Text.RegularExpressions;

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

    public static string InjectDatePlaceholders(string input)
    {
        var regex = new Regex(@"\{\{DAY_YYYY_MM_DD\((-?\d+)\)\}\}");
        var result = regex.Replace(input, match =>
        {
            int daysOffset = int.Parse(match.Groups[1].Value);
            var date = DateTime.UtcNow.Date.AddDays(daysOffset);
            return date.ToString("yyyy-MM-dd");
        });
        return result;
    }

    public string GetDownloadUrl(string baseUri, string[] columns)
    {
        var queryParams = new Dictionary<string, string>();
        queryParams.Add("$select", string.Join(",", columns));
        queryParams.Add("$limit", "100000000");
        if (Query != null)
        {
            foreach (var param in Query)
            {
                var replaced = InjectDatePlaceholders(param.Value);
                queryParams.Add(param.Key, replaced);
            }
        }
        var queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        var url = $"{baseUri}/resource/{SocrataId}.{Type}?{queryString}";
        return url;
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
        if (RawInclude != null && RawInclude.Length > 0)
        {
            return RawInclude.ToArray();
        }

        var columnsResponse = await HttpHelper.GetText(GetColumnsUrl(baseUri));
        var columns = columnsResponse.Split(",").Select(c => c.Replace("\"", "")).ToList();

        if (Include != null && Include.Length > 0)
        {
            columns = columns.Where(column => Include.Contains(column)).ToList();
        }
        else if (ExcludedColumns != null && ExcludedColumns.Length > 0)
        {
            columns = columns.Where(column => !ExcludedColumns.Contains(column)).ToList();
        }

        return columns.ToArray();
    }
}