using Newtonsoft.Json;

namespace SocrataCache.Util;

public static class HttpHelper
{
    private static readonly HttpClient client = new();

    public static async Task<TOutput?> GetJsonAsync<TOutput>(string url)
    {
        try
        {
            var responseBody = await client.GetStringAsync(url);

            var json = JsonConvert.DeserializeObject<TOutput>(responseBody);

            return json;
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine("\nException Caught!");
            Console.WriteLine("Message :{0} ", e.Message);
            return default;
        }
    }

    public static async Task<string> GetText(string url)
    {
        return await client.GetStringAsync(url);
    }
}