namespace DD3.CoverScope.Models;

public record TestFailure(
    string Name,
    string FullyQualifiedName,
    string Assembly,
    TimeSpan Duration,
    string Message,
    string StackTrace,
    string StandardOutput,
    string StandardError,
    string? SourceFile,
    int? SourceLine);
