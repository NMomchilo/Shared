namespace Shared.Infrastructure;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";
    public string? LocalRedisServiceConnectionString { get; set; }
    public string? RedisServiceConnectionString { get; set; }
    public int ApplicationDatabaseIndex { get; set; }
    public string KeyPrefix { get; set; } = string.Empty;

    public string GetConnectionString(bool isDevelopment) =>
        isDevelopment && !string.IsNullOrWhiteSpace(LocalRedisServiceConnectionString)
            ? LocalRedisServiceConnectionString
            : RedisServiceConnectionString ?? string.Empty;
}
