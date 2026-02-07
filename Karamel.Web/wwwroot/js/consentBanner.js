/**
 * Consent banner management module
 * Handles localStorage persistence for GDPR consent decisions
 */

const CONSENT_KEY = 'karamel-analytics-consent';
const CONSENT_TIMESTAMP_KEY = 'karamel-consent-timestamp';

/**
 * Get the user's consent decision from localStorage
 * @returns {string|null} 'true' if accepted, 'false' if rejected, null if no decision
 */
export function getConsentDecision() {
    try {
        return localStorage.getItem(CONSENT_KEY);
    } catch (error) {
        console.error('[ConsentBanner] Error reading consent from localStorage:', error);
        return null;
    }
}

/**
 * Set the user's consent decision in localStorage
 * @param {string} decision - 'true' for accept, 'false' for reject
 */
export function setConsentDecision(decision) {
    try {
        localStorage.setItem(CONSENT_KEY, decision);
        localStorage.setItem(CONSENT_TIMESTAMP_KEY, new Date().toISOString());
        console.log(`[ConsentBanner] Consent decision stored: ${decision}`);
    } catch (error) {
        console.error('[ConsentBanner] Error storing consent in localStorage:', error);
    }
}

/**
 * Clear consent decision (for testing or user-initiated reset)
 */
export function clearConsentDecision() {
    try {
        localStorage.removeItem(CONSENT_KEY);
        localStorage.removeItem(CONSENT_TIMESTAMP_KEY);
        console.log('[ConsentBanner] Consent decision cleared');
    } catch (error) {
        console.error('[ConsentBanner] Error clearing consent from localStorage:', error);
    }
}

/**
 * Get the timestamp of when consent was given/rejected
 * @returns {string|null} ISO 8601 timestamp or null
 */
export function getConsentTimestamp() {
    try {
        return localStorage.getItem(CONSENT_TIMESTAMP_KEY);
    } catch (error) {
        console.error('[ConsentBanner] Error reading timestamp from localStorage:', error);
        return null;
    }
}
