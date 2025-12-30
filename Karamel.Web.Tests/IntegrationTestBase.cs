using Bunit;
using Fluxor;
using Karamel.Web.Services;
using Karamel.Web.Store.Session;
using Karamel.Web.Tests.TestHelpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;

namespace Karamel.Web.Tests;

/// <summary>
/// Base class for integration tests that use real Fluxor store for testing state management.
/// Provides centralized setup for Fluxor, mocked services, and common test utilities.
/// </summary>
public abstract class IntegrationTestBase : TestContext
{
    /// <summary>
    /// Real Fluxor store for integration testing
    /// </summary>
    protected IStore Store { get; private set; }

    /// <summary>
    /// Real Fluxor dispatcher for integration testing
    /// </summary>
    protected IDispatcher Dispatcher { get; private set; }

    /// <summary>
    /// Mock SessionService for verification in tests
    /// </summary>
    protected Mock<ISessionService> MockSessionService { get; private set; }

    /// <summary>
    /// Fake navigation manager for testing
    /// </summary>
    protected FakeNavigationManager NavigationManager { get; private set; }

    /// <summary>
    /// Test session ID
    /// </summary>
    protected Guid TestSessionId { get; private set; }

    /// <summary>
    /// Initialize integration test with Fluxor and mocked services
    /// </summary>
    /// <param name="sessionId">Session ID for testing</param>
    /// <param name="initialUrl">Initial URL for NavigationManager</param>
    /// <param name="asMainTab">Whether this is the main tab (default: true)</param>
    protected IntegrationTestBase(Guid? sessionId = null, string? initialUrl = null, bool asMainTab = true)
    {
        // Generate test session ID
        TestSessionId = sessionId ?? Guid.NewGuid();

        // Set up initial URL
        var url = initialUrl ?? $"https://karaoke.example.com/nextsong?session={TestSessionId}";
        
        // 1. Add NavigationManager FIRST (before Fluxor)
        NavigationManager = new FakeNavigationManager(url);
        Services.AddSingleton<NavigationManager>(NavigationManager);

        // 2. Add mock JS runtime
        var mockJSRuntime = new MockJSRuntime();
        Services.AddSingleton<IJSRuntime>(mockJSRuntime);

        // 3. Add ISessionService mock BEFORE Fluxor (CRITICAL - Fluxor scans for effects)
        MockSessionService = new SessionServiceMockBuilder()
            .AsMainTab(asMainTab)
            .WithSessionId(TestSessionId)
            .Build();
        Services.AddSingleton<ISessionService>(MockSessionService.Object);

        // 4. THEN add Fluxor (which will scan and find ISessionService is available)
        Services.AddFluxor(options =>
        {
            options.ScanAssemblies(typeof(SessionState).Assembly);
        });

        // 5. Get services after Fluxor registration
        Store = Services.GetRequiredService<IStore>();
        Dispatcher = Services.GetRequiredService<IDispatcher>();
        
        // Initialize the store
        Store.InitializeAsync().Wait();
    }

    /// <summary>
    /// Fake NavigationManager for testing that supports custom URIs.
    /// </summary>
    protected class FakeNavigationManager : NavigationManager
    {
        public List<string> NavigationHistory { get; } = new List<string>();
        
        public FakeNavigationManager(string uri = "http://localhost/")
        {
            var baseUri = new Uri(uri);
            var baseUrl = $"{baseUri.Scheme}://{baseUri.Host}{(baseUri.IsDefaultPort ? "" : $":{baseUri.Port}")}/";
            Initialize(baseUrl, uri);
            NavigationHistory.Add(uri);
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            // Track navigation history
            NavigationHistory.Add(uri);
            // Update the Uri property for navigation
            Uri = ToAbsoluteUri(uri).ToString();
        }
    }

    /// <summary>
    /// Mock JS runtime that handles dynamic module imports
    /// </summary>
    private class MockJSRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (identifier == "import")
            {
                // Return mock JS module
                return new ValueTask<TValue>((TValue)(object)new MockJSObjectReference());
            }
            return new ValueTask<TValue>(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, args);
        }
    }

    /// <summary>
    /// Mock JS object reference for module methods
    /// </summary>
    private class MockJSObjectReference : IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return new ValueTask<TValue>(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, args);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
