namespace ServiceLib.Manager;

public class ProfileExManager
{
    private static readonly Lazy<ProfileExManager> _instance = new(() => new());
    private ConcurrentBag<ProfileExItem> _lstProfileEx = [];
    private readonly ConcurrentQueue<string> _queIndexIds = new();
    private readonly Lock _profileExLock = new();
    private readonly Lock _portGate = new();
    public static ProfileExManager Instance => _instance.Value;
    private static readonly string _tag = "ProfileExHandler";

    public ProfileExManager()
    {
        //Init();
    }

    public async Task Init()
    {
        await InitData();
    }

    public async Task<ConcurrentBag<ProfileExItem>> GetProfileExs()
    {
        return await Task.FromResult(_lstProfileEx);
    }

    private async Task InitData()
    {
        await SQLiteHelper.Instance.ExecuteAsync($"delete from ProfileExItem where indexId not in ( select indexId from ProfileItem )");

        var lstProfileEx = new ConcurrentBag<ProfileExItem>(await SQLiteHelper.Instance.TableAsync<ProfileExItem>().ToListAsync());
        lock (_profileExLock)
        {
            _lstProfileEx = lstProfileEx;
        }
    }

    private void IndexIdEnqueue(string indexId)
    {
        if (indexId.IsNotEmpty())
        {
            //Speed tests touch this from many threads; the save loop deduplicates IDs.
            _queIndexIds.Enqueue(indexId);
        }
    }

    private async Task SaveQueueIndexIds()
    {
        var lstIds = new HashSet<string>();
        while (_queIndexIds.TryDequeue(out var id))
        {
            lstIds.Add(id);
        }

        if (lstIds.Count > 0)
        {
            List<ProfileExItem> lstSaves = [];

            foreach (var id in lstIds)
            {
                var itemNew = _lstProfileEx?.FirstOrDefault(t => t.IndexId == id);
                if (itemNew is null)
                {
                    continue;
                }

                lstSaves.Add(itemNew);
            }

            try
            {
                if (lstSaves.Count > 0)
                {
                    //Another save may have inserted these rows since the queue was drained.
                    await SQLiteHelper.Instance.ReplaceAllAsync(lstSaves);
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
            }
        }
    }

    private ProfileExItem AddProfileEx(string indexId)
    {
        var profileEx = new ProfileExItem()
        {
            IndexId = indexId,
            Delay = 0,
            Speed = 0,
            Sort = 0,
            Message = string.Empty
        };
        _lstProfileEx.Add(profileEx);
        IndexIdEnqueue(indexId);
        return profileEx;
    }

    private ProfileExItem GetProfileExItem(string? indexId)
    {
        var profileEx = _lstProfileEx.FirstOrDefault(t => t.IndexId == indexId);
        if (profileEx is not null)
        {
            return profileEx;
        }

        lock (_profileExLock)
        {
            profileEx = _lstProfileEx.FirstOrDefault(t => t.IndexId == indexId);
            return profileEx ?? AddProfileEx(indexId);
        }
    }

    public async Task ClearAll()
    {
        await SQLiteHelper.Instance.ExecuteAsync($"delete from ProfileExItem ");
        lock (_profileExLock)
        {
            _lstProfileEx = [];
        }
    }

    public async Task SaveTo()
    {
        try
        {
            await SaveQueueIndexIds();
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    public void SetTestDelay(string indexId, int delay)
    {
        var profileEx = GetProfileExItem(indexId);

        profileEx.Delay = delay;
        IndexIdEnqueue(indexId);
    }

    public void SetTestSpeed(string indexId, decimal speed)
    {
        var profileEx = GetProfileExItem(indexId);

        profileEx.Speed = speed;
        IndexIdEnqueue(indexId);
    }

    public void SetTestMessage(string indexId, string message)
    {
        var profileEx = GetProfileExItem(indexId);

        profileEx.Message = message;
        IndexIdEnqueue(indexId);
    }

    public void SetTestIpInfo(string indexId, string ipInfo)
    {
        var profileEx = GetProfileExItem(indexId);

        profileEx.IpInfo = ipInfo;
        IndexIdEnqueue(indexId);
    }

    public void SetSort(string indexId, int sort)
    {
        var profileEx = GetProfileExItem(indexId);

        profileEx.Sort = sort;
        IndexIdEnqueue(indexId);
    }

    public int GetSort(string indexId)
    {
        var profileEx = _lstProfileEx.FirstOrDefault(t => t.IndexId == indexId);
        if (profileEx == null)
        {
            return 0;
        }
        return profileEx.Sort;
    }

    public int GetMaxSort()
    {
        if (_lstProfileEx.Count <= 0)
        {
            return 0;
        }
        return _lstProfileEx.Max(t => t?.Sort ?? 0);
    }

    public void SetMixedPort(string indexId, int port)
    {
        var profileEx = GetProfileExItem(indexId);
        profileEx.MixedPort = port;
        IndexIdEnqueue(indexId);
    }

    public int GetMixedPort(string indexId)
    {
        lock (_portGate)
        {
            var profileEx = GetProfileExItem(indexId);
            if (profileEx.MixedPort is > 0 and < Global.MaxPort)
            {
                return profileEx.MixedPort;
            }

            profileEx.MixedPort = FindAvailablePort(GetUnavailablePorts([indexId]), 1);
            IndexIdEnqueue(indexId);
            return profileEx.MixedPort;
        }
    }

    public void SetAllowLan(string indexId, bool allowLan)
    {
        var profileEx = GetProfileExItem(indexId);
        profileEx.AllowLan = allowLan;
        IndexIdEnqueue(indexId);
    }

    public bool GetAllowLan(string indexId)
    {
        return GetProfileExItem(indexId).AllowLan;
    }

    public Dictionary<string, int> OrganizeMixedPorts(IEnumerable<string> indexIds)
    {
        lock (_portGate)
        {
            var ids = indexIds.Where(x => x.IsNotEmpty()).Distinct().ToList();
            if (ids.Count == 0)
            {
                return [];
            }

            var firstPort = FindAvailablePort(GetUnavailablePorts(ids), ids.Count);
            Dictionary<string, int> result = [];
            for (var i = 0; i < ids.Count; i++)
            {
                var port = firstPort + i;
                var profileEx = GetProfileExItem(ids[i]);
                profileEx.MixedPort = port;
                IndexIdEnqueue(ids[i]);
                result[ids[i]] = port;
            }
            return result;
        }
    }

    private HashSet<int> GetUnavailablePorts(IEnumerable<string> excludedIndexIds)
    {
        var excluded = excludedIndexIds.ToHashSet();
        var ports = _lstProfileEx
            .Where(x => !excluded.Contains(x.IndexId) && x.MixedPort is > 0 and < Global.MaxPort)
            .Select(x => x.MixedPort)
            .ToHashSet();
        ports.Add(Global.DefaultLocalPort);

        try
        {
            var properties = IPGlobalProperties.GetIPGlobalProperties();
            ports.UnionWith(properties.GetActiveTcpListeners().Select(x => x.Port));
            ports.UnionWith(properties.GetActiveUdpListeners().Select(x => x.Port));
        }
        catch (Exception ex)
        {
            Logging.SaveLog("Unable to inspect active ports while allocating a mixed port", ex);
        }
        return ports;
    }

    private static int FindAvailablePort(HashSet<int> unavailable, int consecutiveCount)
    {
        var candidateCount = Global.ParallelPortMax - Global.ParallelPortMin - consecutiveCount + 2;
        if (candidateCount <= 0)
        {
            throw new InvalidOperationException("The parallel mixed port range is too small.");
        }

        var randomOffset = Random.Shared.Next(candidateCount);
        for (var offset = 0; offset < candidateCount; offset++)
        {
            var start = Global.ParallelPortMin + ((randomOffset + offset) % candidateCount);
            var available = true;
            for (var i = 0; i < consecutiveCount; i++)
            {
                if (unavailable.Contains(start + i))
                {
                    available = false;
                    break;
                }
            }
            if (available)
            {
                return start;
            }
        }

        throw new InvalidOperationException($"No free block of {consecutiveCount} ports is available in {Global.ParallelPortMin}-{Global.ParallelPortMax}.");
    }
}
