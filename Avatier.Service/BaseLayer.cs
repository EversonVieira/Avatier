using Avatier.Service.Enums;
using Avatier.Service.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Avatier.Service
{
    public abstract class BaseLayer
    {
        protected readonly ILogger _logger;
        private readonly LogFeederOptions _options;

        protected BaseLayer(ILogger logger, IOptions<LogFeederOptions> options)
        {
            _logger = logger;
            _options = options.Value;
        }

        protected void Log(LogLevel level, string message, object?[]? args = default, LogSensitivityLevelEnum logSensitivityLevel = LogSensitivityLevelEnum.Debug)
        {
            if (logSensitivityLevel > _options.AllowedSensitivity)
            {
                return;
            }

            _logger.Log(level, message, args ?? []);
        }

        protected void LogInformation(string message, object?[]? args = default, LogSensitivityLevelEnum logSensitivityLevel = LogSensitivityLevelEnum.Debug)
        {
            Log(LogLevel.Information, message, args, logSensitivityLevel);
        }

        protected void LogWarning(string message, object?[]? args = default, LogSensitivityLevelEnum logSensitivityLevel = LogSensitivityLevelEnum.Debug)
        {
            Log(LogLevel.Warning, message, args, logSensitivityLevel);
        }

        protected void LogError(string message, object?[]? args = default, LogSensitivityLevelEnum logSensitivityLevel = LogSensitivityLevelEnum.Debug)
        {
            Log(LogLevel.Error, message, args, logSensitivityLevel);
        }

        protected void LogCritical(string message, object?[]? args = default, LogSensitivityLevelEnum logSensitivityLevel = LogSensitivityLevelEnum.Debug)
        {
            Log(LogLevel.Critical, message, args, logSensitivityLevel);
        }

        protected void LogDebug(string message, object?[]? args = default, LogSensitivityLevelEnum logSensitivityLevel = LogSensitivityLevelEnum.Debug)
        {
            Log(LogLevel.Debug, message, args, logSensitivityLevel);
        }


    }
}
