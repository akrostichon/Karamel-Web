// File System Access API wrapper for loading MP3 and CDG files
// Store file data in module-level variables to avoid JSON serialization issues

import { createLogger } from './logger.js';
import { extractMetadata, validatePattern, flushID3FailureLog, clearID3FailureLog } from './metadata.js';
import * as byteStore from './byteStore.js';
import * as zipHelper from './zipHelper.js';
import * as dirHelper from './dirHelper.js';

const logger = createLogger('FileAccess');

let libraryDirectoryHandle = null; // Keep directory handle for session-long access
const MAX_ZIP_SIZE = 20 * 1024 * 1024; // 20 MB limit for in-memory unzip (kept for local checks)
const MAX_VIDEO_SIZE = 500 * 1024 * 1024; // 500 MB limit for video files

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

// Helper: build a song object for a video file
async function buildVideoSong(videoFileData, relativePath, filenamePattern) {
    const fileObj = videoFileData.file;
    const extension = videoFileData.extension;
    const baseName = fileObj.name.slice(0, -(extension.length));
    const fullPath = relativePath ? `${relativePath}/${baseName}` : baseName;
    
    // Use existing extractMetadata to parse artist/title from filename
    const metadata = await extractMetadata(fileObj, fullPath, filenamePattern);
    
    return {
        id: crypto.randomUUID(),
        artist: metadata.artist,
        title: metadata.title,
        mediaType: 'video',
        videoFileName: fileObj.name,
        videoExtension: extension,
        mp3FileName: null,
        cdgFileName: null,
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
        logger.error('Error picking MP3 file', { error: error.message || String(error) });
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
        logger.error('Error picking CDG file', { error: error.message || String(error) });
        return null;
    }
}

// ======================== LIBRARY SCANNING HELPERS ========================

/**
 * Report library scan progress
 * @private
 */
function reportScanProgress(matchedCount, progressStep, isComplete = false) {
    try {
        const detail = isComplete 
            ? { scanned: matchedCount, complete: true }
            : { scanned: matchedCount };
        window.dispatchEvent(new CustomEvent('library-scan-progress', { detail }));
    } catch (e) {
        logger.warn('Failed to dispatch library-scan-progress event', { error: e.message || String(e) });
    }
}

/**
 * Process a ZIP file entry and extract song metadata
 * @private
 */
async function processZipEntry(entry, relativePath, filenamePattern) {
    const zipFileObj = await entry.getFile();
    
    if (typeof zipFileObj.size === 'number' && zipHelper.isZipTooLarge(zipFileObj.size, MAX_ZIP_SIZE)) {
        logger.warn('Skipping large ZIP file during scan', { fileName: entry.name, size: zipFileObj.size });
        return null;
    }

    const zipBuf = await zipFileObj.arrayBuffer();
    const pair = await zipHelper.findMp3CdgRootPairFromBuffer(zipBuf);
    
    if (!pair || !pair.mp3Entry || !pair.cdgEntry) {
        return null;
    }

    const jszip = await zipHelper.ensureZipModule();
    const zip = await jszip.loadAsync(zipBuf);
    const zipFilePath = relativePath ? `${relativePath}/${entry.name}` : entry.name;
    
    return await buildZipSong(zip, entry.name, zipFilePath, pair.mp3Entry, pair.cdgEntry, filenamePattern);
}

/**
 * Categorize directory entries into MP3 files, CDG files, video files, ZIP files, and subdirectories
 * @private
 */
async function categorizeDirectoryEntries(directoryHandle) {
    const mp3Files = new Map();
    const cdgFiles = new Set();
    const videoFiles = new Map();
    const zipFiles = [];
    const subdirectories = [];

    for await (const entry of directoryHandle.values()) {
        if (entry.kind === 'file') {
            const fileName = entry.name.toLowerCase();

            if (fileName.endsWith('.mp3')) {
                const baseName = entry.name.slice(0, -4);
                const file = await entry.getFile();
                mp3Files.set(baseName, { handle: entry, file });
            } else if (fileName.endsWith('.cdg')) {
                const baseName = entry.name.slice(0, -4);
                cdgFiles.add(baseName);
            } else if (fileName.endsWith('.mp4') || fileName.endsWith('.m4v')) {
                const extension = fileName.endsWith('.mp4') ? '.mp4' : '.m4v';
                const file = await entry.getFile();
                
                // Check file size - skip videos > 500MB
                if (file.size > MAX_VIDEO_SIZE) {
                    console.warn(`Skipping large video (${(file.size / (1024 * 1024)).toFixed(1)}MB): ${entry.name}`);
                    continue;
                }
                
                const baseName = entry.name.slice(0, -(extension.length));
                videoFiles.set(baseName, { handle: entry, file, extension });
            } else if (fileName.endsWith('.zip')) {
                zipFiles.push(entry);
            }
        } else if (entry.kind === 'directory') {
            subdirectories.push(entry);
        }
    }

    return { mp3Files, cdgFiles, videoFiles, zipFiles, subdirectories };
}

/**
 * Process MP3/CDG pairs and add matching songs to the collection
 * @private
 */
async function processMp3CdgPairs(mp3Files, cdgFiles, relativePath, filenamePattern, songsAcc, progressStep, matchedCountRef) {
    const BATCH_SIZE = 20;
    
    // Filter to only matching pairs
    const matchingPairs = [];
    for (const [baseName, mp3Data] of mp3Files) {
        if (cdgFiles.has(baseName)) {
            matchingPairs.push({ baseName, mp3Data });
        }
    }
    
    // Process pairs in parallel batches
    for (let i = 0; i < matchingPairs.length; i += BATCH_SIZE) {
        const batch = matchingPairs.slice(i, i + BATCH_SIZE);
        
        const batchPromises = batch.map(async ({ baseName, mp3Data }) => {
            try {
                const song = await buildDirectorySong(mp3Data, relativePath, filenamePattern);
                return song;
            } catch (error) {
                logger.warn('Failed to process MP3/CDG pair', { baseName, error: error.message || String(error) });
                return null;
            }
        });
        
        const batchResults = await Promise.all(batchPromises);
        
        // Add successful songs to collection and update progress
        for (const song of batchResults) {
            if (song) {
                songsAcc.push(song);
                matchedCountRef.count++;
                
                if (matchedCountRef.count % progressStep === 0) {
                    reportScanProgress(matchedCountRef.count, progressStep);
                }
            }
        }
    }
}

/**
 * Process ZIP files and add valid songs to the collection
 * @private
 */
async function processZipFiles(zipFiles, relativePath, filenamePattern, songsAcc, matchedCountRef) {
    for (const zipEntry of zipFiles) {
        try {
            const song = await processZipEntry(zipEntry, relativePath, filenamePattern);
            if (song) {
                songsAcc.push(song);
                matchedCountRef.count++;
            }
        } catch (e) {
            logger.warn('Failed to read ZIP file during scan', { fileName: zipEntry.name, error: e.message || String(e) });
        }
    }
}

/**
 * Process video files and add them to the collection
 * @private
 */
async function processVideoFiles(videoFiles, relativePath, filenamePattern, songsAcc, matchedCountRef) {
    for (const [baseName, videoData] of videoFiles) {
        try {
            const song = await buildVideoSong(videoData, relativePath, filenamePattern);
            if (song) {
                songsAcc.push(song);
                matchedCountRef.count++;
            }
        } catch (error) {
            logger.warn('Failed to process video file', { baseName, error: error.message || String(error) });
        }
    }
}

/**
 * Pick a library directory and scan for karaoke files (MP3 + CDG pairs)
 * @param {string} filenamePattern - Pattern for parsing filenames (default: "%artist - %title")
 * @param {number} progressStep - Frequency of progress updates (default: 10)
 * @returns {Promise<Array>} Array of song metadata objects
 */
export async function pickLibraryDirectory(filenamePattern = '%artist - %title', progressStep = 10) {
    // Clear any previous ID3 tag failures from prior scans
    clearID3FailureLog();
    
    try {
        libraryDirectoryHandle = await window.showDirectoryPicker({ mode: 'read' });
        const validPattern = validatePattern(filenamePattern);
        
        const songs = [];
        const matchedCountRef = { count: 0 };

        async function scanDirectory(directoryHandle, songsAcc, relativePath = '') {
            const { mp3Files, cdgFiles, videoFiles, zipFiles, subdirectories } = 
                await categorizeDirectoryEntries(directoryHandle);

            // Process ZIP files first (they're already complete songs)
            await processZipFiles(zipFiles, relativePath, validPattern, songsAcc, matchedCountRef);

            // Process MP3/CDG pairs
            await processMp3CdgPairs(mp3Files, cdgFiles, relativePath, validPattern, songsAcc, progressStep, matchedCountRef);

            // Process video files
            await processVideoFiles(videoFiles, relativePath, validPattern, songsAcc, matchedCountRef);

            // Recursively scan subdirectories
            for (const subdir of subdirectories) {
                const newPath = relativePath ? `${relativePath}/${subdir.name}` : subdir.name;
                await scanDirectory(subdir, songsAcc, newPath);
            }
        }

        await scanDirectory(libraryDirectoryHandle, songs);
        // Flush batched ID3 tag failures to console (after scan completes)
        flushID3FailureLog();
        
        reportScanProgress(songs.length, progressStep, true);

        logger.debug('Library scan complete', { songsFound: songs.length });
        return songs;
    } catch (error) {
        logger.error('Error picking library directory', { error: error.message || String(error) });
        return null;
    }
}

/**
 * Get the library directory handle (for loading files during playback)
 * @returns {FileSystemDirectoryHandle|null}
 */
export function getLibraryDirectoryHandle() {
    return libraryDirectoryHandle;
}

// ======================== PRIVATE HELPERS ========================

/**
 * Track telemetry event for song file loading
 * @private
 */
function trackLoadTelemetry(eventName, origin, mp3FileName, cdgFileName, duration, additionalProps = {}) {
    if (!window.appInsights) return;
    
    window.appInsights.trackEvent({
        name: eventName,
        properties: {
            origin,
            mp3FileName,
            cdgFileName,
            durationMs: Math.round(duration),
            ...additionalProps
        }
    });
}

/**
 * Resolve ZIP file handle from library directory
 * @private
 */
async function resolveZipHandle(zipInfo) {
    if (zipInfo.zipFilePath) {
        return await dirHelper.getFileHandleByPath(libraryDirectoryHandle, zipInfo.zipFilePath);
    }
    return await libraryDirectoryHandle.getFileHandle(zipInfo.zipFileName);
}

/**
 * Load and extract song files from ZIP archive
 * @private
 */
async function loadFromZip(zipInfo, mp3FileName, cdgFileName) {
    let zipHandle;
    try {
        zipHandle = await resolveZipHandle(zipInfo);
    } catch (err) {
        throw new Error(`ZIP file not found in library: ${zipInfo.zipFilePath || zipInfo.zipFileName}`);
    }

    const zipFileObj = await zipHandle.getFile();
    if (zipHelper.isZipTooLarge(zipFileObj.size, MAX_ZIP_SIZE)) {
        throw new Error(`ZIP file too large to extract in-memory: ${zipInfo.zipFileName} (${zipFileObj.size} bytes)`);
    }

    const zipBuf = await zipFileObj.arrayBuffer();
    const { mp3Bytes, cdgBytes } = await zipHelper.extractEntriesFromBuffer(
        zipBuf,
        zipInfo.zipEntryMp3Path,
        zipInfo.zipEntryCdgPath
    );

    return { mp3Bytes, cdgBytes };
}

/**
 * Attempt to load ZIP-origin song from sessionStorage fallback
 * @private
 */
async function trySessionStorageFallback(mp3FileName, cdgFileName) {
    const params = new URLSearchParams(window.location.search);
    const sessionId = params.get('session');
    if (!sessionId) return null;

    const stored = sessionStorage.getItem(`karamel-session-${sessionId}`);
    if (!stored) return null;

    const state = JSON.parse(stored);
    const songs = state?.library?.songs || [];
    
    const match = songs.find(s => 
        s.mp3FileName === mp3FileName ||
        (s.fullPath && `${s.fullPath}.mp3` === mp3FileName) ||
        (s.fullPath === mp3FileName.replace(/\.mp3$/i, ''))
    );

    if (!match || (!match.zipFileName && !match.zipFilePath)) return null;

    const zipInfo = {
        zipFileName: match.zipFileName || match.zipFilePath?.split('/').pop(),
        zipEntryMp3Path: match.zipEntryMp3Path || match.mp3FileName,
        zipEntryCdgPath: match.zipEntryCdgPath || match.cdgFileName,
        zipFilePath: match.zipFilePath
    };

    try {
        return await loadFromZip(zipInfo, mp3FileName, cdgFileName);
    } catch (err) {
        logger.warn('SessionStorage fallback failed', { error: err.message || String(err) });
        return null;
    }
}

/**
 * Load song files from file system directory
 * @private
 */
async function loadFromDirectory(path, mp3FileName, cdgFileName) {
    // Navigate to subdirectory if path provided
    let currentDir = libraryDirectoryHandle;
    if (path) {
        try {
            currentDir = await dirHelper.getDirectoryHandleByPath(libraryDirectoryHandle, path);
        } catch (err) {
            throw new Error(`Directory not found while loading song files: ${path}`);
        }
    }

    // Load MP3 file
    let mp3Data;
    try {
        const mp3Handle = await currentDir.getFileHandle(mp3FileName);
        const mp3File = await mp3Handle.getFile();
        const mp3ArrayBuffer = await mp3File.arrayBuffer();
        mp3Data = new Uint8Array(mp3ArrayBuffer);
    } catch (err) {
        // Try sessionStorage fallback for ZIP-origin songs
        const fallbackResult = await trySessionStorageFallback(mp3FileName, cdgFileName);
        if (fallbackResult) {
            return fallbackResult;
        }
        throw new Error(`MP3 file not found: ${mp3FileName} (path: ${path || '<root>'})`);
    }

    // Load CDG file
    let cdgHandle;
    try {
        cdgHandle = await currentDir.getFileHandle(cdgFileName);
    } catch (err) {
        throw new Error(`CDG file not found: ${cdgFileName} (path: ${path || '<root>'})`);
    }
    
    const cdgFile = await cdgHandle.getFile();
    const cdgArrayBuffer = await cdgFile.arrayBuffer();
    const cdgData = new Uint8Array(cdgArrayBuffer);

    return { mp3Bytes: mp3Data, cdgBytes: cdgData };
}

/**
 * Store loaded song data in byteStore for player access
 * @private
 */
function storeSongData(mp3Bytes, cdgBytes) {
    byteStore.setBytes('mp3', mp3Bytes);
    byteStore.setBytes('cdg', cdgBytes);
    return byteStore.createObjectUrl('mp3', 'audio/mpeg');
}

// ======================== PUBLIC API ========================

/**
 * Load MP3 and CDG file data from the library directory for a specific song
 * @param {string} path - Relative path to the files
 * @param {string} mp3FileName - MP3 filename
 * @param {string} cdgFileName - CDG filename
 * @param {Object} [zipInfo] - ZIP metadata if song is from ZIP archive
 * @returns {Promise<{mp3Data: Uint8Array, cdgData: Uint8Array, mp3Url?: string}>}
 */
export async function loadSongFiles(path, mp3FileName, cdgFileName, zipInfo = null) {
    const startTime = performance.now();
    const origin = zipInfo?.zipFileName ? 'zip' : 'directory';
    
    try {
        if (!libraryDirectoryHandle) {
            throw new Error('No library directory selected');
        }

        // Delegate to appropriate loader based on song origin
        const { mp3Bytes, cdgBytes } = zipInfo?.zipFileName
            ? await loadFromZip(zipInfo, mp3FileName, cdgFileName)
            : await loadFromDirectory(path, mp3FileName, cdgFileName);

        // Store in byteStore and create object URL
        const mp3Url = storeSongData(mp3Bytes, cdgBytes);

        // Track successful load
        const duration = performance.now() - startTime;
        trackLoadTelemetry('SongFileLoaded', origin, mp3FileName, cdgFileName, duration, {
            path: path || '<root>',
            ...(zipInfo?.zipFileName && { zipFileName: zipInfo.zipFileName })
        });

        return { mp3Data: mp3Bytes, cdgData: cdgBytes, mp3Url };

    } catch (error) {
        // Track failed load
        const duration = performance.now() - startTime;
        trackLoadTelemetry('SongFileLoadFailed', origin, mp3FileName, cdgFileName, duration, {
            errorMessage: error instanceof Error ? error.message : String(error)
        });
        
        logger.error('Error loading song files', { 
            mp3FileName, 
            cdgFileName, 
            path: path || '<root>',
            origin,
            error: error.message || String(error) 
        });
        throw error instanceof Error ? error : new Error(String(error));
    }
}
