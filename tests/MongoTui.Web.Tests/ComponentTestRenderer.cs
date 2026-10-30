using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MongoTui.Web.Tests;

#pragma warning disable BL0006 // Test-only renderer dispatches actual component events without a browser.
internal sealed class ComponentTestRenderer(IServiceProvider services)
    : Renderer(services, services.GetRequiredService<ILoggerFactory>())
{
    private int _root;
    public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

    public Task MountAsync<T>(Dictionary<string, object?> parameters) where T : IComponent => Dispatcher.InvokeAsync(async () =>
    {
        _root = AssignRootComponentId(InstantiateComponent(typeof(T)));
        await RenderRootComponentAsync(_root, ParameterView.FromDictionary(parameters));
    });

    public Task SubmitAsync() => Dispatcher.InvokeAsync(() => DispatchEventAsync(FindEvent(_root, "onsubmit"), null, EventArgs.Empty));
    public Task ChangeAsync(object? value) => Dispatcher.InvokeAsync(() =>
        DispatchEventAsync(FindEvent(_root, "onchange"), null, new ChangeEventArgs { Value = value }));
    public Task ChangeAsync(string selector, object? value) => Dispatcher.InvokeAsync(() =>
        DispatchEventAsync(ElementAttributes(_root, selector)!.Single(frame => frame.AttributeName == "onchange").AttributeEventHandlerId,
            null, new ChangeEventArgs { Value = value }));
    public Task ClickAsync(string cssClass) => Dispatcher.InvokeAsync(() =>
        DispatchEventAsync(ElementAttributes(_root, cssClass)!.Single(frame => frame.AttributeName == "onclick").AttributeEventHandlerId,
            null, new MouseEventArgs()));
    public Task<object?> AttributeAsync(string cssClass, string attribute) => Dispatcher.InvokeAsync<object?>(() =>
        ElementAttributes(_root, cssClass)!.FirstOrDefault(frame => frame.AttributeName == attribute).AttributeValue);
    public Task<string> TextAsync() => Dispatcher.InvokeAsync(() => Text(_root));
    public Task CloseAsync() => Dispatcher.InvokeAsync(() => DisposeAsync().AsTask());

    protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
    protected override void HandleException(Exception exception) => throw new InvalidOperationException("Component event failed.", exception);

    private ulong FindEvent(int componentId, string attribute)
    {
        var frames = GetCurrentRenderTreeFrames(componentId);
        for (var index = 0; index < frames.Count; index++)
        {
            var frame = frames.Array[index];
            if (frame.FrameType == RenderTreeFrameType.Attribute && frame.AttributeName == attribute)
                return frame.AttributeEventHandlerId;
            if (frame.FrameType == RenderTreeFrameType.Component && FindEvent(frame.ComponentId, attribute) is var child && child != 0)
                return child;
        }
        return 0;
    }

    private RenderTreeFrame[]? ElementAttributes(int componentId, string cssClass)
    {
        var frames = GetCurrentRenderTreeFrames(componentId);
        for (var index = 0; index < frames.Count; index++)
        {
            var frame = frames.Array[index];
            if (frame.FrameType == RenderTreeFrameType.Element)
            {
                var attributes = frames.Array.Skip(index + 1).Take(frames.Count - index - 1)
                    .TakeWhile(item => item.FrameType == RenderTreeFrameType.Attribute).ToArray();
                if (attributes.Any(item => item.AttributeValue is string value
                    && (item.AttributeName == "class" && value.Split(' ').Contains(cssClass)
                        || item.AttributeName == "id" && "#" + value == cssClass))) return attributes;
            }
            if (frame.FrameType == RenderTreeFrameType.Component && ElementAttributes(frame.ComponentId, cssClass) is { } child)
                return child;
        }
        return null;
    }

    private string Text(int componentId)
    {
        var frames = GetCurrentRenderTreeFrames(componentId);
        var text = new System.Text.StringBuilder();
        for (var index = 0; index < frames.Count; index++)
        {
            var frame = frames.Array[index];
            if (frame.FrameType == RenderTreeFrameType.Text) text.Append(frame.TextContent);
            else if (frame.FrameType == RenderTreeFrameType.Component) text.Append(Text(frame.ComponentId));
        }
        return text.ToString();
    }
}
#pragma warning restore BL0006
