namespace ServiceLib.Manager;

public sealed class ParallelNodeManager
{
    private sealed record Runtime(ProcessService Process, ParallelStatisticsService Statistics, int MixedPort, int StatePort, bool AllowLan, string ConfigPath);

    private static readonly Lazy<ParallelNodeManager> InstanceHolder = new(() => new());
    private readonly ConcurrentDictionary<string, Runtime> _runtimes = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    public static ParallelNodeManager Instance => InstanceHolder.Value;

    private ParallelNodeManager()
    {
        try
        {
            foreach (var path in Directory.GetFiles(Utils.GetBinConfigPath(), "configParallel*.json"))
            {
                TryDeleteConfig(path);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog("Unable to clean stale parallel configuration files", ex);
        }
    }

    public bool IsRunning(string indexId)
    {
        try
        {
            return _runtimes.TryGetValue(indexId, out var runtime) && !runtime.Process.HasExited;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    public int? GetMixedPort(string indexId) => _runtimes.TryGetValue(indexId, out var runtime) ? runtime.MixedPort : null;

    public async Task<RetResult> StartAsync(ProfileItem node, int mixedPort, bool allowLan)
    {
        var ret = new RetResult();
        string? generatedConfigPath = null;
        var keepGeneratedConfig = false;
        await _gate.WaitAsync();
        try
        {
            if (node == null || node.IndexId.IsNullOrEmpty())
            {
                ret.Msg = ResUI.PleaseSelectServer;
                return ret;
            }
            if (mixedPort is <= 0 or >= Global.MaxPort)
            {
                ret.Msg = $"Invalid mixed port: {mixedPort}";
                return ret;
            }
            if (node.ConfigType == EConfigType.Custom)
            {
                ret.Msg = "Custom configurations cannot safely use rewritten inbound ports. Use a standard node or custom outbound.";
                return ret;
            }
            if (_runtimes.TryGetValue(node.IndexId, out var current) && !current.Process.HasExited
                && current.MixedPort == mixedPort && current.AllowLan == allowLan)
            {
                ret.Success = true;
                ret.Msg = $"{node.Remarks} is already running on {(current.AllowLan ? "0.0.0.0" : "127.0.0.1")}:{current.MixedPort}";
                return ret;
            }
            if (current != null && _runtimes.TryRemove(node.IndexId, out var stale))
            {
                stale.Statistics.Dispose();
                if (!stale.Process.HasExited)
                {
                    await stale.Process.StopAsync();
                }
                stale.Process.Dispose();
            }
            if (_runtimes.Any(x => x.Key != node.IndexId && x.Value.MixedPort == mixedPort && !x.Value.Process.HasExited)
                || IsPortInUse(mixedPort))
            {
                ret.Msg = $"Port {mixedPort} is already in use";
                return ret;
            }

            var appConfig = JsonUtils.DeepCopy(AppManager.Instance.Config);
            appConfig.TunModeItem.EnableTun = false;
            appConfig.GuiItem.EnableStatistics = true;
            appConfig.GuiItem.DisplayRealTimeSpeed = true;
            appConfig.Inbound.First().EnableMainInbound = true;
            appConfig.Inbound.First().LocalPort = mixedPort;
            appConfig.Inbound.First().SecondLocalPortEnabled = false;
            appConfig.Inbound.First().AllowLANConn = allowLan;

            var builderResult = await CoreConfigContextBuilder.Build(appConfig, node);
            if (!builderResult.Success)
            {
                ret.Msg = string.Join(Environment.NewLine, builderResult.ValidatorResult.Errors);
                return ret;
            }
            if (builderResult.Context.RunCoreType is not (ECoreType.Xray or ECoreType.v2fly or ECoreType.v2fly_v5 or ECoreType.sing_box))
            {
                ret.Msg = $"Core {builderResult.Context.RunCoreType} does not support parallel instances";
                return ret;
            }

            var statePort = GetFreeStatePort(mixedPort);
            var context = builderResult.Context with
            {
                AppConfig = appConfig,
                IsTunEnabled = false,
                LocalPortOverride = mixedPort,
                StatePortOverride = statePort,
                AllowLanOverride = allowLan
            };
            var fileName = string.Format(Global.CoreParallelConfigFileName, Utils.GetGuid(false));
            var configPath = Utils.GetBinConfigPath(fileName);
            generatedConfigPath = configPath;
            var generateResult = await CoreConfigHandler.GenerateClientConfig(context, configPath);
            if (!generateResult.Success)
            {
                return generateResult;
            }

            var process = await CoreManager.Instance.StartParallelCore(context, fileName);
            if (process == null)
            {
                ret.Msg = ResUI.FailedToRunCore;
                return ret;
            }

            var statistics = new ParallelStatisticsService(node.IndexId, context.RunCoreType, statePort,
                async update =>
                {
                    Publish(node.IndexId, mixedPort, true, update.ProxyUp, update.ProxyDown, "Running");
                    await StatisticsManager.Instance.UpdateParallelServerStat(update);
                });
            _runtimes[node.IndexId] = new Runtime(process, statistics, mixedPort, statePort, allowLan, configPath);
            keepGeneratedConfig = true;
            ProfileExManager.Instance.SetMixedPort(node.IndexId, mixedPort);
            ProfileExManager.Instance.SetAllowLan(node.IndexId, allowLan);
            Publish(node.IndexId, mixedPort, true, 0, 0, "Running");
            _ = MonitorAsync(node.IndexId);

            ret.Success = true;
            ret.Msg = $"{node.Remarks} started: mixed://{(allowLan ? "0.0.0.0" : "127.0.0.1")}:{mixedPort}";
            return ret;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(nameof(ParallelNodeManager), ex);
            ret.Msg = ex.Message;
            return ret;
        }
        finally
        {
            if (!keepGeneratedConfig && generatedConfigPath.IsNotEmpty())
            {
                TryDeleteConfig(generatedConfigPath);
            }
            _gate.Release();
        }
    }

    public async Task StopAsync(string indexId)
    {
        await _gate.WaitAsync();
        try
        {
            await StopCoreAsync(indexId);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task StopCoreAsync(string indexId)
    {
        if (!_runtimes.TryRemove(indexId, out var runtime))
        {
            return;
        }
        runtime.Statistics.Dispose();
        await runtime.Process.StopAsync();
        runtime.Process.Dispose();
        TryDeleteConfig(runtime.ConfigPath);
        Publish(indexId, runtime.MixedPort, false, 0, 0, "Stopped");
    }

    public async Task StopAllAsync()
    {
        await _gate.WaitAsync();
        try
        {
            foreach (var indexId in _runtimes.Keys.ToList())
            {
                await StopCoreAsync(indexId);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task MonitorAsync(string indexId)
    {
        while (_runtimes.TryGetValue(indexId, out var runtime))
        {
            await Task.Delay(1500);
            if (!_runtimes.TryGetValue(indexId, out runtime))
            {
                break;
            }
            bool hasExited;
            try
            {
                hasExited = runtime.Process.HasExited;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            if (!hasExited)
            {
                continue;
            }
            if (_runtimes.TryRemove(indexId, out runtime))
            {
                runtime.Statistics.Dispose();
                runtime.Process.Dispose();
                TryDeleteConfig(runtime.ConfigPath);
                Publish(indexId, runtime.MixedPort, false, 0, 0, "Core exited");
            }
            break;
        }
    }

    private int GetFreeStatePort(int mixedPort)
    {
        var candidate = 32000;
        while (candidate == mixedPort || _runtimes.Values.Any(x => x.StatePort == candidate) || IsPortInUse(candidate))
        {
            candidate++;
        }
        return candidate;
    }

    private static bool IsPortInUse(int port)
    {
        var properties = IPGlobalProperties.GetIPGlobalProperties();
        return properties.GetActiveTcpListeners().Any(x => x.Port == port)
               || properties.GetActiveUdpListeners().Any(x => x.Port == port);
    }

    private static void TryDeleteConfig(string? path)
    {
        try
        {
            if (path.IsNotEmpty() && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog($"Unable to delete parallel configuration file: {path}", ex);
        }
    }

    private static void Publish(string indexId, int mixedPort, bool running, long upload, long download, string message)
    {
        AppEvents.ParallelNodeStatusChanged.Publish(new ParallelNodeStatusItem
        {
            IndexId = indexId,
            MixedPort = mixedPort,
            IsRunning = running,
            UploadSpeed = upload,
            DownloadSpeed = download,
            Message = message
        });
    }
}
