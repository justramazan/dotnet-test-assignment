namespace WeatherMcpServer.Configuration;

public class WeatherApiConfig
{
    public const string SectionName = "WeatherApi";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.openweathermap.org/data/2.5";
    public string Units { get; set; } = "metric"; // metric, imperial, or kelvin
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRetries { get; set; } = 3;
} 