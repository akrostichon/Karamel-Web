export function registerScanProgressCallback(dotNetRef) {
    if (!dotNetRef) return;

    // Remove existing listener if present
    if (window._karamel_dotnet_progress_handler) {
        window.removeEventListener('library-scan-progress', window._karamel_dotnet_progress_handler);
        window._karamel_dotnet_progress_handler = null;
    }

    window._karamel_dotnet_progress_handler = function (e) {
        try {
            const detail = e && e.detail ? e.detail : { scanned: 0 };
            // Call .NET method
            dotNetRef.invokeMethodAsync('OnScanProgress', detail.scanned, !!detail.complete).catch(console.error);
        } catch (err) {
            console.error('Failed to invoke dotnet scan progress callback', err);
        }
    };

    window.addEventListener('library-scan-progress', window._karamel_dotnet_progress_handler);
}

export function unregisterScanProgressCallback() {
    if (window._karamel_dotnet_progress_handler) {
        window.removeEventListener('library-scan-progress', window._karamel_dotnet_progress_handler);
        window._karamel_dotnet_progress_handler = null;
    }
}
