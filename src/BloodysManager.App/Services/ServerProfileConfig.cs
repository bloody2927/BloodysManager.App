namespace BloodysManager.App.Services;

public sealed class ServerProfileConfig
{
    public string Name { get; set; } = string.Empty;
    public string? LivePath { get; set; } = string.Empty;
    public string? CopyPath { get; set; } = string.Empty;
    public string? BackupRoot { get; set; } = string.Empty;
    public string? BackupZipRoot { get; set; } = string.Empty;
    public string? WorldExePath { get; set; } = string.Empty;
    public string? AuthExePath { get; set; } = string.Empty;
}
