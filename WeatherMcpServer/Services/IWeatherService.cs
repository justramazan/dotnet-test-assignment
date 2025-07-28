using WeatherMcpServer.Models;

namespace WeatherMcpServer.Services;

public interface IWeatherService
{
    /// <summary>
    /// Gets current weather conditions for the specified location.
    /// </summary>
    /// <param name="city">The city name to get weather for</param>
    /// <param name="countryCode">Optional country code (e.g., 'US', 'UK')</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current weather data</returns>
    Task<CurrentWeatherResponse> GetCurrentWeatherAsync(string city, string? countryCode = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets weather forecast for the specified location.
    /// </summary>
    /// <param name="city">The city name to get forecast for</param>
    /// <param name="countryCode">Optional country code (e.g., 'US', 'UK')</param>
    /// <param name="days">Number of days to forecast (1-5)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Weather forecast data</returns>
    Task<ForecastResponse> GetWeatherForecastAsync(string city, string? countryCode = null, int days = 3, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets weather alerts for the specified location.
    /// </summary>
    /// <param name="city">The city name to get alerts for</param>
    /// <param name="countryCode">Optional country code (e.g., 'US', 'UK')</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Weather alerts data</returns>
    Task<WeatherAlertsResponse> GetWeatherAlertsAsync(string city, string? countryCode = null, CancellationToken cancellationToken = default);
} 