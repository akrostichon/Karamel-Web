// File System Access API wrapper for loading MP3 and CDG files
// Store file data in module-level variables to avoid JSON serialization issues

import { extractMetadata, validatePattern } from './metadata.js';
import * as byteStore from './byteStore.js';
import * as zipHelper from './zipHelper.js';
import * as dirHelper from './dirHelper.js';

let libraryDirectoryHandle = null; // Keep directory handle for session-long access
const MAX_ZIP_SIZE = 20 * 1024 * 1024; // 20 MB limit for in-memory unzip (kept for local checks)

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
        // Create a File with a name so ID3 readers can access tags and filename fallbacks
        const metaFileName = `${zipFileName}/${mp3EntryPath}`;
        const metaFile = new File([mp3ArrayBuffer], mp3EntryPath, { type: 'audio/mpeg' });
        const md = await extractMetadata(metaFile, metaFileName, filenamePattern);
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
    // Delegate to zipHelper which centralizes import/fallback behavior
    return zipHelper.ensureZipModule();
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
        const bytes = new Uint8Array(arrayBuffer);
        byteStore.setBytes('mp3', bytes);

        return { name: file.name, size: file.size };
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
        const bytes = new Uint8Array(arrayBuffer);
        byteStore.setBytes('cdg', bytes);
        return { name: file.name, size: file.size };
    } catch (error) {
        console.error('Error picking CDG file:', error);
        return null;
    }
}

export function getMp3Data() {
    return byteStore.getBytes('mp3');
}

export function getCdgData() {
    return byteStore.getBytes('cdg');
}

export function hasFiles() {
    return !!byteStore.getBytes('mp3') && !!byteStore.getBytes('cdg');
}

/**
 * Pick a library directory and scan for karaoke files (MP3 + CDG pairs)
 * @param {string} filenamePattern - Pattern for parsing filenames (default: "%artist - %title")
 * @returns {Promise<Array>} Array of song metadata objects
 */
export async function pickLibraryDirectory(filenamePattern = '%artist - %title', progressStep = 10) {
    try {
        // Request directory access
        libraryDirectoryHandle = await window.showDirectoryPicker({ mode: 'read' });

        // Validate pattern
        const validPattern = validatePattern(filenamePattern);

        // Recursively scan for songs
        const songs = [];
        let matchedCount = 0;

        async function scanWrapper(directoryHandle, songsAcc, relativePath = '', filenamePatternInner = '%artist - %title') {
            const mp3Files = new Map();
            const cdgFiles = new Set();
            const subdirectories = [];

            for await (const entry of directoryHandle.values()) {
                if (entry.kind === 'file') {
                    const fileName = entry.name.toLowerCase();

                    if (fileName.endsWith('.mp3')) {
                        const baseName = entry.name.slice(0, -4);
                        const file = await entry.getFile();
                        mp3Files.set(baseName, { handle: entry, file: file });
                    } else if (fileName.endsWith('.cdg')) {
                        const baseName = entry.name.slice(0, -4);
                        cdgFiles.add(baseName);
                    } else if (fileName.endsWith('.zip')) {
                        // ZIP scanning: use zipHelper to detect root entries without extracting full contents
                        try {
                            const zipFileObj = await entry.getFile();
                            if (typeof zipFileObj.size === 'number' && zipHelper.isZipTooLarge(zipFileObj.size, MAX_ZIP_SIZE)) {
                                console.warn('Skipping large ZIP file during scan:', entry.name, `(${zipFileObj.size} bytes)`);
                            } else {
                                const zipBuf = await zipFileObj.arrayBuffer();
                                const pair = await zipHelper.findMp3CdgRootPairFromBuffer(zipBuf);
                                if (pair && pair.mp3Entry && pair.cdgEntry) {
                                    // load JSZip instance to allow reading ID3 metadata from mp3 entry
                                    const jszip = await zipHelper.ensureZipModule();
                                    const zip = await jszip.loadAsync(zipBuf);
                                    const zipFilePath = relativePath ? `${relativePath}/${entry.name}` : entry.name;
                                    const song = await buildZipSong(zip, entry.name, zipFilePath, pair.mp3Entry, pair.cdgEntry, filenamePatternInner);
                                    songsAcc.push(song);
                                    matchedCount++;
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

            for (const [baseName, mp3Data] of mp3Files) {
                const hasCdg = cdgFiles.has(baseName);
                if (!hasCdg) continue;
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

        try {
            window.dispatchEvent(new CustomEvent('library-scan-progress', { detail: { scanned: songs.length, complete: true } }));
        } catch (e) { /* ignore */ }

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
                        try {
                            const zipFileObj = await entry.getFile();
                            if (typeof zipFileObj.size === 'number' && zipHelper.isZipTooLarge(zipFileObj.size, MAX_ZIP_SIZE)) {
                                console.warn('Skipping large ZIP file during scan:', entry.name, `(${zipFileObj.size} bytes)`);
                            } else {
                                const zipBuf = await zipFileObj.arrayBuffer();
                                const pair = await zipHelper.findMp3CdgRootPairFromBuffer(zipBuf);
                                if (pair && pair.mp3Entry && pair.cdgEntry) {
                                    const baseName = pair.mp3Entry.substring(0, pair.mp3Entry.length - 4);
                                    let artist = '';
                                    let title = baseName;
                                    try {
                                        const jszip = await zipHelper.ensureZipModule();
                                        const zipInstance = await jszip.loadAsync(zipBuf);
                                        const mp3ArrayBuffer = await zipInstance.file(pair.mp3Entry).async('arraybuffer');
                                        const metaFileName = `${zipFilePath}/${pair.mp3Entry}`;
                                        const metaFile = new File([mp3ArrayBuffer], pair.mp3Entry, { type: 'audio/mpeg' });
                                        const md = await extractMetadata(metaFile, metaFileName, filenamePattern);
                                        artist = md.artist; title = md.title;
                                    } catch (e) { /* ignore */ }
                                    const zipFilePath = relativePath ? `${relativePath}/${entry.name}` : entry.name;
                                    songs.push({
                                        id: crypto.randomUUID(),
                                        artist,
                                        title,
                                        mp3FileName: `${baseName}.mp3`,
                                        cdgFileName: `${baseName}.cdg`,
                                        path: '',
                                        fullPath: baseName,
                                        sourceType: 'zip',
                                        zipFileName: entry.name,
                                        zipFilePath: zipFilePath,
                                        zipEntryMp3Path: pair.mp3Entry,
                                        zipEntryCdgPath: pair.cdgEntry
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
        if (!libraryDirectoryHandle) throw new Error('No library directory selected');

        // If zipInfo is provided (zip-origin song), lazily extract from ZIP instead
        if (zipInfo && zipInfo.zipFileName) {
            // Resolve ZIP file handle using dirHelper
            let zipHandle = null;
            try {
                if (zipInfo.zipFilePath) {
                    zipHandle = await dirHelper.getFileHandleByPath(libraryDirectoryHandle, zipInfo.zipFilePath);
                } else {
                    zipHandle = await libraryDirectoryHandle.getFileHandle(zipInfo.zipFileName);
                }
            } catch (err) {
                throw new Error(`ZIP file not found in library: ${zipInfo.zipFilePath || zipInfo.zipFileName}`);
            }

            const zipFileObj = await zipHandle.getFile();
            if (typeof zipFileObj.size === 'number' && zipHelper.isZipTooLarge(zipFileObj.size, MAX_ZIP_SIZE)) {
                throw new Error(`ZIP file too large to extract in-memory: ${zipInfo.zipFileName} (${zipFileObj.size} bytes)`);
            }
            const zipBuf = await zipFileObj.arrayBuffer();
            // Use zipHelper to extract entries
            const { mp3Bytes, cdgBytes, mp3Blob } = await zipHelper.extractEntriesFromBuffer(zipBuf, zipInfo.zipEntryMp3Path, zipInfo.zipEntryCdgPath);

            // Store into byteStore
            byteStore.setBytes('mp3', mp3Bytes);
            byteStore.setBytes('cdg', cdgBytes);

            const mp3Url = byteStore.createObjectUrl('mp3', 'audio/mpeg');
            return { mp3Data: mp3Bytes, cdgData: cdgBytes, mp3Url };
        }

        // Navigate to the correct subdirectory for directory-origin songs
        let currentDir = libraryDirectoryHandle;
        if (path) {
            try {
                currentDir = await dirHelper.getDirectoryHandleByPath(libraryDirectoryHandle, path);
            } catch (err) {
                throw new Error(`Directory not found while loading song files: ${path}`);
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
        } catch (err) {
            // Attempt sessionStorage fallback for ZIP-origin songs
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

                            // Locate ZIP file handle using dirHelper
                            let zipHandle = null;
                            try {
                                if (zi.zipFilePath) {
                                    zipHandle = await dirHelper.getFileHandleByPath(libraryDirectoryHandle, zi.zipFilePath);
                                } else {
                                    zipHandle = await libraryDirectoryHandle.getFileHandle(zi.zipFileName);
                                }
                            } catch (err2) {
                                zipHandle = null;
                            }

                            if (zipHandle) {
                                const zipFileObj = await zipHandle.getFile();
                                if (typeof zipFileObj.size === 'number' && zipHelper.isZipTooLarge(zipFileObj.size, MAX_ZIP_SIZE)) {
                                    throw new Error(`ZIP file too large to extract in-memory: ${zi.zipFileName} (${zipFileObj.size} bytes)`);
                                }
                                const zipBuf = await zipFileObj.arrayBuffer();
                                const { mp3Bytes, cdgBytes } = await zipHelper.extractEntriesFromBuffer(zipBuf, zi.zipEntryMp3Path, zi.zipEntryCdgPath);

                                byteStore.setBytes('mp3', mp3Bytes);
                                byteStore.setBytes('cdg', cdgBytes);
                                const mp3Url = byteStore.createObjectUrl('mp3', 'audio/mpeg');
                                return { mp3Data: mp3Bytes, cdgData: cdgBytes, mp3Url };
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

        // Store in byteStore for player access
        byteStore.setBytes('mp3', loadedMp3Data);
        byteStore.setBytes('cdg', loadedCdgData);

        return { mp3Data: loadedMp3Data, cdgData: loadedCdgData };
    } catch (error) {
        console.error('Error loading song files:', error);
        // Preserve original error for callers, but ensure it's an Error instance with message
        if (error instanceof Error) throw error;
        throw new Error(String(error));
    }
}
