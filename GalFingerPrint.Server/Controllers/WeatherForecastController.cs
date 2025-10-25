using GalFingerPrint.Server.Helper;
using Microsoft.AspNetCore.Mvc;

namespace GalFingerPrint.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;

        public WeatherForecastController(ILogger<WeatherForecastController> logger)
        {
            _logger = logger;
        }

        /// <summary>获取示例天气预报数据</summary>
        /// <remarks>该接口返回一组随机生成的天气数据，主要用于演示用途</remarks>
        /// <response code="200">成功返回天气预报列表</response>
        [HttpGet(Name = "GetWeatherForecast")]
        public IEnumerable<WeatherForecast> Get()
        {
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
        }

        /// <summary>获取客户端IP地址</summary>
        /// <response code="200">返回客户端ip</response>
        [HttpGet("GetIp")]
        public string GetIp() => this.GetClientIp();
    }
}
