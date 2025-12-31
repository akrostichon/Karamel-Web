// File System Access API wrapper for loading MP3 and CDG files
// Store file data in module-level variables to avoid JSON serialization issues

import { extractMetadata, validatePattern } from './metadata.js';

let mp3Data = null;
let cdgData = null;
let libraryDirectoryHandle = null; // Keep directory handle for session-long access

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
                const metadata = await extractMetadata(mp3Data.file, fullPath, filenamePatternInner);

                songsAcc.push({
                    id: crypto.randomUUID(),
                    artist: metadata.artist,
                    title: metadata.title,
                    mp3FileName: `${baseName}.mp3`,
                    cdgFileName: `${baseName}.cdg`,
                    path: relativePath,
                    fullPath: fullPath
                });

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
            const metadata = await extractMetadata(mp3Data.file, fullPath, filenamePattern);

            songs.push({
                id: crypto.randomUUID(),
                artist: metadata.artist,
                title: metadata.title,
                mp3FileName: `${baseName}.mp3`,
                cdgFileName: `${baseName}.cdg`,
                path: relativePath,
                fullPath: fullPath
            });
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
export async function loadSongFiles(path, mp3FileName, cdgFileName) {
    try {
        if (!libraryDirectoryHandle) {
            throw new Error('No library directory selected');
        }

        // Navigate to the correct subdirectory
        let currentDir = libraryDirectoryHandle;
        if (path) {
            const pathParts = path.split('/');
            for (const part of pathParts) {
                currentDir = await currentDir.getDirectoryHandle(part);
            }
        }

        // Load MP3 file
        const mp3FileHandle = await currentDir.getFileHandle(mp3FileName);
        const mp3File = await mp3FileHandle.getFile();
        const mp3ArrayBuffer = await mp3File.arrayBuffer();
        const loadedMp3Data = new Uint8Array(mp3ArrayBuffer);

        // Load CDG file
        const cdgFileHandle = await currentDir.getFileHandle(cdgFileName);
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
        throw error;
    }
}
