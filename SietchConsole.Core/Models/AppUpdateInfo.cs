namespace SietchConsole.Core.Models;

/// <summary>Metadata returned by the GitHub Releases API for the latest Sietch Console release (#145).</summary>
public sealed record AppUpdateInfo(
    string  TagName,
    string  ReleaseName,
    string  HtmlUrl,
    string? InstallerDownloadUrl,
    string? ReleaseNotes);
