// File System Access API wrapper for loading MP3 and CDG files
// Store file data in module-level variables to avoid JSON serialization issues

import { extractMetadata, validatePattern } from './metadata.js';

let mp3Data = null;
let cdgData = null;
let libraryDirectoryHandle = null; // Keep directory handle for session-long access
let zipModule = null;
const MAX_ZIP_SIZE = 20 * 1024 * 1024; // 20 MB limit for in-memory unzip

// Helper: build a song object for a directory-origin song
async function buildDirectorySong(mp3FileEntry, relativePath, filenamePattern) {
    // mp3FileEntry may be an object { handle, file } (from getFile) or a File-like object
    const fileObj = mp3FileEntry.file ? mp3FileEntry.file : mp3FileEntry;
    const baseName = fileObj.name.slice(0, -4);
    const fullPath = relativePath ? `${relativePath}/${baseName}` : baseName;
    const metadata = await extractMetadata(fileObj, fullPath, filenamePattern);
    return {
        id: crypto.randomUUID(),
        artist: metadata.artist,
        title: metadata.title,
        mp3FileName: `${baseName}.mp3`,
        cdgFileName: `${baseName}.cdg`,
        path: relativePath,
        fullPath: fullPath
    };
}

// Helper: build a song object for a zip-origin song (assumes mp3Entry/cdgEntry are root paths)
async function buildZipSong(zip, zipFileName, zipFilePath, mp3EntryPath, cdgEntryPath, filenamePattern) {
    const baseName = mp3EntryPath.substring(0, mp3EntryPath.length - 4);
    let artist = '';
    let title = baseName;
    try {
        const mp3ArrayBuffer = await zip.file(mp3EntryPath).async('arraybuffer');
        const blob = new Blob([mp3ArrayBuffer], { type: 'audio/mpeg' });
        const md = await extractMetadata(blob, baseName, filenamePattern);
        artist = md.artist; title = md.title;
    } catch (e) {
        // ignore metadata extraction errors
    }

    return {
        id: crypto.randomUUID(),
        artist: artist,
        title: title,
        mp3FileName: `${baseName}.mp3`,
        cdgFileName: `${baseName}.cdg`,
        path: '',
        fullPath: baseName,
        sourceType: 'zip',
        zipFileName: zipFileName,
        zipEntryMp3Path: mp3EntryPath,
        zipEntryCdgPath: cdgEntryPath,
        zipFilePath: zipFilePath
    };
}

async function ensureJSZip() {
    if (zipModule) return zipModule;
    // Try dynamic import first (works in bundlers if available)
    try {
        zipModule = await import('jszip');
        return zipModule;
    } catch (e) {
        // Fallback to CDN UMD build which exposes JSZip global when loaded
    }

    return new Promise((resolve, reject) => {
        if (window.JSZip) { zipModule = window.JSZip; resolve(window.JSZip); return; }
        const existing = document.querySelector('script[data-jszip]');
        if (existing) {
            existing.addEventListener('load', () => { zipModule = window.JSZip; resolve(window.JSZip); });
            existing.addEventListener('error', () => reject(new Error('Failed to load JSZip from CDN')));
            return;
        }

        const script = document.createElement('script');
        script.setAttribute('data-jszip', '1');
        script.src = 'https://cdn.jsdelivr.net/npm/jszip@3.10.1/dist/jszip.min.js';
        script.onload = () => { zipModule = window.JSZip; resolve(window.JSZip); };
        script.onerror = () => reject(new Error('Failed to load JSZip from CDN'));
        document.head.appendChild(script);
    });
}

export async function pickMp3File() {
    try {
        const [fileHandle] = await window.showOpenFilePicker({
            types: [{
                description: 'MP3 Audio Files',
                accept: { 'audio/mpeg': ['.mp3'] }
            }],
            multiple: false
        });
        
        const file = await fileHandle.getFile();
        const arrayBuffer = await file.arrayBuffer();
        mp3Data = new Uint8Array(arrayBuffer);
        
        return {
            name: file.name,
            size: file.size
        };
    } catch (error) {
        console.error('Error picking MP3 file:', error);
        return null;
    }
}

export async function pickCdgFile() {
    try {
        const [fileHandle] = await window.showOpenFilePicker({
            types: [{
                description: 'CDG Graphics Files',
                accept: { 'application/octet-stream': ['.cdg'] }
            }],
            multiple: false
        });
        
        const file = await fileHandle.getFile();
        const arrayBuffer = await file.arrayBuffer();
        cdgData = new Uint8Array(arrayBuffer);
        
        return {
            name: file.name,
            size: file.size
        };
    } catch (error) {
        console.error('Error picking CDG file:', error);
        return null;
    }
}

export function getMp3Data() {
    return mp3Data;
}

export function getCdgData() {
    return cdgData;
}

export function hasFiles() {
    return mp3Data !== null && cdgData !== null;
}

/**
 * Pick a library directory and scan for karaoke files (MP3 + CDG pairs)
 * @param {string} filenamePattern - Pattern for parsing filenames (default: "%artist - %title")
 * @returns {Promise<Array>} Array of song metadata objects
 */
export async function pickLibraryDirectory(filenamePattern = '%artist - %title', progressStep = 10) {
    try {
        // Request directory access
        libraryDirectoryHandle = await window.showDirectoryPicker({
            mode: 'read'
        });

        // Validate pattern
        const validPattern = validatePattern(filenamePattern);

        // Recursively scan for songs
        const songs = [];
        // matchedCount tracks number of matched (mp3+cdg) songs discovered so far
        let matchedCount = 0;

        async function scanWrapper(directoryHandle, songsAcc, relativePath = '', filenamePatternInner = '%artist - %title') {
            const mp3Files = new Map();
            const cdgFiles = new Set();
            const subdirectories = [];

            for await (const entry of directoryHandle.values()) {
                if (entry.kind === 'file') {
                    const fileName = entry.name.toLowerCase();

                    if (fileName.endsWith('.mp3')) {
                            const baseName = entry.name.slice(0, -4); // Remove .mp3 extension
                            const file = await entry.getFile();
                            mp3Files.set(baseName, { handle: entry, file: file });
                        } else if (fileName.endsWith('.zip')) {
                            // ZIP files: simplified handling per spec — each zip is expected
                            // to contain exactly one MP3 and one CDG at the zip root.
                            try {
                                const jszip = await ensureJSZip();
                                const zipFileObj = await entry.getFile();

                                // If file size is known and exceeds the limit, skip to avoid OOM
                                if (typeof zipFileObj.size === 'number' && zipFileObj.size > MAX_ZIP_SIZE) {
                                    console.warn('Skipping large ZIP file during scan:', entry.name, `(${zipFileObj.size} bytes)`);
                                } else {
                                    const zipBuf = await zipFileObj.arrayBuffer();
                                    const zip = await jszip.loadAsync(zipBuf);

                                    let mp3Entry = null;
                                    let cdgEntry = null;
                                    zip.forEach((relativePath) => {
                                        // only consider root entries (no '/')
                                        if (relativePath.indexOf('/') !== -1) return;
                                        const lower = relativePath.toLowerCase();
                                        if (!mp3Entry && lower.endsWith('.mp3')) mp3Entry = relativePath;
                                        if (!cdgEntry && lower.endsWith('.cdg')) cdgEntry = relativePath;
                                    });

                                    if (mp3Entry && cdgEntry) {
                                        const zipFilePath = relativePath ? `${relativePath}/${entry.name}` : entry.name;
                                        const song = await buildZipSong(zip, entry.name, zipFilePath, mp3Entry, cdgEntry, filenamePatternInner);
                                        songsAcc.push(song);
                                        matchedCount++;
                                    }
                                }
                            } catch (e) {
                                console.warn('Failed to read ZIP file during scan:', entry.name, e);
                            }
                    } else if (fileName.endsWith('.cdg')) {
                        const baseName = entry.name.slice(0, -4); // Remove .cdg extension
                        cdgFiles.add(baseName);
                    }
                } else if (entry.kind === 'directory') {
                    subdirectories.push(entry);
                }
            }

            for (const [baseName, mp3Data] of mp3Files) {
                const hasCdg = cdgFiles.has(baseName);

                // Only include songs that have both MP3 and CDG files
                if (!hasCdg) {
                    continue;
                }

                const fullPath = relativePath ? `${relativePath}/${baseName}` : baseName;

                // Extract metadata (ID3 tags or filename parsing)
                const song = await buildDirectorySong(mp3Data, relativePath, filenamePatternInner);
                songsAcc.push(song);

                matchedCount++;
                try {
                    if (matchedCount % progressStep === 0) {
                        window.dispatchEvent(new CustomEvent('library-scan-progress', { detail: { scanned: matchedCount } }));
                    }
                } catch (e) {
                    console.warn('Failed to dispatch library-scan-progress event', e);
                }
            }

            for (const subdir of subdirectories) {
                const newPath = relativePath ? `${relativePath}/${subdir.name}` : subdir.name;
                await scanWrapper(subdir, songsAcc, newPath, filenamePatternInner);
            }
        }

        await scanWrapper(libraryDirectoryHandle, songs, '', validPattern);

        // Final progress dispatch so UI knows we're complete
        try {
            window.dispatchEvent(new CustomEvent('library-scan-progress', { detail: { scanned: songs.length, complete: true } }));
        } catch (e) {
            // ignore
        }

        console.log(`Library scan complete: ${songs.length} songs found`);
        return songs;
    } catch (error) {
        console.error('Error picking library directory:', error);
        return null;
    }
}

/**
 * Recursively scan directory for MP3 files and their matching CDG files
 * @param {FileSystemDirectoryHandle} directoryHandle 
 * @param {Array} songs - Accumulator array for found songs
 * @param {string} relativePath - Current relative path from library root
 * @param {string} filenamePattern - Pattern for parsing filenames
 */
async function scanDirectoryForSongs(directoryHandle, songs, relativePath = '', filenamePattern = '%artist - %title') {
    try {
        const mp3Files = new Map(); // Map of basename -> {handle, file}
        const cdgFiles = new Set(); // Set of basenames that have CDG files
        const subdirectories = [];

        // First pass: collect all files
        for await (const entry of directoryHandle.values()) {
            if (entry.kind === 'file') {
                const fileName = entry.name.toLowerCase();
                
                if (fileName.endsWith('.mp3')) {
                    const baseName = entry.name.slice(0, -4); // Remove .mp3 extension
                    const file = await entry.getFile();
                    mp3Files.set(baseName, { handle: entry, file: file });
                } else if (fileName.endsWith('.cdg')) {
                    const baseName = entry.name.slice(0, -4); // Remove .cdg extension
                    cdgFiles.add(baseName);
                    } else if (fileName.endsWith('.zip')) {
                        // Simplified ZIP handling: assume a single mp3 + cdg at zip root
                        try {
                            const jszip = await ensureJSZip();
                            const zipFileObj = await entry.getFile();
                            if (typeof zipFileObj.size === 'number' && zipFileObj.size > MAX_ZIP_SIZE) {
                                console.warn('Skipping large ZIP file during scan:', entry.name, `(${zipFileObj.size} bytes)`);
                            } else {
                                const zipBuf = await zipFileObj.arrayBuffer();
                                const zip = await jszip.loadAsync(zipBuf);

                                let mp3Entry = null;
                                let cdgEntry = null;
                                zip.forEach((relativePath) => {
                                    if (relativePath.indexOf('/') !== -1) return;
                                    const lower = relativePath.toLowerCase();
                                    if (!mp3Entry && lower.endsWith('.mp3')) mp3Entry = relativePath;
                                    if (!cdgEntry && lower.endsWith('.cdg')) cdgEntry = relativePath;
                                });

                                if (mp3Entry && cdgEntry) {
                                    const baseName = mp3Entry.substring(0, mp3Entry.length - 4);
                                    let artist = '';
                                    let title = baseName;
                                    try {
                                        const mp3ArrayBuffer = await zip.file(mp3Entry).async('arraybuffer');
                                        const blob = new Blob([mp3ArrayBuffer], { type: 'audio/mpeg' });
                                        const md = await extractMetadata(blob, baseName, filenamePattern);
                                        artist = md.artist; title = md.title;
                                    } catch (e) {
                                        // ignore
                                    }

                                    const zipFilePath = relativePath ? `${relativePath}/${entry.name}` : entry.name;

                                    songs.push({
                                        id: crypto.randomUUID(),
                                        artist: artist,
                                        title: title,
                                        mp3FileName: `${baseName}.mp3`,
                                        cdgFileName: `${baseName}.cdg`,
                                        path: '',
                                        fullPath: baseName,
                                        sourceType: 'zip',
                                        zipFileName: entry.name,
                                        zipFilePath: zipFilePath,
                                        zipEntryMp3Path: mp3Entry,
                                        zipEntryCdgPath: cdgEntry
                                    });
                                }
                            }
                        } catch (e) {
                            console.warn('Failed to read ZIP file during scan:', entry.name, e);
                        }
                }
            } else if (entry.kind === 'directory') {
                subdirectories.push(entry);
            }
        }

        // Second pass: match MP3s with CDGs and extract metadata
        for (const [baseName, mp3Data] of mp3Files) {
            const hasCdg = cdgFiles.has(baseName);
            
            // Only include songs that have both MP3 and CDG files
            if (!hasCdg) {
                continue;
            }
            
            const fullPath = relativePath ? `${relativePath}/${baseName}` : baseName;

            // Extract metadata (ID3 tags or filename parsing)
            const song = await buildDirectorySong(mp3Data, relativePath, filenamePattern);
            songs.push(song);
        }

        // Recursively scan subdirectories
        for (const subdir of subdirectories) {
            const newPath = relativePath ? `${relativePath}/${subdir.name}` : subdir.name;
            await scanDirectoryForSongs(subdir, songs, newPath, filenamePattern);
        }
    } catch (error) {
        console.error(`Error scanning directory ${relativePath}:`, error);
    }
}

/**
 * Get the library directory handle (for loading files during playback)
 * @returns {FileSystemDirectoryHandle|null}
 */
export function getLibraryDirectoryHandle() {
    return libraryDirectoryHandle;
}

/**
 * Load MP3 and CDG file data from the library directory for a specific song
 * @param {string} path - Relative path to the files
 * @param {string} mp3FileName - MP3 filename
 * @param {string} cdgFileName - CDG filename
 * @returns {Promise<{mp3Data: Uint8Array, cdgData: Uint8Array}>}
 */
export async function loadSongFiles(path, mp3FileName, cdgFileName, zipInfo = null) {
    try {
        if (!libraryDirectoryHandle) {
            throw new Error('No library directory selected');
        }
        // If zipInfo is provided (zip-origin song), lazily extract from ZIP instead
        if (zipInfo && zipInfo.zipFileName) {
            // zipInfo: { zipFileName, zipEntryMp3Path, zipEntryCdgPath, zipFilePath }
            // If the ZIP is inside a subdirectory, navigate to its directory first
            let zipHandle = null;
            try {
                if (zipInfo.zipFilePath) {
                    // zipFilePath may include subdirectories like 'dir/song.zip'
                    const parts = zipInfo.zipFilePath.split('/');
                    const fileName = parts.pop();
                    let dir = libraryDirectoryHandle;
                    for (const part of parts) {
                        dir = await dir.getDirectoryHandle(part);
                    }
                    zipHandle = await dir.getFileHandle(fileName);
                } else {
                    zipHandle = await libraryDirectoryHandle.getFileHandle(zipInfo.zipFileName);
                }
            } catch (err) {
                throw new Error(`ZIP file not found in library: ${zipInfo.zipFilePath || zipInfo.zipFileName}`);
            }
            const zipFileObj = await zipHandle.getFile();
            if (typeof zipFileObj.size === 'number' && zipFileObj.size > MAX_ZIP_SIZE) {
                throw new Error(`ZIP file too large to extract in-memory: ${zipInfo.zipFileName} (${zipFileObj.size} bytes)`);
            }
            const zipBuf = await zipFileObj.arrayBuffer();
            const jszip = await ensureJSZip();
            const zip = await jszip.loadAsync(zipBuf);

            // Extract CDG as ArrayBuffer using the provided zip entry path
            const cdgEntry = zip.file(zipInfo.zipEntryCdgPath);
            const mp3Entry = zip.file(zipInfo.zipEntryMp3Path);
            if (!cdgEntry || !mp3Entry) {
                // collect root entries for diagnostics
                const rootEntries = [];
                zip.forEach((relPath) => {
                    if (relPath.indexOf('/') !== -1) return; // only root
                    rootEntries.push(relPath);
                });
                throw new Error(`ZIP entries not found. Expected mp3='${zipInfo.zipEntryMp3Path}', cdg='${zipInfo.zipEntryCdgPath}'. Root entries: ${rootEntries.join(', ')}`);
            }
            const cdgArrayBuffer = await cdgEntry.async('arraybuffer');
            const loadedCdgData = new Uint8Array(cdgArrayBuffer);

            // Extract MP3 as ArrayBuffer and create blob + bytes for player
            const mp3ArrayBuffer = await zip.file(zipInfo.zipEntryMp3Path).async('arraybuffer');
            const loadedMp3Data = new Uint8Array(mp3ArrayBuffer);
            const mp3Blob = new Blob([mp3ArrayBuffer], { type: 'audio/mpeg' });
            const mp3Url = URL.createObjectURL(mp3Blob);

            // Store cdg and mp3 bytes in module-level variables
            cdgData = loadedCdgData;
            mp3Data = loadedMp3Data;

            return {
                mp3Url,
                mp3Blob,
                cdgData: loadedCdgData
            };
        }

        // Navigate to the correct subdirectory for directory-origin songs
        let currentDir = libraryDirectoryHandle;
        if (path) {
            const pathParts = path.split('/');
            for (const part of pathParts) {
                try {
                    currentDir = await currentDir.getDirectoryHandle(part);
                } catch (err) {
                    throw new Error(`Directory not found while loading song files: ${part} (full path: ${path})`);
                }
            }
        }

        // Load MP3 file
        let mp3FileHandle;
        let loadedMp3Data;
        try {
            mp3FileHandle = await currentDir.getFileHandle(mp3FileName);
            const mp3File = await mp3FileHandle.getFile();
            const mp3ArrayBuffer = await mp3File.arrayBuffer();
            loadedMp3Data = new Uint8Array(mp3ArrayBuffer);

            // continue to load cdg below using directory handles
        } catch (err) {
            // Directory file wasn't found. Attempt sessionStorage fallback: if the library
            // contains a ZIP-origin entry for this mp3, extract it from the ZIP.
            try {
                const params = new URLSearchParams(window.location.search);
                const sessionId = params.get('session');
                if (sessionId) {
                    const key = `karamel-session-${sessionId}`;
                    const stored = sessionStorage.getItem(key);
                    if (stored) {
                        const state = JSON.parse(stored);
                        const lib = state.library;
                        const songs = lib?.songs || [];
                        const match = songs.find(s => (s.mp3FileName === mp3FileName) || (s.fullPath && `${s.fullPath}.mp3` === mp3FileName) || (s.fullPath === mp3FileName.replace(/\.mp3$/i, '')));
                        if (match && (match.zipFileName || match.zipFilePath)) {
                            const zi = {
                                zipFileName: match.zipFileName || (match.zipFilePath ? match.zipFilePath.split('/').pop() : null),
                                zipEntryMp3Path: match.zipEntryMp3Path || match.mp3FileName,
                                zipEntryCdgPath: match.zipEntryCdgPath || match.cdgFileName,
                                zipFilePath: match.zipFilePath
                            };

                            // Locate ZIP file handle (may be in subdirectory)
                            let zipHandle = null;
                            try {
                                if (zi.zipFilePath) {
                                    const parts = zi.zipFilePath.split('/');
                                    const fileName = parts.pop();
                                    let dir = libraryDirectoryHandle;
                                    for (const part of parts) {
                                        dir = await dir.getDirectoryHandle(part);
                                    }
                                    zipHandle = await dir.getFileHandle(fileName);
                                } else {
                                    zipHandle = await libraryDirectoryHandle.getFileHandle(zi.zipFileName);
                                }
                            } catch (err2) {
                                // fallthrough to original error below
                                zipHandle = null;
                            }

                            if (zipHandle) {
                                const zipFileObj = await zipHandle.getFile();
                                if (typeof zipFileObj.size === 'number' && zipFileObj.size > MAX_ZIP_SIZE) {
                                    throw new Error(`ZIP file too large to extract in-memory: ${zi.zipFileName} (${zipFileObj.size} bytes)`);
                                }
                                const zipBuf = await zipFileObj.arrayBuffer();
                                const jszip = await ensureJSZip();
                                const zip = await jszip.loadAsync(zipBuf);

                                const cdgEntry = zip.file(zi.zipEntryCdgPath);
                                const mp3Entry = zip.file(zi.zipEntryMp3Path);
                                if (!cdgEntry || !mp3Entry) {
                                    const rootEntries = [];
                                    zip.forEach((relPath) => {
                                        if (relPath.indexOf('/') !== -1) return;
                                        rootEntries.push(relPath);
                                    });
                                    throw new Error(`ZIP entries not found during fallback. Expected mp3='${zi.zipEntryMp3Path}', cdg='${zi.zipEntryCdgPath}'. Root entries: ${rootEntries.join(', ')}`);
                                }

                                const cdgArrayBuffer = await cdgEntry.async('arraybuffer');
                                const loadedCdgData = new Uint8Array(cdgArrayBuffer);

                                const mp3ArrayBuffer = await mp3Entry.async('arraybuffer');
                                const loadedMp3Data = new Uint8Array(mp3ArrayBuffer);
                                const mp3Blob = new Blob([mp3ArrayBuffer], { type: 'audio/mpeg' });
                                const mp3Url = URL.createObjectURL(mp3Blob);

                                // Store cdg and mp3 bytes in module-level variables
                                cdgData = loadedCdgData;
                                mp3Data = loadedMp3Data;

                                return {
                                    mp3Url,
                                    mp3Blob,
                                    cdgData: loadedCdgData
                                };
                            }
                        }
                    }
                }
            } catch (fallbackErr) {
                console.warn('Fallback lookup failed:', fallbackErr);
            }

            throw new Error(`MP3 file not found: ${mp3FileName} (path: ${path || '<root>'})`);
        }

        // Load CDG file
        let cdgFileHandle;
        try {
            cdgFileHandle = await currentDir.getFileHandle(cdgFileName);
        } catch (err) {
            throw new Error(`CDG file not found: ${cdgFileName} (path: ${path || '<root>'})`);
        }
        const cdgFile = await cdgFileHandle.getFile();
        const cdgArrayBuffer = await cdgFile.arrayBuffer();
        const loadedCdgData = new Uint8Array(cdgArrayBuffer);

        // Store in module-level variables for player access
        mp3Data = loadedMp3Data;
        cdgData = loadedCdgData;

        return {
            mp3Data: loadedMp3Data,
            cdgData: loadedCdgData
        };
    } catch (error) {
        console.error('Error loading song files:', error);
        // Preserve original error for callers, but ensure it's an Error instance with message
        if (error instanceof Error) throw error;
        throw new Error(String(error));
    }
}
