/**
 * fullscreen.js - Fullscreen API wrapper for PlayerView and NextSongView
 * Handles browser fullscreen toggle and syncs state on F11 press
 */

let dotNetRef = null;

/**
 * Initialize fullscreen module with DotNet reference for callbacks
 * @param {object} dotNetReference - DotNet reference for invoking C# methods
 */
export function initializeFullscreen(dotNetReference) {
    dotNetRef = dotNetReference;

    // Listen for fullscreen changes (including F11 key)
    document.addEventListener('fullscreenchange', handleFullscreenChange);
}

/**
 * Toggle fullscreen mode on/off
 * @returns {Promise<boolean>} True if entered fullscreen, false if exited
 */
export async function toggleFullscreen() {
    try {
        if (!document.fullscreenElement) {
            // Enter fullscreen
            await document.documentElement.requestFullscreen();
            return true;
        } else {
            // Exit fullscreen
            await document.exitFullscreen();
            return false;
        }
    } catch (error) {
        console.error('Fullscreen toggle failed:', error);
        throw error;
    }
}

/**
 * Check if currently in fullscreen mode
 * @returns {boolean} True if in fullscreen
 */
export function isFullscreen() {
    return !!document.fullscreenElement;
}

/**
 * Exit fullscreen mode (if currently in fullscreen)
 * @returns {Promise<void>}
 */
export async function exitFullscreen() {
    try {
        if (document.fullscreenElement) {
            await document.exitFullscreen();
        }
    } catch (error) {
        console.error('Exit fullscreen failed:', error);
        throw error;
    }
}

/**
 * Handle fullscreen change events (including F11)
 * Notifies DotNet component to update UI state
 */
function handleFullscreenChange() {
    const isNowFullscreen = !!document.fullscreenElement;
    
    // Notify DotNet component if reference is set
    if (dotNetRef) {
        dotNetRef.invokeMethodAsync('OnFullscreenChanged', isNowFullscreen)
            .catch(err => console.error('Error calling OnFullscreenChanged:', err));
    }
}

/**
 * Cleanup - remove event listener
 */
export function dispose() {
    document.removeEventListener('fullscreenchange', handleFullscreenChange);
    dotNetRef = null;
}
