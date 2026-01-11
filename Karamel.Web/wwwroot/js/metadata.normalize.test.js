import { describe, it, expect, beforeEach, vi } from 'vitest';

describe('metadata normalization and parsing', () => {
  it('parseFilename handles default pattern', async () => {
    // Dynamically import the helpers after resetting modules
    vi.resetModules();
    const mod = await import('./metadata.js');
    const { parseFilename } = mod;
    const res = parseFilename('Artist Name - Song Title.mp3');
    expect(res.artist).toBe('Artist Name');
    expect(res.title).toBe('Song Title');
  });

  it('validatePattern returns default for invalid pattern', async () => {
    vi.resetModules();
    const mod = await import('./metadata.js');
    const { validatePattern } = mod;
    const p = validatePattern('%artist only');
    expect(p).toBe('%artist - %title');
  });

  it('normalizeToBlob accepts ArrayBuffer and Uint8Array via extractMetadata', async () => {
    // Mock jsmediatags to force read ID3 from blob path
    const fake = {
      read(target, handlers) {
        // Emulate success with tags
        handlers.onSuccess({ tags: { artist: 'T', title: 'S' } });
      }
    };

    vi.resetModules();
    vi.doMock('jsmediatags', () => ({ default: fake }), { virtual: false });
    const mod = await import('./metadata.js');

    // ArrayBuffer case
    const arr = (new TextEncoder()).encode('mp3bytes').buffer;
    const r1 = await mod.extractMetadata(arr, 'arr.mp3');
    expect(r1.artist).toBe('T');
    expect(r1.title).toBe('S');

    // Uint8Array case
    const u8 = new TextEncoder().encode('mp3bytes');
    const r2 = await mod.extractMetadata(u8, 'u8.mp3');
    expect(r2.artist).toBe('T');

    // File-like wrapper (object with file property)
    const wrapper = { file: new Blob([u8.buffer], { type: 'audio/mpeg' }), name: 'w.mp3' };
    const r3 = await mod.extractMetadata(wrapper, 'w.mp3');
    expect(r3.title).toBe('S');
  });

  it('extractMetadata falls back to filename when id3 fails or not available', async () => {
    // Mock jsmediatags that errors
    const fakeErr = {
      read(target, handlers) {
        handlers.onError(new Error('no id3'));
      }
    };

    vi.resetModules();
    vi.doMock('jsmediatags', () => ({ default: fakeErr }), { virtual: false });
    const mod = await import('./metadata.js');

    const res = await mod.extractMetadata(null, 'Fallback Artist - FTitle.mp3');
    expect(res.artist).toBe('Fallback Artist');
    expect(res.title).toBe('FTitle');
  });
});
