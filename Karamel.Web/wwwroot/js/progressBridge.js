import { createLogger } from './logger.js';

const logger = createLogger('ProgressBridge');

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
            dotNetRef.invokeMethodAsync('OnScanProgress', detail.scanned, !!detail.complete).catch(err => logger.error('Failed to invoke OnScanProgress', { error: err.message }));
        } catch (err) {
            logger.error('Failed to invoke dotnet scan progress callback', { error: err.message });
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
