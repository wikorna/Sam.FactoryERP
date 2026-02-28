using System.Net.Sockets;
using System.Text;
using Labeling.Application.Interfaces;
using Labeling.Domain.Entities;

namespace Labeling.Infrastructure.Services;

/// <summary>
/// Raw TCP 9100 transport — sends ZPL bytes directly to a Zebra printer.
/// </summary>
public sealed class Raw9100PrinterTransport : IPrinterTransport
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

    public PrinterProtocol Protocol => PrinterProtocol.Raw9100;

    public async Task SendAsync(string host, int port, string zplContent, CancellationToken cancellationToken = default)
    {
        using var client = new TcpClient();

        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectCts.CancelAfter(ConnectTimeout);

        await client.ConnectAsync(host, port, connectCts.Token);

        await using var stream = client.GetStream();
        var data = Encoding.UTF8.GetBytes(zplContent);
        await stream.WriteAsync(data, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}
