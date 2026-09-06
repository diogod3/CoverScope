using Microsoft.JSInterop;
namespace DD3.CoverScope.Brokers.Clipboards;
public interface IClipboardBroker { ValueTask CopyAsync(string text); }
public class ClipboardBroker(IJSRuntime jsRuntime) : IClipboardBroker
{
    public ValueTask CopyAsync(string text) => jsRuntime.InvokeVoidAsync("CoverScope.copyText", text);
}
