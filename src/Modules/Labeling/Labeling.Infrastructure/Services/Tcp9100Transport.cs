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
    private static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(15);

    public PrinterProtocol Protocol => PrinterProtocol.Raw9100;

    public async Task SendAsync(string host, int port, string zplContent, CancellationToken cancellationToken = default)
    {
        var data = Encoding.UTF8.GetBytes(zplContent);
        await SendRawAsync(host, port, data, cancellationToken);
    }

    public async Task SendRawAsync(string host, int port, byte[] data, CancellationToken cancellationToken = default)
    {
        using var client = new TcpClient();

        // 1. Connect with timeout
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectCts.CancelAfter(ConnectTimeout);

        await client.ConnectAsync(host, port, connectCts.Token);

        // 2. Write with timeout
        await using var stream = client.GetStream();

        using var writeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        writeCts.CancelAfter(WriteTimeout);

        await stream.WriteAsync(data, writeCts.Token);
        await stream.FlushAsync(writeCts.Token);
    }
}
