/**
 * QR Code Interop - QR code generation for session sharing
 * Uses QRCode.js library from CDN
 */

let qrcodeLibraryLoaded = false;
let qrcodeLibraryPromise = null;

/**
 * Load QRCode.js library from CDN
 * @returns {Promise<void>} Promise that resolves when library is loaded
 */
async function loadQRCodeLibrary() {
    if (qrcodeLibraryLoaded) {
        return Promise.resolve();
    }

    if (qrcodeLibraryPromise) {
        return qrcodeLibraryPromise;
    }

    qrcodeLibraryPromise = new Promise((resolve, reject) => {
        // Check if QRCode is already available (for testing)
        if (typeof window.QRCode !== 'undefined') {
            qrcodeLibraryLoaded = true;
            resolve();
            return;
        }

        const script = document.createElement('script');
        script.src = 'https://cdn.jsdelivr.net/npm/qrcodejs@1.0.0/qrcode.min.js';
        script.onload = () => {
            qrcodeLibraryLoaded = true;
            resolve();
        };
        script.onerror = () => {
            reject(new Error('Failed to load QRCode.js library'));
        };
        document.head.appendChild(script);
    });

    return qrcodeLibraryPromise;
}

/**
 * Generate QR code in specified container
 * @param {string} containerId - ID of the container element
 * @param {string} url - URL to encode in QR code
 * @param {Object} options - QR code options (optional)
 * @param {number} options.width - Width of QR code (default: container width)
 * @param {number} options.height - Height of QR code (default: container height)
 * @param {number} options.colorDark - Dark color (default: #000000)
 * @param {number} options.colorLight - Light color (default: #ffffff)
 * @returns {Promise<void>} Promise that resolves when QR code is generated
 */
export async function generateQRCode(containerId, url, options = {}) {
    if (!containerId) {
        throw new Error('containerId is required');
    }
    if (!url) {
        throw new Error('url is required');
    }

    // Load library if not already loaded
    await loadQRCodeLibrary();

    // Get container element
    const container = document.getElementById(containerId);
    if (!container) {
        throw new Error(`Container with id '${containerId}' not found`);
    }

    // Clear existing QR code
    container.innerHTML = '';

    // Get container dimensions
    const containerWidth = container.offsetWidth || 256;
    const containerHeight = container.offsetHeight || 256;

    // Merge options with defaults
    const qrOptions = {
        text: url,
        width: options.width || containerWidth,
        height: options.height || containerHeight,
        colorDark: options.colorDark || '#000000',
        colorLight: options.colorLight || '#ffffff',
        correctLevel: QRCode.CorrectLevel.H // High error correction
    };

    // Generate QR code
    new QRCode(container, qrOptions);
}

/**
 * Clear QR code from container
 * @param {string} containerId - ID of the container element
 */
export function clearQRCode(containerId) {
    if (!containerId) {
        throw new Error('containerId is required');
    }

    const container = document.getElementById(containerId);
    if (container) {
        container.innerHTML = '';
    }
}

/**
 * Check if QRCode library is loaded
 * @returns {boolean} True if library is loaded
 */
export function isQRCodeLibraryLoaded() {
    return qrcodeLibraryLoaded;
}
