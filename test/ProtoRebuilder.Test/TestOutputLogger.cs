using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Knapcode.ProtoRebuilder.Test
{
    public class TestOutputLogger : ILogger
    {
        public TestOutputLogger(ITestOutputHelper output)
        {
            Output = output;
        }

        public ITestOutputHelper Output { get; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Output.WriteLine(formatter(state, exception));
        }
    }
}