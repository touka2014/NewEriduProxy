using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using ReactiveUI.Primitives;
using ServiceLib;
using ServiceLib.Common;
using ServiceLib.Enums;
using ServiceLib.Events;
using ServiceLib.Handler;
using ServiceLib.Helper;
using ServiceLib.Manager;
using ServiceLib.Models.Dto;
using ServiceLib.Models.Entities;

// This executable owns a separate portable data directory and uses synthetic nodes only.
// Its upstream SOCKS server answers locally, so no subscription or Internet access is needed.
Environment.SetEnvironmentVariable(Global.LocalAppData, null, EnvironmentVariableTarget.Process);
Directory.SetCurrentDirectory(AppContext.BaseDirectory);
Require(AppManager.Instance.InitApp(), "Initialize isolated application state");
var config = AppManager.Instance.Config;
Require(!config.Inbound[0].EnableMainInbound && config.Inbound[0].LocalPort == 20808,
    "Main local 20808 is disabled by default");
Require(!config.TunModeItem.EnableTun && config.UiItem.CurrentLanguage == "en",
    "TUN defaults off and the interface language is English");
Console.WriteLine($"Runtime checks for New Eridu Proxy {Utils.GetVersionInfo()}");
Require(Global.CoreUrls[ECoreType.v2rayN] == "touka2014/NewEriduProxy", "Keep the fork update feed");

await SQLiteHelper.Instance.DeleteAllAsync<ProfileItem>();
await SQLiteHelper.Instance.DeleteAllAsync<ProfileExItem>();
await SQLiteHelper.Instance.DeleteAllAsync<ServerStatItem>();
await SQLiteHelper.Instance.DeleteAllAsync<RoutingItem>();
var routing = new RoutingItem { Id = "smoke-route", Remarks = "Local smoke test", RuleSet = "[]", DomainStrategy = Global.AsIs };
await SQLiteHelper.Instance.InsertAsync(routing);
config.RoutingBasicItem.RoutingIndexId = routing.Id;
config.GuiItem.EnableStatistics = false;
config.GuiItem.DisplayRealTimeSpeed = false;
config.Inbound[0].NewPort4LAN = true;

await using var upstream = new LocalSocksServer();
var nodes = Enumerable.Range(0, 4).Select(i => new ProfileItem
{
    IndexId = $"smoke-{i}",
    Remarks = $"Local smoke node {i}",
    ConfigType = EConfigType.SOCKS,
    CoreType = i < 2 ? ECoreType.Xray : ECoreType.sing_box,
    Address = "127.0.0.1",
    Port = upstream.Port,
    Network = "raw",
    StreamSecurity = string.Empty,
    Subid = string.Empty,
    Username = string.Empty,
    Password = string.Empty,
}).ToArray();
await SQLiteHelper.Instance.InsertAllAsync(nodes);
await SQLiteHelper.Instance.InsertAsync(new ServerStatItem
{
    IndexId = nodes[3].IndexId, DateNow = DateTime.Now.Date.AddDays(-1).Ticks,
    TodayUp = 1234, TodayDown = 5678, TotalUp = 1234, TotalDown = 5678,
});
await ProfileExManager.Instance.Init();
await StatisticsManager.Instance.Init(config, _ => Task.CompletedTask);
Require(StatisticsManager.Instance.ServerStat.Single().TodayDown == 0, "Reset yesterday's daily counters");
await CoreManager.Instance.Init(config, (_, message) => { Console.WriteLine(message); return Task.CompletedTask; });
var telemetry = new ConcurrentDictionary<string, (long Up, long Down)>();
var telemetryTicks = new ConcurrentDictionary<string, int>();
using var subscription = AppEvents.ParallelNodeStatusChanged.AsObservable().Subscribe(update =>
{
    telemetryTicks.AddOrUpdate(update.IndexId, 1, (_, count) => count + 1);
    telemetry.AddOrUpdate(update.IndexId, (update.UploadSpeed, update.DownloadSpeed),
        (_, previous) => (Math.Max(previous.Up, update.UploadSpeed), Math.Max(previous.Down, update.DownloadSpeed)));
});
var mainPortWasListening = IsListening(20808);
var assignments = ProfileExManager.Instance.OrganizeMixedPorts(nodes.Select(node => node.IndexId));
var ports = assignments.Values.Order().ToArray();
Require(ports.SequenceEqual(Enumerable.Range(ports[0], 4)), "Organize four consecutive mixed ports");
Require(ports.All(port => port >= 40000 && port <= 48999), "Stay within the preferred mixed port range");

try
{
    for (var i = 0; i < nodes.Length; i++)
    {
        var result = await ParallelNodeManager.Instance.StartAsync(nodes[i], assignments[nodes[i].IndexId], i % 2 == 1);
        Require(result.Success, $"Start {nodes[i].CoreType} node {i}: {result.Msg}");
    }
    await WaitUntil(() => nodes.All(node => IsListening(assignments[node.IndexId])), "All four mixed listeners are online");
    var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
    for (var i = 0; i < nodes.Length; i++)
    {
        var expectedAddress = i % 2 == 1 ? IPAddress.Any : IPAddress.Loopback;
        Require(listeners.Any(endpoint => endpoint.Port == assignments[nodes[i].IndexId]
            && (endpoint.Address.Equals(expectedAddress) || (i % 2 == 1 && endpoint.Address.Equals(IPAddress.IPv6Any)))),
            $"Per-node LAN binding for {nodes[i].IndexId}");
    }
    var sameNode = await ParallelNodeManager.Instance.StartAsync(nodes[0], assignments[nodes[0].IndexId], false);
    Require(sameNode.Success, "Starting the same listener again is idempotent");
    var conflictNode = nodes[0].WithIndex("smoke-conflict");
    var conflict = await ParallelNodeManager.Instance.StartAsync(conflictNode, assignments[nodes[0].IndexId], false);
    Require(!conflict.Success, "Reject an occupied TCP mixed port");
    using (var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
    {
        conflict = await ParallelNodeManager.Instance.StartAsync(conflictNode, ((IPEndPoint)udp.Client.LocalEndPoint!).Port, false);
        Require(!conflict.Success, "Reject an occupied UDP mixed port");
    }
    await WaitUntil(() => nodes.All(node => telemetryTicks.GetValueOrDefault(node.IndexId) >= 2),
        "Connect every statistics endpoint before transferring data");
    foreach (var node in nodes)
    {
        using var handler = new HttpClientHandler
        {
            Proxy = new WebProxy($"http://127.0.0.1:{assignments[node.IndexId]}"),
            UseProxy = true,
        };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        using var response = await client.PostAsync("http://198.18.0.1/smoke", new ByteArrayContent(new byte[128 * 1024]));
        response.EnsureSuccessStatusCode();
        Require((await response.Content.ReadAsByteArrayAsync()).Length == LocalSocksServer.PayloadSize,
            $"Transfer upload and download through {node.IndexId}");
    }
    await WaitUntil(() => nodes.All(node => telemetry.TryGetValue(node.IndexId, out var rates) && rates.Up > 0 && rates.Down > 0),
        "Receive independent live upload/download telemetry");
    await StatisticsManager.Instance.SaveTo();
    foreach (var node in nodes)
    {
        var stat = await SQLiteHelper.Instance.TableAsync<ServerStatItem>().FirstOrDefaultAsync(item => item.IndexId == node.IndexId);
        Require(stat is { TodayUp: > 0, TodayDown: > 0, TotalUp: > 0, TotalDown: > 0 }, $"Persist daily/lifetime statistics for {node.IndexId}");
    }
    Require(IsListening(20808) == mainPortWasListening && !config.Inbound[0].EnableMainInbound && !config.TunModeItem.EnableTun,
        "Parallel listeners do not enable the main inbound or TUN");
    await ParallelNodeManager.Instance.StopAsync(nodes[0].IndexId);
    Require(!IsListening(assignments[nodes[0].IndexId]) && nodes.Skip(1).All(node => ParallelNodeManager.Instance.IsRunning(node.IndexId)),
        "Stop one selected node without stopping the others");
    await ProfileExManager.Instance.SaveTo();
    for (var i = 0; i < nodes.Length; i++)
    {
        var indexId = nodes[i].IndexId;
        var profile = await SQLiteHelper.Instance.TableAsync<ProfileExItem>().FirstOrDefaultAsync(item => item.IndexId == indexId);
        Require(profile is not null && profile.MixedPort == assignments[nodes[i].IndexId] && profile.AllowLan == (i % 2 == 1),
            $"Persist mixed port and LAN policy for {nodes[i].IndexId}");
    }
}
finally
{
    await ParallelNodeManager.Instance.StopAllAsync();
    StatisticsManager.Instance.Close();
    await SQLiteHelper.Instance.DisposeDbConnectionAsync();
}
Require(ports.All(port => !IsListening(port)), "Stop all listeners");
Require(Directory.GetFiles(Utils.GetBinConfigPath(), "configParallel*.json").Length == 0, "Remove temporary parallel configurations");
Console.WriteLine("Runtime smoke checks passed.");

static bool IsListening(int port) => IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(endpoint => endpoint.Port == port);
static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
    Console.WriteLine($"PASS: {message}");
}
static async Task WaitUntil(Func<bool> condition, string message)
{
    var deadline = DateTime.UtcNow.AddSeconds(15);
    while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(200);
    Require(condition(), message);
}

internal sealed class LocalSocksServer : IAsyncDisposable
{
    public const int PayloadSize = 256 * 1024;
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _acceptTask;
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public LocalSocksServer()
    {
        _listener.Start();
        _acceptTask = AcceptAsync();
    }

    private async Task AcceptAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                _ = HandleAsync(client);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task HandleAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(15));
                var token = timeout.Token;
                var stream = client.GetStream();
                var greeting = new byte[2];
                await stream.ReadExactlyAsync(greeting, token);
                await stream.ReadExactlyAsync(new byte[greeting[1]], token);
                await stream.WriteAsync(new byte[] { 5, 0 }, token);
                var request = new byte[4];
                await stream.ReadExactlyAsync(request, token);
                var addressLength = request[3] switch
                {
                    1 => 4,
                    4 => 16,
                    3 => await ReadByteAsync(stream, token),
                    _ => throw new IOException("Invalid SOCKS address type"),
                };
                await stream.ReadExactlyAsync(new byte[addressLength + 2], token);
                await stream.WriteAsync(new byte[] { 5, 0, 0, 1, 127, 0, 0, 1, 0, 80 }, token);
                var header = new StringBuilder();
                while (!header.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
                {
                    header.Append((char)await ReadByteAsync(stream, token));
                    if (header.Length > 16384) throw new IOException("HTTP header is too large");
                }
                var lengthLine = header.ToString().Split("\r\n").FirstOrDefault(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
                var length = lengthLine is null ? 0 : int.Parse(lengthLine.Split(':', 2)[1].Trim());
                await stream.ReadExactlyAsync(new byte[length], token);
                await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {PayloadSize}\r\nConnection: close\r\n\r\n"), token);
                await stream.WriteAsync(new byte[PayloadSize], token);
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
        }
    }

    private static async Task<int> ReadByteAsync(NetworkStream stream, CancellationToken token)
    {
        var buffer = new byte[1];
        await stream.ReadExactlyAsync(buffer, token);
        return buffer[0];
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _listener.Stop();
        await _acceptTask;
        _cts.Dispose();
    }
}

internal static class ProfileSmokeExtensions
{
    public static ProfileItem WithIndex(this ProfileItem source, string indexId) => new()
    {
        IndexId = indexId, Remarks = "Port conflict probe", ConfigType = source.ConfigType,
        CoreType = source.CoreType, Address = source.Address, Port = source.Port,
        Network = source.Network, StreamSecurity = source.StreamSecurity,
    };
}
