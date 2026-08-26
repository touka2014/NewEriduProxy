namespace ServiceLib.Manager;

public class StatisticsManager
{
    private static readonly Lazy<StatisticsManager> instance = new(() => new());
    public static StatisticsManager Instance => instance.Value;

    private Config _config;
    private readonly Dictionary<string, ServerStatItem> _serverStatItems = new();
    private readonly SemaphoreSlim _statisticsLock = new(1, 1);
    private List<ServerStatItem> _lstServerStat;
    private Func<ServerSpeedItem, Task>? _updateFunc;
    private DateTime _lastPeriodicSaveUtc = DateTime.MinValue;

    private StatisticsXrayService? _statisticsXray;
    private StatisticsSingboxService? _statisticsSingbox;
    private static readonly string _tag = "StatisticsHandler";
    public List<ServerStatItem> ServerStat => _lstServerStat;

    public async Task Init(Config config, Func<ServerSpeedItem, Task> updateFunc)
    {
        _config = config;
        _updateFunc = updateFunc;
        await InitData();
        if (config.GuiItem.EnableStatistics || _config.GuiItem.DisplayRealTimeSpeed)
        {
            _statisticsXray = new StatisticsXrayService(config, UpdateServerStatHandler);
            _statisticsSingbox = new StatisticsSingboxService(config, UpdateServerStatHandler);
        }
    }

    public void Close()
    {
        try
        {
            _statisticsXray?.Close();
            _statisticsSingbox?.Close();
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    public async Task ClearAllServerStatistics()
    {
        await _statisticsLock.WaitAsync();
        try
        {
            await SQLiteHelper.Instance.ExecuteAsync($"delete from ServerStatItem ");
            _serverStatItems.Clear();
            _lstServerStat = [];
        }
        finally
        {
            _statisticsLock.Release();
        }
    }

    public async Task SaveTo()
    {
        await _statisticsLock.WaitAsync();
        try
        {
            if (_lstServerStat != null)
            {
                await SQLiteHelper.Instance.UpdateAllAsync(_lstServerStat);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        finally
        {
            _statisticsLock.Release();
        }
    }

    public async Task CloneServerStatItem(string indexId, string toIndexId)
    {
        if (_lstServerStat == null)
        {
            return;
        }

        if (indexId == toIndexId)
        {
            return;
        }

        var stat = _lstServerStat.FirstOrDefault(t => t.IndexId == indexId);
        if (stat == null)
        {
            return;
        }

        var toStat = JsonUtils.DeepCopy(stat);
        toStat.IndexId = toIndexId;
        await SQLiteHelper.Instance.ReplaceAsync(toStat);
        _lstServerStat.Add(toStat);
    }

    private async Task InitData()
    {
        await SQLiteHelper.Instance.ExecuteAsync($"delete from ServerStatItem where indexId not in ( select indexId from ProfileItem )");

        var ticks = DateTime.Now.Date.Ticks;
        await SQLiteHelper.Instance.ExecuteAsync($"update ServerStatItem set todayUp = 0,todayDown=0,dateNow={ticks} where dateNow<>{ticks}");

        _lstServerStat = await SQLiteHelper.Instance.TableAsync<ServerStatItem>().ToListAsync();
    }

    private async Task UpdateServerStatHandler(ServerSpeedItem server)
    {
        await UpdateServerStat(server);
    }

    private async Task UpdateServerStat(ServerSpeedItem server)
    {
        var indexId = server.IndexId.IsNotEmpty() ? server.IndexId : _config.IndexId;
        if (indexId.IsNullOrEmpty())
        {
            return;
        }
        await _statisticsLock.WaitAsync();
        try
        {
            var serverStatItem = await GetServerStatItem(indexId);
            if (server.ProxyUp != 0 || server.ProxyDown != 0)
            {
                serverStatItem.TodayUp += server.ProxyUp;
                serverStatItem.TodayDown += server.ProxyDown;
                serverStatItem.TotalUp += server.ProxyUp;
                serverStatItem.TotalDown += server.ProxyDown;
            }

            server.IndexId = indexId;
            server.TodayUp = serverStatItem.TodayUp;
            server.TodayDown = serverStatItem.TodayDown;
            server.TotalUp = serverStatItem.TotalUp;
            server.TotalDown = serverStatItem.TotalDown;

            if (DateTime.UtcNow - _lastPeriodicSaveUtc >= TimeSpan.FromMinutes(1))
            {
                await SQLiteHelper.Instance.UpdateAllAsync(_lstServerStat);
                _lastPeriodicSaveUtc = DateTime.UtcNow;
            }
        }
        finally
        {
            _statisticsLock.Release();
        }
        await _updateFunc?.Invoke(server);
    }

    public async Task UpdateParallelServerStat(ServerSpeedItem server)
    {
        await UpdateServerStat(server);
    }

    private async Task<ServerStatItem> GetServerStatItem(string indexId)
    {
        var ticks = DateTime.Now.Date.Ticks;
        if (!_serverStatItems.TryGetValue(indexId, out var serverStatItem))
        {
            serverStatItem = _lstServerStat.FirstOrDefault(t => t.IndexId == indexId);
            if (serverStatItem == null)
            {
                serverStatItem = new ServerStatItem
                {
                    IndexId = indexId,
                    TotalUp = 0,
                    TotalDown = 0,
                    TodayUp = 0,
                    TodayDown = 0,
                    DateNow = ticks
                };
                await SQLiteHelper.Instance.ReplaceAsync(serverStatItem);
                _lstServerStat.Add(serverStatItem);
            }
            _serverStatItems[indexId] = serverStatItem;
        }

        if (serverStatItem.DateNow != ticks)
        {
            serverStatItem.TodayUp = 0;
            serverStatItem.TodayDown = 0;
            serverStatItem.DateNow = ticks;
        }
        return serverStatItem;
    }
}
