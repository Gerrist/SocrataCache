namespace SocrataCache.Config;

public static class Env
{
    public static readonly Lazy<string> ConfigFilePath =
        new(() => Environment.GetEnvironmentVariable("SOCRATACACHE_CONFIG_FILE") ??
                  throw new NullReferenceException("Environment variable 'SOCRATACACHE_CONFIG_FILE' is not defined"));

    public static readonly Lazy<string> DbFilePath =
        new(() => Environment.GetEnvironmentVariable("SOCRATACACHE_DB_FILE_PATH") ??
                  throw new NullReferenceException("Environment variable 'SOCRATACACHE_DB_FILE_PATH' is not defined"));

    public static readonly Lazy<string> DownloadsRootPath =
        new(() => Environment.GetEnvironmentVariable("SOCRATACACHE_DOWNLOADS_ROOT_PATH") ??
                  throw new NullReferenceException("Environment variable 'SOCRATACACHE_DB_FILE_PATH' is not defined"));
}