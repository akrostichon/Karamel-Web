using Microsoft.JSInterop;

namespace Karamel.Web.Tests.TestHelpers;

/// <summary>
/// Lightweight mock for IJSObjectReference used in integration tests to simulate
/// JavaScript broadcast channel behavior without actual JavaScript interop
/// </summary>
public class MockJSObjectReference : IJSObjectReference
{
    private readonly Dictionary<string, object?> _returnValues = new();
    private readonly Dictionary<string, List<object?[]>> _capturedCalls = new();
    private readonly Dictionary<string, Func<object?[], Task<object?>>> _handlers = new();
    private bool _disposed;

    /// <summary>
    /// All method calls captured during test execution
    /// </summary>
    public IReadOnlyDictionary<string, List<object?[]>> CapturedCalls => _capturedCalls;

    /// <summary>
    /// Set a return value for a specific method call
    /// </summary>
    public MockJSObjectReference SetReturnValue<T>(string methodName, T value)
    {
        _returnValues[methodName] = value;
        return this;
    }

    /// <summary>
    /// Set a handler function that will be called when a method is invoked
    /// </summary>
    public MockJSObjectReference SetHandler(string methodName, Func<object?[], Task<object?>> handler)
    {
        _handlers[methodName] = handler;
        return this;
    }

    /// <summary>
    /// Check if a method was called
    /// </summary>
    public bool WasCalled(string methodName)
    {
        return _capturedCalls.ContainsKey(methodName) && _capturedCalls[methodName].Count > 0;
    }

    /// <summary>
    /// Get all arguments from calls to a specific method
    /// </summary>
    public List<object?[]> GetCallArguments(string methodName)
    {
        return _capturedCalls.GetValueOrDefault(methodName, new List<object?[]>());
    }

    /// <summary>
    /// Get the number of times a method was called
    /// </summary>
    public int GetCallCount(string methodName)
    {
        return _capturedCalls.GetValueOrDefault(methodName, new List<object?[]>()).Count;
    }

    /// <summary>
    /// Clear all captured calls
    /// </summary>
    public void ClearCalls()
    {
        _capturedCalls.Clear();
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        return InvokeAsync<TValue>(identifier, CancellationToken.None, args ?? Array.Empty<object?>());
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MockJSObjectReference));

        var argsArray = args ?? Array.Empty<object?>();
        
        // Capture the call
        if (!_capturedCalls.ContainsKey(identifier))
            _capturedCalls[identifier] = new List<object?[]>();
        _capturedCalls[identifier].Add(argsArray);

        // If there's a handler, use it
        if (_handlers.ContainsKey(identifier))
        {
            var result = _handlers[identifier](argsArray).GetAwaiter().GetResult();
            if (result is TValue typedResult)
                return ValueTask.FromResult(typedResult);
            if (result == null && default(TValue) == null)
                return ValueTask.FromResult(default(TValue)!);
        }

        // Return configured value or default
        if (_returnValues.TryGetValue(identifier, out var returnValue))
        {
            if (returnValue is TValue typedValue)
                return ValueTask.FromResult(typedValue);
            if (returnValue == null && default(TValue) == null)
                return ValueTask.FromResult(default(TValue)!);
        }

        return ValueTask.FromResult(default(TValue)!);
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }
}
