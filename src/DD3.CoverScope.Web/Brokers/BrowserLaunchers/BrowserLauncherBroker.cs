using System.Diagnostics;
namespace DD3.CoverScope.Brokers.BrowserLaunchers;
public interface IBrowserLauncherBroker { void Open(Uri uri); }
public class BrowserLauncherBroker : IBrowserLauncherBroker
{
    public void Open(Uri uri) => Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
}
