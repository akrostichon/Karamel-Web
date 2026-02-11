// File System Access API wrapper for loading MP3 and CDG files
// Store file data in module-level variables to avoid JSON serialization issues

import { extractMetadata, validatePattern, flushID3FailureLog, clearID3FailureLog } from './metadata.js';
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
        console.warn('Failed to dispatch library-scan-progress event', e);
    }
}

/**
 * Process a ZIP file entry and extract song metadata
 * @private
 */
async function processZipEntry(entry, relativePath, filenamePattern) {
    const zipFileObj = await entry.getFile();
    
    if (typeof zipFileObj.size === 'number' && zipHelper.isZipTooLarge(zipFileObj.size, MAX_ZIP_SIZE)) {
        console.warn('Skipping large ZIP file during scan:', entry.name, `(${zipFileObj.size} bytes)`);
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
 * Categorize directory entries into MP3 files, CDG files, ZIP files, and subdirectories
 * @private
 */
async function categorizeDirectoryEntries(directoryHandle) {
    const mp3Files = new Map();
    const cdgFiles = new Set();
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
            } else if (fileName.endsWith('.zip')) {
                zipFiles.push(entry);
            }
        } else if (entry.kind === 'directory') {
            subdirectories.push(entry);
        }
    }

    return { mp3Files, cdgFiles, zipFiles, subdirectories };
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
                console.warn(`Failed to process MP3/CDG pair: ${baseName}`, error);
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
            console.warn('Failed to read ZIP file during scan:', zipEntry.name, e);
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
    performance.mark('library-scan-start');
    
    // Clear any previous ID3 tag failures from prior scans
    clearID3FailureLog();
    
    try {
        libraryDirectoryHandle = await window.showDirectoryPicker({ mode: 'read' });
        performance.mark('directory-picker-complete');
        
        const validPattern = validatePattern(filenamePattern);
        
        const songs = [];
        const matchedCountRef = { count: 0 };

        async function scanDirectory(directoryHandle, songsAcc, relativePath = '') {
            performance.mark('categorize-start');
            const { mp3Files, cdgFiles, zipFiles, subdirectories } = 
                await categorizeDirectoryEntries(directoryHandle);
            performance.mark('categorize-complete');

            // Process ZIP files first (they're already complete songs)
            performance.mark('zip-processing-start');
            await processZipFiles(zipFiles, relativePath, validPattern, songsAcc, matchedCountRef);
            performance.mark('zip-processing-complete');

            // Process MP3/CDG pairs
            performance.mark('pairs-processing-start');
            await processMp3CdgPairs(mp3Files, cdgFiles, relativePath, validPattern, songsAcc, progressStep, matchedCountRef);
            performance.mark('pairs-processing-complete');

            // Recursively scan subdirectories
            for (const subdir of subdirectories) {
                const newPath = relativePath ? `${relativePath}/${subdir.name}` : subdir.name;
                await scanDirectory(subdir, songsAcc, newPath);
            }
        }

        performance.mark('scan-start');
        await scanDirectory(libraryDirectoryHandle, songs);
        performance.mark('scan-complete');
        
        // Flush batched ID3 tag failures to console (after scan completes)
        flushID3FailureLog();
        
        reportScanProgress(songs.length, progressStep, true);
        performance.mark('library-scan-complete');

        // Create performance measures
        performance.measure('directory-selection', 'library-scan-start', 'directory-picker-complete');
        performance.measure('file-scanning', 'scan-start', 'scan-complete');
        performance.measure('total-library-load', 'library-scan-start', 'library-scan-complete');
        
        // Try to measure sub-phases (may not exist if no files processed)
        try {
            performance.measure('file-categorization', 'categorize-start', 'categorize-complete');
            performance.measure('zip-file-processing', 'zip-processing-start', 'zip-processing-complete');
            performance.measure('mp3-cdg-pair-processing', 'pairs-processing-start', 'pairs-processing-complete');
        } catch (e) {
            // Marks may not exist if no files were found
        }

        // Log performance results
        const measures = performance.getEntriesByType('measure')
            .filter(m => m.name.includes('library') || m.name.includes('directory') || 
                        m.name.includes('scanning') || m.name.includes('categorization') || 
                        m.name.includes('processing'));
        
        console.log('📊 Library Scan Performance:');
        measures.forEach(m => {
            console.log(`  ${m.name}: ${Math.round(m.duration)}ms`);
        });
        
        const totalMeasure = measures.find(m => m.name === 'total-library-load');
        const totalDuration = totalMeasure ? totalMeasure.duration : 0;
        
        console.log(`Library scan complete: ${songs.length} songs found in ${(totalDuration / 1000).toFixed(2)}s`);
        console.log(`  Performance: ${(songs.length / (totalDuration / 1000)).toFixed(2)} songs/second`);
        
        return songs;
    } catch (error) {
        console.error('Error picking library directory:', error);
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
        console.warn('SessionStorage fallback failed:', err);
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
        
        console.error('Error loading song files:', error);
        throw error instanceof Error ? error : new Error(String(error));
    }
}
