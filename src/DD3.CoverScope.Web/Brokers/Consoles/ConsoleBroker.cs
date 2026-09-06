namespace DD3.CoverScope.Brokers.Consoles;
public interface IConsoleBroker
{
    string GetInvocationDirectory();
    void WriteLine(string text);
    void WriteError(string text);
}
public class ConsoleBroker : IConsoleBroker
{
    public string GetInvocationDirectory() => Environment.CurrentDirectory;
    public void WriteLine(string text) => Console.WriteLine(text);
    public void WriteError(string text) => Console.Error.WriteLine(text);
}
