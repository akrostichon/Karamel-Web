/**
 * Centralized logging infrastructure with Application Insights integration
 * 
 * Usage:
 *   import { createLogger } from './logger.js';
 *   const logger = createLogger('ModuleName');
 *   logger.debug('Debug message', { key: 'value' });
 *   logger.info('Info message', { key: 'value' });
 *   logger.warn('Warning message', { key: 'value' });
 *   logger.error('Error message', error, { key: 'value' });
 */

/**
 * Log levels enum
 * @enum {number}
 */
export const LogLevel = {
    Debug: 0,
    Info: 1,
    Warn: 2,
    Error: 3
};

/**
 * Check if running in development mode
 * @returns {boolean}
 */
function isDevelopment() {
    // Check if window.location is available (may not be in test environments)
    if (!window.location || !window.location.hostname) {
        return false;
    }
    const hostname = window.location.hostname;
    return hostname === 'localhost' || hostname === '127.0.0.1' || hostname === '::1';
}

/**
 * Get current session ID from sessionStorage (if available)
 * @returns {string|null}
 */
function getCurrentSessionId() {
    try {
        // Session ID is stored in sessionStorage with key pattern "karamel-session-{guid}"
        for (let i = 0; i < sessionStorage.length; i++) {
            const key = sessionStorage.key(i);
            if (key && key.startsWith('karamel-session-')) {
                return key.substring('karamel-session-'.length);
            }
        }
    } catch (e) {
        // SessionStorage access might fail in some environments (e.g., private browsing)
        return null;
    }
    return null;
}

/**
 * Get Application Insights instance
 * @returns {object|null}
 */
function getAppInsights() {
    return window.appInsights || null;
}

/**
 * Format log message with timestamp and module name
 * @param {string} moduleName
 * @param {string} level
 * @param {string} message
 * @returns {string}
 */
function formatMessage(moduleName, level, message) {
    // Handle cases where Date might be mocked (e.g., in tests)
    let timestamp;
    try {
        const date = new Date();
        timestamp = date.toISOString();
    } catch (e) {
        // Fallback: use Date.now() if available, otherwise use placeholder
        timestamp = Date.now ? String(Date.now()) : 'unknown';
    }
    return `[${timestamp}] [${moduleName}] [${level}] ${message}`;
}

/**
 * Build structured properties object
 * @param {string} moduleName
 * @param {object} properties
 * @returns {object}
 */
function buildProperties(moduleName, properties = {}) {
    const sessionId = getCurrentSessionId();
    // Handle cases where Date might be mocked (e.g., in tests)
    let timestamp;
    try {
        const date = new Date();
        timestamp = date.toISOString();
    } catch (e) {
        timestamp = Date.now ? String(Date.now()) : 'unknown';
    }
    return {
        moduleName,
        timestamp,
        ...(sessionId && { sessionId }),
        ...properties
    };
}

/**
 * Create a logger instance for a specific module
 * @param {string} moduleName - Name of the module using this logger
 * @returns {object} Logger instance with debug, info, warn, error methods
 */
export function createLogger(moduleName) {
    const currentLogLevel = window.logLevel !== undefined ? window.logLevel : (isDevelopment() ? LogLevel.Debug : LogLevel.Warn);

    /**
     * Log a debug message
     * @param {string} message
     * @param {object} properties - Additional structured properties
     */
    function debug(message, properties = {}) {
        if (currentLogLevel <= LogLevel.Debug) {
            console.log(formatMessage(moduleName, 'DEBUG', message), buildProperties(moduleName, properties));
        }
    }

    /**
     * Log an info message
     * @param {string} message
     * @param {object} properties - Additional structured properties
     */
    function info(message, properties = {}) {
        if (currentLogLevel <= LogLevel.Info) {
            console.log(formatMessage(moduleName, 'INFO', message), buildProperties(moduleName, properties));
        }
        
        // Track as custom event in Application Insights
        const appInsights = getAppInsights();
        if (appInsights) {
            appInsights.trackEvent({
                name: `${moduleName}.Info`,
                properties: buildProperties(moduleName, { message, ...properties })
            });
        }
    }

    /**
     * Log a warning message
     * @param {string} message
     * @param {object} properties - Additional structured properties
     */
    function warn(message, properties = {}) {
        if (currentLogLevel <= LogLevel.Warn) {
            console.warn(formatMessage(moduleName, 'WARN', message), buildProperties(moduleName, properties));
        }
        
        // Track as custom event in Application Insights
        const appInsights = getAppInsights();
        if (appInsights) {
            appInsights.trackEvent({
                name: `${moduleName}.Warning`,
                properties: buildProperties(moduleName, { message, ...properties })
            });
        }
    }

    /**
     * Log an error message
     * @param {string} message
     * @param {Error|null} error - Optional error object
     * @param {object} properties - Additional structured properties
     */
    function error(message, errorObj = null, properties = {}) {
        if (currentLogLevel <= LogLevel.Error) {
            if (errorObj) {
                console.error(formatMessage(moduleName, 'ERROR', message), errorObj, buildProperties(moduleName, properties));
            } else {
                console.error(formatMessage(moduleName, 'ERROR', message), buildProperties(moduleName, properties));
            }
        }
        
        // Track as exception in Application Insights
        const appInsights = getAppInsights();
        if (appInsights) {
            if (errorObj instanceof Error) {
                appInsights.trackException({
                    exception: errorObj,
                    properties: buildProperties(moduleName, { message, ...properties })
                });
            } else {
                // If no Error object, create one from message
                appInsights.trackException({
                    exception: new Error(message),
                    properties: buildProperties(moduleName, properties)
                });
            }
        }
    }

    return {
        debug,
        info,
        warn,
        error
    };
}
