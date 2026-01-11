// Centralized in-memory byte store for MP3 and CDG data
const store = new Map();
const urlCache = new Map();

export function setBytes(key, bytes) {
    if (!(bytes instanceof Uint8Array)) throw new TypeError('bytes must be Uint8Array');
    store.set(key, bytes);
}

export function getBytes(key) {
    return store.get(key) || null;
}

export function clear(key) {
    const bytes = store.get(key);
    if (bytes) store.delete(key);
    const url = urlCache.get(key);
    if (url) {
        try { URL.revokeObjectURL(url); } catch (e) { /* ignore */ }
        urlCache.delete(key);
    }
}

export function createObjectUrl(key, mimeType = 'audio/mpeg') {
    if (urlCache.has(key)) return urlCache.get(key);
    const bytes = store.get(key);
    if (!bytes) return null;
    const blob = new Blob([bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength)], { type: mimeType });
    const url = URL.createObjectURL(blob);
    urlCache.set(key, url);
    return url;
}

export function revokeObjectUrl(url) {
    try { URL.revokeObjectURL(url); } catch (e) { /* ignore */ }
    for (const [k, v] of urlCache) {
        if (v === url) urlCache.delete(k);
    }
}

export default {
    setBytes,
    getBytes,
    clear,
    createObjectUrl,
    revokeObjectUrl
};
