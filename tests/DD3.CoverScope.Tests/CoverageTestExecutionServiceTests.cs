using DD3.CoverScope.Services.Foundations.CoverageTestExecutions;
using System.Diagnostics;
using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Brokers.Processes;
using DD3.CoverScope.Services;
namespace DD3.CoverScope.Tests;
public partial class CoverageTestExecutionServiceTests
{
    private readonly TestProcessBroker broker = new();
    private CoverageTestExecutionService CreateService() => new(broker, new DiagnosticsBroker());
    private static string Absolute(string name) => Path.Combine(Path.GetTempPath(), "coverscope execution", name);
    private class TestProcessBroker : IProcessBroker
    {
        public ProcessStartInfo? StartInfo { get; private set; }
        public Exception? Failure { get; set; }
        public ValueTask<ProcessResult> ExecuteAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
        {
            StartInfo = startInfo;
            if (Failure is not null) throw Failure;
            return ValueTask.FromResult(new ProcessResult(1, "failed test"));
        }
    }
}
