namespace ServiceLib.Models.Dto;

public class ParallelNodeStatusItem
{
    public string IndexId { get; set; } = string.Empty;
    public int MixedPort { get; set; }
    public bool IsRunning { get; set; }
    public long UploadSpeed { get; set; }
    public long DownloadSpeed { get; set; }
    public string Message { get; set; } = string.Empty;
}
