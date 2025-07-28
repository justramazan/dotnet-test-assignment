using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WeatherMcpServer.Configuration;
using WeatherMcpServer.Models;

namespace WeatherMcpServer.Services;

public class OpenWeatherMapService : IWeatherService
{
    private readonly HttpClient _httpClient;
    private readonly WeatherApiConfig _config;
    private readonly ILogger<OpenWeatherMapService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public OpenWeatherMapService(
        HttpClient httpClient,
        IOptions<WeatherApiConfig> config,
        ILogger<OpenWeatherMapService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // Configure HTTP client
        _httpClient.BaseAddress = new Uri(_config.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
    }

    public async Task<CurrentWeatherResponse> GetCurrentWeatherAsync(string city, string? countryCode = null, CancellationToken cancellationToken = default)
    {
        ValidateApiKey();
        ArgumentException.ThrowIfNullOrWhiteSpace(city);

        var locationQuery = BuildLocationQuery(city, countryCode);
        var endpoint = $"/weather?q={Uri.EscapeDataString(locationQuery)}&appid={_config.ApiKey}&units={_config.Units}";

        _logger.LogInformation("Fetching current weather for {Location}", locationQuery);

        try
        {
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            await EnsureSuccessStatusCode(response);

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var weatherData = JsonSerializer.Deserialize<CurrentWeatherResponse>(content, _jsonOptions);

            if (weatherData == null)
            {
                throw new InvalidOperationException("Failed to deserialize weather response");
            }

            _logger.LogInformation("Successfully retrieved current weather for {City}", weatherData.Name);
            return weatherData;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while fetching weather for {Location}", locationQuery);
            throw new InvalidOperationException($"Failed to fetch weather data for {locationQuery}", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout while fetching weather for {Location}", locationQuery);
            throw new InvalidOperationException($"Request timeout while fetching weather for {locationQuery}", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error while processing weather response for {Location}", locationQuery);
            throw new InvalidOperationException($"Invalid response format from weather service", ex);
        }
    }

    public async Task<ForecastResponse> GetWeatherForecastAsync(string city, string? countryCode = null, int days = 3, CancellationToken cancellationToken = default)
    {
        ValidateApiKey();
        ArgumentException.ThrowIfNullOrWhiteSpace(city);

        if (days < 1 || days > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(days), "Days must be between 1 and 5");
        }

        var locationQuery = BuildLocationQuery(city, countryCode);
        var cnt = days * 8; // OpenWeatherMap returns data every 3 hours, so 8 entries per day
        var endpoint = $"/forecast?q={Uri.EscapeDataString(locationQuery)}&appid={_config.ApiKey}&units={_config.Units}&cnt={cnt}";

        _logger.LogInformation("Fetching {Days}-day weather forecast for {Location}", days, locationQuery);

        try
        {
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            await EnsureSuccessStatusCode(response);

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var forecastData = JsonSerializer.Deserialize<ForecastResponse>(content, _jsonOptions);

            if (forecastData == null)
            {
                throw new InvalidOperationException("Failed to deserialize forecast response");
            }

            _logger.LogInformation("Successfully retrieved {Days}-day forecast for {City}", days, forecastData.City?.Name);
            return forecastData;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while fetching forecast for {Location}", locationQuery);
            throw new InvalidOperationException($"Failed to fetch forecast data for {locationQuery}", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout while fetching forecast for {Location}", locationQuery);
            throw new InvalidOperationException($"Request timeout while fetching forecast for {locationQuery}", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error while processing forecast response for {Location}", locationQuery);
            throw new InvalidOperationException($"Invalid response format from weather service", ex);
        }
    }

    public async Task<WeatherAlertsResponse> GetWeatherAlertsAsync(string city, string? countryCode = null, CancellationToken cancellationToken = default)
    {
        ValidateApiKey();
        ArgumentException.ThrowIfNullOrWhiteSpace(city);

        // First, get coordinates for the city
        var currentWeather = await GetCurrentWeatherAsync(city, countryCode, cancellationToken);
        
        if (currentWeather.Coord == null)
        {
            throw new InvalidOperationException($"Unable to get coordinates for {city}");
        }

        var lat = currentWeather.Coord.Lat;
        var lon = currentWeather.Coord.Lon;
        var endpoint = $"/onecall?lat={lat}&lon={lon}&appid={_config.ApiKey}&exclude=minutely,hourly,daily,current";

        _logger.LogInformation("Fetching weather alerts for {City} at coordinates ({Lat}, {Lon})", city, lat, lon);

        try
        {
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            await EnsureSuccessStatusCode(response);

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var alertsData = JsonSerializer.Deserialize<WeatherAlertsResponse>(content, _jsonOptions);

            if (alertsData == null)
            {
                // Return empty alerts response if no alerts data
                alertsData = new WeatherAlertsResponse
                {
                    Lat = lat,
                    Lon = lon,
                    Alerts = Array.Empty<WeatherAlert>()
                };
            }

            _logger.LogInformation("Successfully retrieved weather alerts for {City} - {AlertCount} alerts found", 
                city, alertsData.Alerts?.Length ?? 0);
            return alertsData;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while fetching alerts for {City}", city);
            throw new InvalidOperationException($"Failed to fetch weather alerts for {city}", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout while fetching alerts for {City}", city);
            throw new InvalidOperationException($"Request timeout while fetching alerts for {city}", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error while processing alerts response for {City}", city);
            throw new InvalidOperationException($"Invalid response format from weather service", ex);
        }
    }

    private void ValidateApiKey()
    {
        if (string.IsNullOrWhiteSpace(_config.ApiKey))
        {
            throw new InvalidOperationException("OpenWeatherMap API key is not configured. Please set the OPENWEATHERMAP_API_KEY environment variable.");
        }
    }

    private static string BuildLocationQuery(string city, string? countryCode)
    {
        return string.IsNullOrWhiteSpace(countryCode) 
            ? city 
            : $"{city},{countryCode}";
    }

    private async Task EnsureSuccessStatusCode(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("API request failed with status {StatusCode}: {ErrorContent}", 
                response.StatusCode, errorContent);

            var errorMessage = response.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized => "Invalid API key",
                System.Net.HttpStatusCode.NotFound => "Location not found",
                System.Net.HttpStatusCode.TooManyRequests => "API rate limit exceeded",
                _ => $"API request failed with status {response.StatusCode}"
            };

            throw new HttpRequestException(errorMessage);
        }
    }
} 