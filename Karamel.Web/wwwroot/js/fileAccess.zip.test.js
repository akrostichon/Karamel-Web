import { describe, it, expect, beforeEach, vi } from 'vitest';
import JSZip from 'jszip';

// Reuse same mocking approach as fileAccess.test.js but focused on ZIP files
vi.mock('../js/metadata.js', () => ({
  extractMetadata: vi.fn(async (file, relativePath, pattern) => {
    // If file is a Blob or ArrayBuffer, just fallback to filename parsing using relativePath
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

class MockFileSystemFileHandle {
  constructor(name, contentBuffer) {
    this.kind = 'file';
    this.name = name;
    this._content = contentBuffer; // Uint8Array or ArrayBuffer
  }

  async getFile() {
    const buf = this._content instanceof Uint8Array ? this._content.buffer : this._content;
    return {
      name: this.name,
      async arrayBuffer() {
        return buf;
      }
    };
  }
}

class MockFileSystemDirectoryHandle {
  constructor(name, entries = {}) {
    this.kind = 'directory';
    this.name = name;
    this._entries = entries;
  }

  async *values() {
    for (const entry of Object.values(this._entries)) {
      yield entry;
    }
  }

  async getFileHandle(name) {
    const entry = this._entries[name];
    if (!entry || entry.kind !== 'file') throw new Error(`File not found: ${name}`);
    return entry;
  }

  async getDirectoryHandle(name) {
    const entry = this._entries[name];
    if (!entry || entry.kind !== 'directory') throw new Error(`Directory not found: ${name}`);
    return entry;
  }
}

describe('fileAccess.js - ZIP support', () => {
  let fileAccessModule;
  let mockDirectoryPicker;

  beforeEach(async () => {
    vi.resetModules();
    mockDirectoryPicker = vi.fn();
    global.window = { showDirectoryPicker: mockDirectoryPicker };
    vi.spyOn(global.crypto, 'randomUUID').mockReturnValue('zip-id-1');
    fileAccessModule = await import('../js/fileAccess.js');
  });

  it('should detect zip with single mp3+cdg at root and add a song', async () => {
    // Create an in-memory ZIP with test.mp3 and test.cdg at root
    const zip = new JSZip();
    zip.file('Zip Artist - zipsong.mp3', new TextEncoder().encode('mp3 bytes'));
    zip.file('Zip Artist - zipsong.cdg', new TextEncoder().encode('cdg bytes'));
    const zipBuf = await zip.generateAsync({ type: 'arraybuffer' });

    const mockDirectory = new MockFileSystemDirectoryHandle('library', {
      'song.zip': new MockFileSystemFileHandle('song.zip', zipBuf)
    });

    mockDirectoryPicker.mockResolvedValue(mockDirectory);

    const songs = await fileAccessModule.pickLibraryDirectory();
    expect(songs).toBeDefined();
    expect(songs.length).toBe(1);
    const s = songs[0];
    expect(s).toHaveProperty('sourceType', 'zip');
    expect(s).toHaveProperty('zipFileName', 'song.zip');
    expect(s).toHaveProperty('mp3FileName', 'Zip Artist - zipsong.mp3');
    expect(s).toHaveProperty('cdgFileName', 'Zip Artist - zipsong.cdg');
  });

  it('should lazily extract mp3 blob and cdg arraybuffer when loadSongFiles called for zip', async () => {
    const zip = new JSZip();
    zip.file('Z - s.mp3', new TextEncoder().encode('mp3 bytes'));
    zip.file('Z - s.cdg', new TextEncoder().encode('cdg bytes'));
    const zipBuf = await zip.generateAsync({ type: 'arraybuffer' });

    const mockDirectory = new MockFileSystemDirectoryHandle('library', {
      'my.zip': new MockFileSystemFileHandle('my.zip', zipBuf)
    });

    mockDirectoryPicker.mockResolvedValue(mockDirectory);

    const songs = await fileAccessModule.pickLibraryDirectory();
    expect(songs).toHaveLength(1);
    const song = songs[0];

    const result = await fileAccessModule.loadSongFiles('', song.mp3FileName, song.cdgFileName, {
      zipFileName: song.zipFileName,
      zipEntryMp3Path: song.zipEntryMp3Path,
      zipEntryCdgPath: song.zipEntryCdgPath
    });

    expect(result).toHaveProperty('mp3Blob');
    expect(result).toHaveProperty('mp3Url');
    expect(result).toHaveProperty('cdgData');
    expect(result.cdgData).toBeInstanceOf(Uint8Array);
  });
});
