using Microsoft.JSInterop;
namespace DD3.CoverScope.Brokers.ElementNavigations;
public interface IElementNavigationBroker { ValueTask ScrollToAsync(string id); }
public class ElementNavigationBroker(IJSRuntime jsRuntime) : IElementNavigationBroker
{
    public ValueTask ScrollToAsync(string id) => jsRuntime.InvokeVoidAsync("CoverScope.scrollToLine", id);
}
