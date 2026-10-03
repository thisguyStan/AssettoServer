using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AssettoServer.Server.Configuration;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace AssettoServer.Network.Tcp;

internal sealed class TcpHttpMultiplexer : BackgroundService
{
    private const int MaxHttpMethodLength = 128;
    private const int MaxHttpHeaderLength = 64 * 1024;
    private static readonly TimeSpan ProtocolDetectionTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan HttpHeaderReadTimeout = TimeSpan.FromSeconds(30);
    private static ReadOnlySpan<byte> HttpHeaderTerminator => "\r\n\r\n"u8;
    private readonly ACServerConfiguration _configuration;
    private readonly Func<TcpClient, ACTcpClient> _acTcpClientFactory;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly IServer _httpServer;

    public TcpHttpMultiplexer(
        Func<TcpClient, ACTcpClient> acTcpClientFactory,
        ACServerConfiguration configuration,
        IHostApplicationLifetime applicationLifetime,
        IServer httpServer)
    {
        _acTcpClientFactory = acTcpClientFactory;
        _configuration = configuration;
        _applicationLifetime = applicationLifetime;
        _httpServer = httpServer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await WaitForApplicationStartedAsync(stoppingToken);
        int httpPort = GetInternalHttpPort();

        using var listener = new TcpListener(IPAddress.Any, _configuration.Server.TcpPort);
        listener.Start();
        Log.Information("Starting combined TCP and HTTP server on port {Port}", _configuration.Server.TcpPort);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                TcpClient client = await listener.AcceptTcpClientAsync(stoppingToken);
                _ = HandleClientAsync(client, httpPort, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task WaitForApplicationStartedAsync(CancellationToken stoppingToken)
    {
        if (_applicationLifetime.ApplicationStarted.IsCancellationRequested)
        {
            return;
        }

        var applicationStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration =
            _applicationLifetime.ApplicationStarted.Register(() => applicationStarted.TrySetResult(true));
        await applicationStarted.Task.WaitAsync(stoppingToken);
    }

    private int GetInternalHttpPort()
    {
        var addresses = _httpServer.Features.Get<IServerAddressesFeature>()?.Addresses
                        ?? throw new InvalidOperationException("Kestrel did not expose its bound HTTP address");

        foreach (string address in addresses)
        {
            if (Uri.TryCreate(address, UriKind.Absolute, out Uri? uri)
                && uri.Scheme == Uri.UriSchemeHttp
                && IPAddress.TryParse(uri.Host, out IPAddress? ipAddress)
                && IPAddress.IsLoopback(ipAddress)
                && uri.Port > 0)
            {
                return uri.Port;
            }
        }

        throw new InvalidOperationException("Could not determine Kestrel's internal HTTP port");
    }

    private async Task HandleClientAsync(TcpClient client, int httpPort, CancellationToken stoppingToken)
    {
        bool clientOwnedByAcServer = false;
        EndPoint? remoteEndPoint = client.Client.RemoteEndPoint;

        try
        {
            SharedPortProtocol protocol = await DetectProtocolAsync(client.Client, stoppingToken);
            if (protocol == SharedPortProtocol.Http)
            {
                await ProxyHttpConnectionAsync(client, httpPort, stoppingToken);
            }
            else if (protocol == SharedPortProtocol.AssettoTcp)
            {
                ACTcpClient acClient = _acTcpClientFactory(client);
                await acClient.StartAsync();
                clientOwnedByAcServer = true;
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (OperationCanceledException)
        {
            Log.Debug("Timed out reading shared-port connection from {RemoteEndPoint}", remoteEndPoint?.ToString());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling shared-port connection from {RemoteEndPoint}", remoteEndPoint?.ToString());
        }
        finally
        {
            if (!clientOwnedByAcServer)
            {
                client.Dispose();
            }
        }
    }

    private static async Task<SharedPortProtocol> DetectProtocolAsync(Socket socket, CancellationToken stoppingToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(ProtocolDetectionTimeout);

        byte[] prefix = new byte[MaxHttpMethodLength];
        while (true)
        {
            int bytesRead = await socket.ReceiveAsync(prefix.AsMemory(), SocketFlags.Peek, timeout.Token);
            if (bytesRead == 0)
            {
                return SharedPortProtocol.Unknown;
            }

            SharedPortProtocol protocol = ClassifyProtocol(prefix.AsSpan(0, bytesRead));
            if (protocol != SharedPortProtocol.NeedMoreData)
            {
                return protocol;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
        }
    }

    internal static SharedPortProtocol ClassifyProtocol(ReadOnlySpan<byte> prefix)
    {
        if (prefix.IsEmpty)
        {
            return SharedPortProtocol.NeedMoreData;
        }

        for (int i = 0; i < prefix.Length; i++)
        {
            byte value = prefix[i];
            if (value == (byte)' ')
            {
                return i > 0 ? SharedPortProtocol.Http : SharedPortProtocol.AssettoTcp;
            }

            if (!IsHttpTokenByte(value))
            {
                return SharedPortProtocol.AssettoTcp;
            }
        }

        return prefix.Length < MaxHttpMethodLength
            ? SharedPortProtocol.NeedMoreData
            : SharedPortProtocol.AssettoTcp;
    }

    private static bool IsHttpTokenByte(byte value)
    {
        char character = (char)value;
        return char.IsAsciiLetterOrDigit(character) || "!#$%&'*+-.^_`|~".Contains(character);
    }

    private static async Task ProxyHttpConnectionAsync(TcpClient client, int httpPort, CancellationToken stoppingToken)
    {
        using var httpClient = new TcpClient(AddressFamily.InterNetwork);
        await httpClient.ConnectAsync(IPAddress.Loopback, httpPort, stoppingToken);

        NetworkStream clientStream = client.GetStream();
        NetworkStream httpStream = httpClient.GetStream();
        var remoteAddress = (client.Client.RemoteEndPoint as IPEndPoint)?.Address
                            ?? throw new InvalidOperationException("Accepted TCP connection had no IP remote endpoint");
        using var headerTimeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        headerTimeout.CancelAfter(HttpHeaderReadTimeout);
        (byte[] headers, byte[] remainingData) = await ReadHttpHeadersAsync(clientStream, headerTimeout.Token);
        await httpStream.WriteAsync(RewriteHttpHeaders(headers, remoteAddress), stoppingToken);
        if (remainingData.Length > 0)
        {
            await httpStream.WriteAsync(remainingData, stoppingToken);
        }

        Task clientToHttp = CopyToAsync(clientStream, httpStream, httpClient.Client, stoppingToken);
        Task httpToClient = CopyToAsync(httpStream, clientStream, client.Client, stoppingToken);

        Task firstCompleted = await Task.WhenAny(clientToHttp, httpToClient);
        if (firstCompleted.IsFaulted || firstCompleted.IsCanceled)
        {
            client.Close();
            httpClient.Close();
        }

        await Task.WhenAll(clientToHttp, httpToClient);
    }

    private static async Task<(byte[] Headers, byte[] RemainingData)> ReadHttpHeadersAsync(
        NetworkStream stream,
        CancellationToken stoppingToken)
    {
        byte[] readBuffer = new byte[4096];
        using var accumulated = new MemoryStream();

        while (true)
        {
            int bytesRead = await stream.ReadAsync(readBuffer, stoppingToken);
            if (bytesRead == 0)
            {
                throw new EndOfStreamException("HTTP connection closed before request headers were complete");
            }

            accumulated.Write(readBuffer, 0, bytesRead);
            byte[] buffer = accumulated.GetBuffer();
            int length = (int)accumulated.Length;
            int terminatorIndex = buffer.AsSpan(0, length).IndexOf(HttpHeaderTerminator);
            if (terminatorIndex < 0)
            {
                if (length >= MaxHttpHeaderLength)
                {
                    throw new InvalidDataException($"HTTP request headers exceeded {MaxHttpHeaderLength} bytes");
                }

                continue;
            }

            if (terminatorIndex + HttpHeaderTerminator.Length > MaxHttpHeaderLength)
            {
                throw new InvalidDataException($"HTTP request headers exceeded {MaxHttpHeaderLength} bytes");
            }

            int remainingDataIndex = terminatorIndex + HttpHeaderTerminator.Length;
            return (
                buffer.AsSpan(0, terminatorIndex).ToArray(),
                buffer.AsSpan(remainingDataIndex, length - remainingDataIndex).ToArray());
        }
    }

    internal static byte[] RewriteHttpHeaders(ReadOnlySpan<byte> headers, IPAddress remoteAddress)
    {
        string[] lines = Encoding.Latin1.GetString(headers).Split("\r\n", StringSplitOptions.None);
        if (lines.Length == 0 || string.IsNullOrEmpty(lines[0]))
        {
            throw new InvalidDataException("HTTP request did not contain a request line");
        }

        bool hasUpgradeHeader = false;
        bool connectionRequestsUpgrade = false;
        for (int i = 1; i < lines.Length; i++)
        {
            int separatorIndex = lines[i].IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            string name = lines[i][..separatorIndex];
            string value = lines[i][(separatorIndex + 1)..].Trim();
            if (name.Equals("Upgrade", StringComparison.OrdinalIgnoreCase))
            {
                hasUpgradeHeader = true;
            }
            else if (name.Equals("Connection", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string connectionOption in value.Split(','))
                {
                    if (connectionOption.Trim().Equals("Upgrade", StringComparison.OrdinalIgnoreCase))
                    {
                        connectionRequestsUpgrade = true;
                        break;
                    }
                }
            }
        }

        bool isUpgradeRequest = hasUpgradeHeader && connectionRequestsUpgrade;
        var rewrittenHeaders = new StringBuilder();
        rewrittenHeaders.Append(lines[0]).Append("\r\n");
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            int separatorIndex = line.IndexOf(':');
            if (separatorIndex > 0)
            {
                string name = line[..separatorIndex];
                if (name.Equals("X-Forwarded-For", StringComparison.OrdinalIgnoreCase)
                    || (!isUpgradeRequest && name.Equals("Connection", StringComparison.OrdinalIgnoreCase))
                    || name.Equals("Keep-Alive", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("Proxy-Connection", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            rewrittenHeaders.Append(line).Append("\r\n");
        }

        rewrittenHeaders.Append("X-Forwarded-For: ").Append(remoteAddress).Append("\r\n");
        if (isUpgradeRequest)
        {
            rewrittenHeaders.Append("\r\n");
        }
        else
        {
            rewrittenHeaders.Append("Connection: close\r\n\r\n");
        }

        return Encoding.Latin1.GetBytes(rewrittenHeaders.ToString());
    }

    private static async Task CopyToAsync(
        NetworkStream source,
        NetworkStream destination,
        Socket destinationSocket,
        CancellationToken stoppingToken)
    {
        await source.CopyToAsync(destination, stoppingToken);
        destinationSocket.Shutdown(SocketShutdown.Send);
    }

    internal enum SharedPortProtocol
    {
        NeedMoreData,
        Http,
        AssettoTcp,
        Unknown
    }
}
