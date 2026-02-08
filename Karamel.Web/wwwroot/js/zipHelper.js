const DEFAULT_MAX_ZIP_SIZE = 20 * 1024 * 1024; // 20 MB
let zipModule = null;

export async function ensureZipModule() {
    if (zipModule) return zipModule;
    try {
        zipModule = await import('jszip');
        return zipModule;
    } catch (e) {
        if (typeof window !== 'undefined') {
            if (window.JSZip) { zipModule = window.JSZip; return zipModule; }

            // Try to dynamically load from local library
            return new Promise((resolve, reject) => {
                const existing = document.querySelector('script[data-jszip]');
                if (existing) {
                    existing.addEventListener('load', () => { zipModule = window.JSZip; resolve(window.JSZip); });
                    existing.addEventListener('error', () => reject(new Error('Failed to load JSZip from local library')));
                    return;
                }

                const script = document.createElement('script');
                script.setAttribute('data-jszip', '1');
                script.src = '/lib/jszip/jszip.min.js';
                script.onload = () => { zipModule = window.JSZip; resolve(window.JSZip); };
                script.onerror = () => reject(new Error('Failed to load JSZip from local library'));
                document.head.appendChild(script);
            });
        }
        throw new Error('JSZip module not available');
    }
}

export function isZipTooLarge(size, maxBytes = DEFAULT_MAX_ZIP_SIZE) {
    if (typeof size !== 'number') return false;
    return size > maxBytes;
}

export async function findMp3CdgRootPairFromBuffer(zipBuf) {
    const jszip = await ensureZipModule();
    const zip = await jszip.loadAsync(zipBuf);
    const rootEntries = [];
    let mp3Entry = null;
    let cdgEntry = null;
    zip.forEach((relativePath) => {
        if (relativePath.indexOf('/') !== -1) return; // only root entries
        rootEntries.push(relativePath);
        const lower = relativePath.toLowerCase();
        if (!mp3Entry && lower.endsWith('.mp3')) mp3Entry = relativePath;
        if (!cdgEntry && lower.endsWith('.cdg')) cdgEntry = relativePath;
    });
    if (mp3Entry && cdgEntry) return { mp3Entry, cdgEntry, rootEntries };
    return { mp3Entry: null, cdgEntry: null, rootEntries };
}

export async function extractEntriesFromBuffer(zipBuf, mp3EntryPath, cdgEntryPath) {
    const jszip = await ensureZipModule();
    const zip = await jszip.loadAsync(zipBuf);
    const mp3File = zip.file(mp3EntryPath);
    const cdgFile = zip.file(cdgEntryPath);
    if (!mp3File || !cdgFile) {
        const entries = [];
        zip.forEach((p) => { if (p.indexOf('/') !== -1) return; entries.push(p); });
        throw new Error(`ZIP entries not found. Expected mp3='${mp3EntryPath}', cdg='${cdgEntryPath}'. Root entries: ${entries.join(', ')}`);
    }
    const mp3ArrayBuffer = await mp3File.async('arraybuffer');
    const cdgArrayBuffer = await cdgFile.async('arraybuffer');
    const mp3Bytes = new Uint8Array(mp3ArrayBuffer);
    const cdgBytes = new Uint8Array(cdgArrayBuffer);
    const mp3Blob = new Blob([mp3ArrayBuffer], { type: 'audio/mpeg' });
    return { mp3Bytes, cdgBytes, mp3Blob };
}

export default {
    ensureZipModule,
    isZipTooLarge,
    findMp3CdgRootPairFromBuffer,
    extractEntriesFromBuffer
};
