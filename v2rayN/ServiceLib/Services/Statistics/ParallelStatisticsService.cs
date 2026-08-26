using System.Net.WebSockets;

namespace ServiceLib.Services.Statistics;

public sealed class ParallelStatisticsService : IDisposable
{
    private const long LinkBase = 1024;
    private readonly string _indexId;
    private readonly ECoreType _coreType;
    private readonly int _statePort;
    private readonly Func<ServerSpeedItem, Task> _updateFunc;
    private readonly CancellationTokenSource _cts = new();
    private ServerSpeedItem _last = new();
    private ClientWebSocket? _webSocket;

    public ParallelStatisticsService(string indexId, ECoreType coreType, int statePort, Func<ServerSpeedItem, Task> updateFunc)
    {
        _indexId = indexId;
        _coreType = coreType;
        _statePort = statePort;
        _updateFunc = updateFunc;
        _ = Task.Run(RunAsync);
    }

    private async Task RunAsync()
    {
        await Task.Delay(500, _cts.Token).ContinueWith(_ => { });
        if (_cts.IsCancellationRequested)
        {
            return;
        }

        if (_coreType == ECoreType.sing_box)
        {
            await RunSingboxAsync();
        }
        else
        {
            await RunXrayAsync();
        }
    }

    private async Task RunXrayAsync()
    {
        var url = $"{Global.HttpProtocol}{Global.Loopback}:{_statePort}/debug/vars";
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000, _cts.Token);
                var result = await HttpClientHelper.Instance.TryGetAsync(url);
                var source = JsonUtils.Deserialize<V2rayMetricsVars>(result ?? string.Empty);
                if (source?.stats?.outbound == null)
                {
                    continue;
                }

                var total = new ServerSpeedItem();
                foreach (var key in source.stats.outbound.Keys.Cast<string>().Where(x => x.StartsWith(Global.ProxyTag)))
                {
                    var value = source.stats.outbound[key];
                    var link = JsonUtils.Deserialize<V2rayMetricsVarsLink>(value?.ToString() ?? string.Empty);
                    total.ProxyUp += (link?.uplink ?? 0) / LinkBase;
                    total.ProxyDown += (link?.downlink ?? 0) / LinkBase;
                }

                if (total.ProxyUp < _last.ProxyUp || total.ProxyDown < _last.ProxyDown)
                {
                    _last = total;
                    continue;
                }

                var update = new ServerSpeedItem
                {
                    IndexId = _indexId,
                    ProxyUp = total.ProxyUp - _last.ProxyUp,
                    ProxyDown = total.ProxyDown - _last.ProxyDown
                };
                _last = total;
                await _updateFunc(update);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // The core may still be starting; retry on the next tick.
            }
        }
    }

    private async Task RunSingboxAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                _webSocket = new ClientWebSocket();
                await _webSocket.ConnectAsync(new Uri($"ws://{Global.Loopback}:{_statePort}/traffic"), _cts.Token);
                var buffer = new byte[2048];
                while (_webSocket.State == WebSocketState.Open && !_cts.IsCancellationRequested)
                {
                    var result = await _webSocket.ReceiveAsync(buffer, _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                    var traffic = JsonUtils.Deserialize<TrafficItem>(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    if (traffic == null)
                    {
                        continue;
                    }
                    await _updateFunc(new ServerSpeedItem
                    {
                        IndexId = _indexId,
                        ProxyUp = (long)(traffic.Up / 1000),
                        ProxyDown = (long)(traffic.Down / 1000)
                    });
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(1000, _cts.Token).ContinueWith(_ => { });
            }
            finally
            {
                _webSocket?.Dispose();
                _webSocket = null;
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _webSocket?.Abort();
        _webSocket?.Dispose();
        _cts.Dispose();
    }
}
