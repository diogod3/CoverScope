using DD3.CoverScope;
using DD3.CoverScope.Analysis;
using DD3.CoverScope.Brokers;

if (args.Length == 2 && args[0] == "--internal-process")
{
    return await ProcessSupervisor.RunAsync(args[1]);
}
if (args.Length > 0 && args[0] == "--internal-index")
{
    return await SourceIndexWorker.RunAsync(args);
}
return await CoverScopeHost.RunAsync(args);
