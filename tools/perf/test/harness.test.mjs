import assert from 'node:assert/strict';
import test from 'node:test';
import { createSyntheticLibrary } from '../lib/data.mjs';
import { summarize } from '../lib/stats.mjs';

test('summarize uses nearest-rank percentiles', () => {
  const result = summarize(Array.from({ length: 100 }, (_, index) => index + 1));

  assert.deepEqual(result, {
    n: 100,
    min: 1,
    p50: 50,
    p95: 95,
    max: 100,
    mean: 50.5
  });
});

test('summarize handles empty input and rejects invalid values', () => {
  assert.deepEqual(summarize([]), {
    n: 0,
    min: null,
    p50: null,
    p95: null,
    max: null,
    mean: null
  });
  assert.throws(() => summarize([1, Number.NaN]), TypeError);
});

test('synthetic song content is deterministic and includes video metadata', () => {
  const first = createSyntheticLibrary(24);
  const second = createSyntheticLibrary(24);

  assert.deepEqual(
    first.map(({ artist, title, metadataJson }) => ({ artist, title, metadataJson })),
    second.map(({ artist, title, metadataJson }) => ({ artist, title, metadataJson }))
  );
  assert.notEqual(first[0].id, second[0].id);
  assert.equal(JSON.parse(first[0].metadataJson).mediaType, 'video');
  assert.match(first[1].artist + first[1].title, /[äöüÄÖÜßé]/i);
});

test('default synthetic library has 1400 artists and about 17 percent videos', () => {
  const songs = createSyntheticLibrary();
  const videoCount = songs.filter(song => JSON.parse(song.metadataJson).mediaType === 'video').length;

  assert.equal(songs.length, 3700);
  assert.equal(new Set(songs.map(song => song.artist)).size, 1400);
  assert.ok(Math.abs(videoCount / songs.length - 0.17) < 0.01);
});
