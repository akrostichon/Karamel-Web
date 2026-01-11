import { describe, it, expect, beforeEach, vi } from 'vitest';
import JSZip from 'jszip';

vi.mock('../js/metadata.js', () => ({
  extractMetadata: vi.fn(async (file, relativePath, pattern) => {
    const basename = relativePath.replace(/\.[^/.]+$/, '');
    const nameOnly = basename.split('/').pop().split('\\').pop();
    if (nameOnly.includes(' - ')) {
      const [artist, title] = nameOnly.split(' - ');
      return { artist: artist.trim(), title: title.trim() };
    }
    return { artist: 'Unknown Artist', title: nameOnly || 'Unknown Title' };
  }),
  validatePattern: vi.fn((pattern) => pattern || '%artist - %title')
}));

class MockFileSystemFileHandle { constructor(name, contentBuffer) { this.kind = 'file'; this.name = name; this._content = contentBuffer; } async getFile() { const buf = this._content instanceof Uint8Array ? this._content.buffer : this._content; return { name: this.name, async arrayBuffer() { return buf; } }; } }
class MockFileSystemDirectoryHandle { constructor(name, entries = {}) { this.kind = 'directory'; this.name = name; this._entries = entries; } async *values() { for (const entry of Object.values(this._entries)) yield entry; } async getFileHandle(name) { const entry = this._entries[name]; if (!entry || entry.kind !== 'file') throw new Error(`File not found: ${name}`); return entry; } async getDirectoryHandle(name) { const entry = this._entries[name]; if (!entry || entry.kind !== 'directory') throw new Error(`Directory not found: ${name}`); return entry; } }

describe('fileAccess.js - ZIP multiple pairs', () => {
  let fileAccessModule;
  let mockDirectoryPicker;

  beforeEach(async () => {
    vi.resetModules();
    mockDirectoryPicker = vi.fn();
    global.window = { showDirectoryPicker: mockDirectoryPicker };
    vi.spyOn(global.crypto, 'randomUUID').mockReturnValue('zip-id-multi');
    fileAccessModule = await import('../js/fileAccess.js');
  });

  it('with multiple mp3+cdg pairs picks the first root pair only', async () => {
    const zip = new JSZip();
    zip.file('A - one.mp3', new TextEncoder().encode('mp3'));
    zip.file('A - one.cdg', new TextEncoder().encode('cdg'));
    zip.file('B - two.mp3', new TextEncoder().encode('mp3'));
    zip.file('B - two.cdg', new TextEncoder().encode('cdg'));

    const zipBuf = await zip.generateAsync({ type: 'arraybuffer' });

    const mockDirectory = new MockFileSystemDirectoryHandle('library', {
      'multi.zip': new MockFileSystemFileHandle('multi.zip', zipBuf)
    });

    mockDirectoryPicker.mockResolvedValue(mockDirectory);

    const songs = await fileAccessModule.pickLibraryDirectory();
    expect(songs).toHaveLength(1);
    const s = songs[0];
    expect(s).toHaveProperty('mp3FileName');
    // Should pick the first detected mp3 (A - one.mp3)
    expect(s.mp3FileName.toLowerCase()).toContain('a - one.mp3');
  });
});
