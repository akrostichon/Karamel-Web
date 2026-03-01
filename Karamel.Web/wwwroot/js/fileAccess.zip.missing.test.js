import { describe, it, expect, beforeEach, vi } from 'vitest';
import JSZip from 'jszip';

vi.mock('../js/metadata.js', () => ({
  extractMetadata: vi.fn(async (file, relativePath, pattern) => ({ artist: 'X', title: relativePath.replace(/\.[^/.]+$/, '') })),
  validatePattern: vi.fn((pattern) => pattern || '%artist - %title'),
  clearID3FailureLog: vi.fn(),
  flushID3FailureLog: vi.fn()
}));

class MockFileSystemFileHandle { constructor(name, contentBuffer) { this.kind = 'file'; this.name = name; this._content = contentBuffer; } async getFile() { const buf = this._content instanceof Uint8Array ? this._content.buffer : this._content; return { name: this.name, async arrayBuffer() { return buf; } }; } }
class MockFileSystemDirectoryHandle { constructor(name, entries = {}) { this.kind = 'directory'; this.name = name; this._entries = entries; } async *values() { for (const entry of Object.values(this._entries)) yield entry; } async getFileHandle(name) { const entry = this._entries[name]; if (!entry || entry.kind !== 'file') throw new Error(`File not found: ${name}`); return entry; } async getDirectoryHandle(name) { const entry = this._entries[name]; if (!entry || entry.kind !== 'directory') throw new Error(`Directory not found: ${name}`); return entry; } }

describe('fileAccess.js - ZIP missing files', () => {
  let fileAccessModule;
  let mockDirectoryPicker;

  beforeEach(async () => {
    vi.resetModules();
    mockDirectoryPicker = vi.fn();
    global.window = { showDirectoryPicker: mockDirectoryPicker };
    vi.spyOn(global.crypto, 'randomUUID').mockReturnValue('zip-id-missing');
    // Prevent extractDuration from hanging (happy-dom doesn't load media)
    global.URL.createObjectURL = vi.fn(() => { throw new Error('Not supported in tests'); });
    global.URL.revokeObjectURL = vi.fn();
    fileAccessModule = await import('../js/fileAccess.js');
  });

  it('zip without cdg should not produce a song', async () => {
    const zip = new JSZip();
    zip.file('Only.mp3', new TextEncoder().encode('mp3'));
    const zipBuf = await zip.generateAsync({ type: 'arraybuffer' });

    const mockDirectory = new MockFileSystemDirectoryHandle('library', {
      'nodgc.zip': new MockFileSystemFileHandle('nodgc.zip', zipBuf)
    });

    mockDirectoryPicker.mockResolvedValue(mockDirectory);

    const songs = await fileAccessModule.pickLibraryDirectory();
    expect(songs).toHaveLength(0);
  });
});
