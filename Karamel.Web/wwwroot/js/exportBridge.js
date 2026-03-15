import { createLogger } from './logger.js';
import { pickLibraryDirectory } from './fileAccess.js';

const logger = createLogger('ExportBridge');

/**
 * Scan a directory using the File System Access API.
 * @param {string} filenamePattern - e.g. '%artist - %title'
 * @returns {Promise<Array>} Array of song DTOs
 */
export async function scanDirectory(filenamePattern) {
    logger.info('Starting directory scan for export');
    const songs = await pickLibraryDirectory(filenamePattern);
    logger.info('Scan complete', { count: songs.length });
    return songs;
}

/**
 * Trigger a browser download for a CSV string.
 * @param {string} content - Full UTF-8 CSV content
 * @param {string} filename - e.g. 'artists.csv'
 */
export function triggerDownload(content, filename) {
    logger.info('Triggering CSV download', { filename });
    const blob = new Blob([content], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
}
