import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';

// Mock fileAccess.js before importing the module under test
vi.mock('./fileAccess.js', () => ({
    pickLibraryDirectory: vi.fn()
}));

// Mock logger.js
vi.mock('./logger.js', () => ({
    createLogger: vi.fn(() => ({
        info: vi.fn(),
        debug: vi.fn(),
        warn: vi.fn(),
        error: vi.fn()
    }))
}));

import { scanDirectory, triggerDownload } from './exportBridge.js';
import { pickLibraryDirectory } from './fileAccess.js';

describe('scanDirectory', () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    it('returns songs from pickLibraryDirectory', async () => {
        const fakeSongs = [
            { id: '1', artist: 'Queen', title: 'Bohemian Rhapsody' },
            { id: '2', artist: 'Abba', title: 'Dancing Queen' }
        ];
        pickLibraryDirectory.mockResolvedValue(fakeSongs);

        const result = await scanDirectory('%artist - %title');

        expect(result).toEqual(fakeSongs);
        expect(pickLibraryDirectory).toHaveBeenCalledWith('%artist - %title');
    });

    it('propagates error when user cancels directory picker', async () => {
        const cancelError = new DOMException('The user aborted a request.', 'AbortError');
        pickLibraryDirectory.mockRejectedValue(cancelError);

        await expect(scanDirectory('%artist - %title')).rejects.toThrow('The user aborted a request.');
    });
});

describe('triggerDownload', () => {
    let createObjectURLSpy;
    let revokeObjectURLSpy;
    let appendChildSpy;
    let removeChildSpy;
    let createdAnchor;

    beforeEach(() => {
        vi.clearAllMocks();

        createObjectURLSpy = vi.fn(() => 'blob:mock-url');
        revokeObjectURLSpy = vi.fn();
        global.URL.createObjectURL = createObjectURLSpy;
        global.URL.revokeObjectURL = revokeObjectURLSpy;

        // Capture the created anchor element
        const originalCreateElement = document.createElement.bind(document);
        vi.spyOn(document, 'createElement').mockImplementation((tag) => {
            const el = originalCreateElement(tag);
            if (tag === 'a') {
                createdAnchor = el;
                vi.spyOn(el, 'click').mockImplementation(() => {});
            }
            return el;
        });
    });

    afterEach(() => {
        vi.restoreAllMocks();
    });

    it('sets correct href and download attributes on the anchor', () => {
        triggerDownload('Artist;Title\nQueen;Bohemian Rhapsody\n', 'artists.csv');

        expect(createdAnchor.href).toContain('blob:mock-url');
        expect(createdAnchor.download).toBe('artists.csv');
    });

    it('calls URL.createObjectURL with a Blob', () => {
        triggerDownload('some,content', 'titles.csv');

        expect(createObjectURLSpy).toHaveBeenCalledOnce();
        const blobArg = createObjectURLSpy.mock.calls[0][0];
        expect(blobArg).toBeInstanceOf(Blob);
    });

    it('calls URL.revokeObjectURL after triggering download', () => {
        triggerDownload('content', 'duplicates.csv');

        expect(revokeObjectURLSpy).toHaveBeenCalledWith('blob:mock-url');
    });

    it('calls click() on the anchor element', () => {
        triggerDownload('content', 'artists.csv');

        expect(createdAnchor.click).toHaveBeenCalledOnce();
    });
});
