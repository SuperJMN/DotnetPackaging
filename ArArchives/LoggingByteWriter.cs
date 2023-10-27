using System.Reactive.Disposables;
using System.Runtime.CompilerServices;
using System.Text;
using Serilog;

namespace Archive.Tests;

internal class LoggingByteWriter : IByteWriter
{
    private readonly IByteWriter inner;
    private readonly ILogger logger;

    public LoggingByteWriter(IByteWriter inner, ILogger logger)
    {
        this.logger = logger;
        this.inner = inner;
    }

    public void WriteAllBytes(byte[] bytes, string operationName)
    {
        using (Log(operationName))
        {
            inner.WriteAllBytes(bytes, operationName);
        }
    }

    public void WriteString(string str, string operationName, Encoding? encoding = default)
    {
        using (Log(operationName))
        {
            inner.WriteString(str, operationName, encoding);
        }
    }

    public long Position { get; set; }

    private IDisposable Log([CallerMemberName] string operation = null)
    {
        var from = inner.Position;
        return Disposable.Create(() =>
        {
            var to = inner.Position;
            logger.Information("{Operation}:[{From}-{To}:{Size}]", operation, from, to, to - from);
        });
    }
}