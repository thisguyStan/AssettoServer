using System.Text;
using System.Net;
using AssettoServer.Network.Tcp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AssettoServer.Tests;

public class TcpHttpMultiplexerTests
{
    [TestCase("", nameof(TcpHttpMultiplexer.SharedPortProtocol.NeedMoreData))]
    [TestCase("G", nameof(TcpHttpMultiplexer.SharedPortProtocol.NeedMoreData))]
    [TestCase("GET ", nameof(TcpHttpMultiplexer.SharedPortProtocol.Http))]
    [TestCase("OPTIONS * HTTP/1.1", nameof(TcpHttpMultiplexer.SharedPortProtocol.Http))]
    [TestCase("PRI * HTTP/2.0", nameof(TcpHttpMultiplexer.SharedPortProtocol.Http))]
    [TestCase("GET/", nameof(TcpHttpMultiplexer.SharedPortProtocol.AssettoTcp))]
    [TestCase("\u0032\0\0\0", nameof(TcpHttpMultiplexer.SharedPortProtocol.AssettoTcp))]
    public void ClassifyProtocol_DetectsHttpAndGameTcp(string prefix, string expected)
    {
        TcpHttpMultiplexer.SharedPortProtocol actual =
            TcpHttpMultiplexer.ClassifyProtocol(Encoding.ASCII.GetBytes(prefix));

        Assert.That(actual.ToString(), Is.EqualTo(expected));
    }

    [Test]
    public void RewriteHttpHeaders_UsesActualClientAddressAndDisablesKeepAlive()
    {
        byte[] headers = Encoding.Latin1.GetBytes(
            "POST /report HTTP/1.1\r\nHost: server\r\nX-Forwarded-For: 192.0.2.99\r\nConnection: keep-alive");

        string rewritten = Encoding.Latin1.GetString(
            TcpHttpMultiplexer.RewriteHttpHeaders(headers, IPAddress.Parse("198.51.100.7")));

        Assert.That(rewritten, Does.Contain("X-Forwarded-For: 198.51.100.7"));
        Assert.That(rewritten, Does.Not.Contain("192.0.2.99"));
        Assert.That(rewritten, Does.Contain("Connection: close"));
    }

    [Test]
    public void RewriteHttpHeaders_PreservesProtocolUpgrade()
    {
        byte[] headers = Encoding.Latin1.GetBytes(
            "GET /socket HTTP/1.1\r\nHost: server\r\nConnection: keep-alive, Upgrade\r\nUpgrade: websocket");

        string rewritten = Encoding.Latin1.GetString(
            TcpHttpMultiplexer.RewriteHttpHeaders(headers, IPAddress.Loopback));

        Assert.That(rewritten, Does.Contain("Connection: keep-alive, Upgrade"));
        Assert.That(rewritten, Does.Contain("Upgrade: websocket"));
        Assert.That(rewritten, Does.Not.Contain("Connection: close"));
    }

    [Test]
    public async Task KestrelBindsLoopbackEphemeralEndpointForSharedPort()
    {
        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webHostBuilder => webHostBuilder
                .UseKestrel()
                .UseUrls("http://127.0.0.1:0")
                .Configure(app => app.Run(context => context.Response.WriteAsync("ready"))))
            .Build();
        await host.StartAsync();

        try
        {
            IServer server = host.Services.GetRequiredService<IServer>();
            string address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var httpClient = new HttpClient();
            string response = await httpClient.GetStringAsync(address);

            Assert.That(response, Is.EqualTo("ready"));
        }
        finally
        {
            await host.StopAsync();
        }
    }
}
