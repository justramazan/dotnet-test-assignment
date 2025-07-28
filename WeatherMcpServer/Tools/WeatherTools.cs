using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using WeatherMcpServer.Services;

namespace WeatherMcpServer.Tools;

public class WeatherTools
{
    private readonly IWeatherService _weatherService;
    private readonly ILogger<WeatherTools> _logger;

    public WeatherTools(IWeatherService weatherService, ILogger<WeatherTools> logger)
    {
        _weatherService = weatherService ?? throw new ArgumentNullException(nameof(weatherService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [McpServerTool]
    [Description("Gets current weather conditions for the specified city.")]
    public async Task<string> GetCurrentWeather(
        [Description("The city name to get weather for")] string city,
        [Description("Optional: Country code (e.g., 'US', 'UK')")] string? countryCode = null)
    {
        try
        {
            _logger.LogInformation("Getting current weather for {City}, {CountryCode}", city, countryCode ?? "N/A");

            var weather = await _weatherService.GetCurrentWeatherAsync(city, countryCode);
            
            var result = new StringBuilder();
            result.AppendLine($"🌤️ Current Weather for {weather.Name}, {weather.Sys?.Country}");
            result.AppendLine("─────────────────────────────────────");
            
            if (weather.Weather != null && weather.Weather.Length > 0)
            {
                var mainWeather = weather.Weather[0];
                result.AppendLine($"☁️ Condition: {mainWeather.Main} - {mainWeather.Description}");
            }

            if (weather.Main != null)
            {
                result.AppendLine($"🌡️ Temperature: {weather.Main.Temp:F1}°C (feels like {weather.Main.FeelsLike:F1}°C)");
                result.AppendLine($"📊 Range: {weather.Main.TempMin:F1}°C - {weather.Main.TempMax:F1}°C");
                result.AppendLine($"💧 Humidity: {weather.Main.Humidity}%");
                result.AppendLine($"🎈 Pressure: {weather.Main.Pressure} hPa");
            }

            if (weather.Wind != null)
            {
                result.AppendLine($"💨 Wind: {weather.Wind.Speed} m/s at {weather.Wind.Deg}°");
                if (weather.Wind.Gust.HasValue)
                {
                    result.AppendLine($"   Gusts: {weather.Wind.Gust:F1} m/s");
                }
            }

            if (weather.Clouds != null)
            {
                result.AppendLine($"☁️ Cloud Cover: {weather.Clouds.All}%");
            }

            result.AppendLine($"👁️ Visibility: {weather.Visibility / 1000.0:F1} km");

            if (weather.Sys != null)
            {
                var sunrise = DateTimeOffset.FromUnixTimeSeconds(weather.Sys.Sunrise);
                var sunset = DateTimeOffset.FromUnixTimeSeconds(weather.Sys.Sunset);
                result.AppendLine($"🌅 Sunrise: {sunrise:HH:mm}");
                result.AppendLine($"🌇 Sunset: {sunset:HH:mm}");
            }

            var lastUpdated = DateTimeOffset.FromUnixTimeSeconds(weather.Dt);
            result.AppendLine($"📅 Last updated: {lastUpdated:yyyy-MM-dd HH:mm} UTC");

            return result.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current weather for {City}", city);
            return $"❌ Error: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Gets weather forecast for the specified city.")]
    public async Task<string> GetWeatherForecast(
        [Description("The city name to get forecast for")] string city,
        [Description("Optional: Country code (e.g., 'US', 'UK')")] string? countryCode = null,
        [Description("Number of days to forecast (1-5, default: 3)")] int days = 3)
    {
        try
        {
            _logger.LogInformation("Getting {Days}-day forecast for {City}, {CountryCode}", days, city, countryCode ?? "N/A");

            var forecast = await _weatherService.GetWeatherForecastAsync(city, countryCode, days);
            
            var result = new StringBuilder();
            result.AppendLine($"🔮 {days}-Day Weather Forecast for {forecast.City?.Name}, {forecast.City?.Country}");
            result.AppendLine("═══════════════════════════════════════════════");

            if (forecast.List != null && forecast.List.Length > 0)
            {
                var dailyForecasts = forecast.List
                    .Where(item => !string.IsNullOrEmpty(item.DtTxt))
                    .GroupBy(item => DateTime.Parse(item.DtTxt!).Date)
                    .Take(days)
                    .ToList();

                foreach (var day in dailyForecasts)
                {
                    var date = day.Key;
                    var dayItems = day.OrderBy(x => DateTime.Parse(x.DtTxt!)).ToList();
                    
                    result.AppendLine($"\n📅 {date:dddd, MMMM dd}");
                    result.AppendLine("─────────────────────────────────");

                    // Get morning, afternoon, and evening forecasts
                    var morning = dayItems.FirstOrDefault(x => DateTime.Parse(x.DtTxt!).Hour <= 12);
                    var afternoon = dayItems.FirstOrDefault(x => DateTime.Parse(x.DtTxt!).Hour >= 12 && DateTime.Parse(x.DtTxt!).Hour <= 18);
                    var evening = dayItems.FirstOrDefault(x => DateTime.Parse(x.DtTxt!).Hour >= 18);

                    var dayMin = dayItems.Min(x => x.Main?.TempMin ?? 0);
                    var dayMax = dayItems.Max(x => x.Main?.TempMax ?? 0);
                    
                    result.AppendLine($"🌡️ Temperature: {dayMin:F1}°C - {dayMax:F1}°C");

                    if (morning?.Weather != null && morning.Weather.Length > 0)
                    {
                        result.AppendLine($"🌅 Morning: {morning.Weather[0].Description} ({morning.Main?.Temp:F1}°C)");
                    }
                    
                    if (afternoon?.Weather != null && afternoon.Weather.Length > 0)
                    {
                        result.AppendLine($"☀️ Afternoon: {afternoon.Weather[0].Description} ({afternoon.Main?.Temp:F1}°C)");
                    }
                    
                    if (evening?.Weather != null && evening.Weather.Length > 0)
                    {
                        result.AppendLine($"🌙 Evening: {evening.Weather[0].Description} ({evening.Main?.Temp:F1}°C)");
                    }

                    var avgHumidity = dayItems.Where(x => x.Main != null).Average(x => x.Main!.Humidity);
                    var maxPop = dayItems.Max(x => x.Pop) * 100;
                    
                    result.AppendLine($"💧 Humidity: {avgHumidity:F0}%");
                    result.AppendLine($"🌧️ Precipitation chance: {maxPop:F0}%");
                }
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting forecast for {City}", city);
            return $"❌ Error: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Gets weather alerts and warnings for the specified city.")]
    public async Task<string> GetWeatherAlerts(
        [Description("The city name to get alerts for")] string city,
        [Description("Optional: Country code (e.g., 'US', 'UK')")] string? countryCode = null)
    {
        try
        {
            _logger.LogInformation("Getting weather alerts for {City}, {CountryCode}", city, countryCode ?? "N/A");

            var alerts = await _weatherService.GetWeatherAlertsAsync(city, countryCode);
            
            var result = new StringBuilder();
            result.AppendLine($"🚨 Weather Alerts for {city}");
            result.AppendLine("═══════════════════════════════════");

            if (alerts.Alerts == null || alerts.Alerts.Length == 0)
            {
                result.AppendLine("✅ No active weather alerts at this time.");
                result.AppendLine($"📍 Coordinates: {alerts.Lat:F4}, {alerts.Lon:F4}");
                return result.ToString();
            }

            result.AppendLine($"⚠️ {alerts.Alerts.Length} active alert(s) found:");
            result.AppendLine($"📍 Coordinates: {alerts.Lat:F4}, {alerts.Lon:F4}");

            for (int i = 0; i < alerts.Alerts.Length; i++)
            {
                var alert = alerts.Alerts[i];
                var start = DateTimeOffset.FromUnixTimeSeconds(alert.Start);
                var end = DateTimeOffset.FromUnixTimeSeconds(alert.End);

                result.AppendLine($"\n🚨 Alert #{i + 1}: {alert.Event}");
                result.AppendLine("─────────────────────────────────");
                result.AppendLine($"📢 Issued by: {alert.SenderName}");
                result.AppendLine($"🕐 Valid from: {start:yyyy-MM-dd HH:mm} UTC");
                result.AppendLine($"🕐 Valid until: {end:yyyy-MM-dd HH:mm} UTC");
                
                if (alert.Tags != null && alert.Tags.Length > 0)
                {
                    result.AppendLine($"🏷️ Tags: {string.Join(", ", alert.Tags)}");
                }

                if (!string.IsNullOrEmpty(alert.Description))
                {
                    result.AppendLine($"📋 Description:");
                    result.AppendLine($"   {alert.Description}");
                }
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting weather alerts for {City}", city);
            return $"❌ Error: {ex.Message}";
        }
    }
}