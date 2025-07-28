using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WeatherMcpServer.Configuration;
using WeatherMcpServer.Services;
using WeatherMcpServer.Tools;

var builder = Host.CreateApplicationBuilder(args);

// Configure all logs to go to stderr (stdout is used for the MCP protocol messages).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// Configure weather API settings
builder.Services.Configure<WeatherApiConfig>(options =>
{
    // Read API key from environment variable
    options.ApiKey = Environment.GetEnvironmentVariable("OPENWEATHERMAP_API_KEY") ?? string.Empty;
    options.BaseUrl = Environment.GetEnvironmentVariable("OPENWEATHERMAP_BASE_URL") ?? "https://api.openweathermap.org/data/2.5";
    options.Units = Environment.GetEnvironmentVariable("OPENWEATHERMAP_UNITS") ?? "metric";
    
    if (int.TryParse(Environment.GetEnvironmentVariable("OPENWEATHERMAP_TIMEOUT_SECONDS"), out var timeout))
    {
        options.TimeoutSeconds = timeout;
    }
    
    if (int.TryParse(Environment.GetEnvironmentVariable("OPENWEATHERMAP_MAX_RETRIES"), out var retries))
    {
        options.MaxRetries = retries;
    }
});

// Register HttpClient for weather service
builder.Services.AddHttpClient<IWeatherService, OpenWeatherMapService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "RealWeatherMcpServer/1.0");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler())
.SetHandlerLifetime(TimeSpan.FromMinutes(5));

// Register weather service
builder.Services.AddScoped<IWeatherService, OpenWeatherMapService>();

// Add the MCP services: the transport to use (stdio) and the tools to register.
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<WeatherTools>();

// Remove RandomNumberTools as it's not needed for weather functionality
// If you want to keep it for testing, uncomment the line below:
// .WithTools<RandomNumberTools>();

var host = builder.Build();

// Validate configuration on startup
var logger = host.Services.GetRequiredService<ILogger<Program>>();
var apiKey = Environment.GetEnvironmentVariable("OPENWEATHERMAP_API_KEY");

if (string.IsNullOrWhiteSpace(apiKey))
{
    logger.LogWarning("OpenWeatherMap API key not found. Please set the OPENWEATHERMAP_API_KEY environment variable.");
    logger.LogInformation("You can get a free API key from: https://openweathermap.org/api");
}
else
{
    logger.LogInformation("Weather MCP Server starting with API key configured");
}

await host.RunAsync();