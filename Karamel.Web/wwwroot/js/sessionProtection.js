/**
 * Session protection module - prevents accidental main tab closure
 */

import { createLogger } from './logger.js';

const logger = createLogger('SessionProtection');
let beforeUnloadHandler = null;
let originalTitle = null;

/**
 * Enable main tab protection with beforeunload warning and title prefix
 * @param {string} message - Warning message to display when user tries to close tab
 */
export function setMainTabProtection(message) {
    // Store original title before modification
    if (!originalTitle) {
        originalTitle = document.title;
    }

    // Add beforeunload event listener to warn user before closing
    beforeUnloadHandler = (e) => {
        e.preventDefault();
        e.returnValue = message; // Chrome requires returnValue to be set
        return message; // For older browsers
    };

    window.addEventListener('beforeunload', beforeUnloadHandler);

    // Prepend [MAIN] indicator to document title
    if (!document.title.startsWith('🎤 [MAIN] ')) {
        document.title = '🎤 [MAIN] ' + document.title;
    }

    logger.debug('Main tab protection enabled');
}

/**
 * Disable main tab protection (for cleanup)
 */
export function removeMainTabProtection() {
    if (beforeUnloadHandler) {
        window.removeEventListener('beforeunload', beforeUnloadHandler);
        beforeUnloadHandler = null;
    }

    // Restore original title
    if (originalTitle) {
        document.title = originalTitle;
        originalTitle = null;
    }

    logger.debug('Main tab protection disabled');
}
