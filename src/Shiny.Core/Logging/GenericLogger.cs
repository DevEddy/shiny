using System;
using Microsoft.Extensions.Logging;

namespace Shiny.Logging;


public class GenericLogger<T>(ILoggerFactory loggerFactory) : ILogger<T>
{
    private readonly ILogger _internalLogger = loggerFactory.CreateLogger(typeof(T).FullName ?? string.Empty);

    public IDisposable? BeginScope<TState>(TState state)  where TState : notnull
        => _internalLogger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel)
        => _internalLogger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _internalLogger.Log(logLevel, eventId, state, exception, formatter);
}
