// Song metadata extraction module
// Uses jsmediatags for ID3 tag extraction with filename fallback

// Import jsmediatags - use npm package in tests, browser UMD build in browser
let jsmediatags;
let id3Enabled = false;

try {
    // Try npm package first (for tests with Node.js environment)
    const module = await import('jsmediatags');
    jsmediatags = module.default || module;
    id3Enabled = true;
    console.log('Using jsmediatags from npm package');
} catch {
    // In browser, load UMD build from CDN
    try {
        // Check if already loaded via script tag
        if (window.jsmediatags) {
            jsmediatags = window.jsmediatags;
            id3Enabled = true;
            console.log('Using jsmediatags from window (pre-loaded)');
        } else {
            // Dynamically load the browser UMD build
            await loadJsMediaTagsFromCDN();
            jsmediatags = window.jsmediatags;
            id3Enabled = true;
            console.log('Using jsmediatags from CDN');
        }
    } catch (error) {
        console.warn('Failed to load jsmediatags, using filename parsing only:', error);
        id3Enabled = false;
    }
}

/**
 * Load jsmediatags browser UMD build from CDN
 * @returns {Promise<void>}
 */
function loadJsMediaTagsFromCDN() {
    return new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = 'https://cdn.jsdelivr.net/npm/jsmediatags@3.9.5/dist/jsmediatags.min.js';
        script.onload = () => resolve();
        script.onerror = () => reject(new Error('Failed to load jsmediatags from CDN'));
        document.head.appendChild(script);
    });
}

/**
 * Extract song metadata from an MP3 file
 * Tries ID3 tags first (if available), falls back to filename parsing
 * @param {File} file - The MP3 file to extract metadata from
 * @param {string} relativePath - Relative path from library root (for fallback)
 * @param {string} filenamePattern - Pattern for parsing filename (default: "%artist - %title")
 * @returns {Promise<{artist: string, title: string}>} Artist and title
 */
export async function extractMetadata(file, relativePath, filenamePattern = '%artist - %title') {
    // Try ID3 tags first if available
    if (id3Enabled) {
        try {
            const id3Data = await readID3Tags(file);
            if (id3Data && id3Data.artist && id3Data.title) {
                return {
                    artist: id3Data.artist.trim(),
                    title: id3Data.title.trim()
                };
            }
        } catch (error) {
            console.warn('ID3 tag extraction failed, falling back to filename parsing:', error);
        }
    }

    // Fallback to filename parsing
    return parseFilename(relativePath, filenamePattern);
}

/**
 * Read ID3 tags from an MP3 file using jsmediatags
 * @param {File} file - The MP3 file to read
 * @returns {Promise<{artist: string, title: string}|null>}
 */
function readID3Tags(file) {
    if (!id3Enabled || !jsmediatags) {
        return Promise.resolve(null);
    }
    
    return new Promise((resolve, reject) => {
        jsmediatags.read(file, {
            onSuccess: (tag) => {
                const tags = tag.tags || {};
                resolve({
                    artist: tags.artist || null,
                    title: tags.title || null,
                    album: tags.album || null,
                    year: tags.year || null
                });
            },
            onError: (error) => {
                reject(error);
            }
        });
    });
}

/**
 * Parse filename according to configurable pattern
 * Default pattern: "%artist - %title"
 * @param {string} filename - Filename without extension (or full relative path)
 * @param {string} pattern - Pattern with %artist and %title placeholders
 * @returns {{artist: string, title: string}}
 */
export function parseFilename(filename, pattern = '%artist - %title') {
    // Remove file extension if present (e.g., .mp3, .MP3)
    // Only matches extensions with letters/digits, not dots in the middle
    const basename = filename.replace(/\.\w+$/, '');
    
    // Extract just the filename if it's a path
    const nameOnly = basename.split('/').pop().split('\\').pop();

    // Convert pattern to regex
    // Escape special regex characters except %
    const escapedPattern = pattern
        .replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
        .replace(/%artist/g, '(?<artist>.+?)')
        .replace(/%title/g, '(?<title>.+)');

    const regex = new RegExp(`^${escapedPattern}$`, 'i');
    const match = nameOnly.match(regex);

    if (match && match.groups) {
        return {
            artist: match.groups.artist?.trim() || 'Unknown Artist',
            title: match.groups.title?.trim() || 'Unknown Title'
        };
    }

    // If pattern doesn't match, return filename as title
    return {
        artist: 'Unknown Artist',
        title: nameOnly || 'Unknown Title'
    };
}

/**
 * Validate and normalize filename pattern
 * @param {string} pattern - User-provided pattern
 * @returns {string} Valid pattern or default
 */
export function validatePattern(pattern) {
    if (!pattern || typeof pattern !== 'string') {
        return '%artist - %title';
    }

    // Pattern must contain both %artist and %title
    const hasArtist = pattern.includes('%artist');
    const hasTitle = pattern.includes('%title');

    if (!hasArtist || !hasTitle) {
        console.warn('Invalid pattern: must contain both %artist and %title. Using default.');
        return '%artist - %title';
    }

    return pattern.trim();
}
