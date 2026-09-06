namespace DD3.CoverScope.Models;

public record CoverScopeSessionRequest(string InvocationDirectory, string? TargetPath, int? Port);
public record CoverScopeSession(Guid Id, DateTimeOffset StartedAt, string InvocationDirectory,
    string? InitialTargetPath, Uri PreferredUrl, Uri FallbackUrl);
