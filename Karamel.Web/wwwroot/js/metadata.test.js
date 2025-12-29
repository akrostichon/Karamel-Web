// Unit tests for metadata extraction module
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { parseFilename, validatePattern } from './metadata.js';

// Note: extractMetadata with ID3 tag reading is tested separately with real File objects
// These tests focus on the filename parsing logic

describe('parseFilename', () => {
    describe('with default pattern "%artist - %title"', () => {
        it('should parse filename with default pattern', () => {
            const result = parseFilename('The Beatles - Hey Jude', '%artist - %title');
            expect(result).toEqual({
                artist: 'The Beatles',
                title: 'Hey Jude'
            });
        });

        it('should parse filename with file extension', () => {
            const result = parseFilename('Elvis Presley - Suspicious Minds.mp3', '%artist - %title');
            expect(result).toEqual({
                artist: 'Elvis Presley',
                title: 'Suspicious Minds'
            });
        });

        it('should parse filename with path', () => {
            const result = parseFilename('Rock/Queen - Bohemian Rhapsody.mp3', '%artist - %title');
            expect(result).toEqual({
                artist: 'Queen',
                title: 'Bohemian Rhapsody'
            });
        });

        it('should trim whitespace from artist and title', () => {
            const result = parseFilename('  ABBA   -   Dancing Queen  ', '%artist - %title');
            expect(result).toEqual({
                artist: 'ABBA',
                title: 'Dancing Queen'
            });
        });

        it('should handle artist names with dashes', () => {
            const result = parseFilename('AC-DC - Thunderstruck', '%artist - %title');
            expect(result).toEqual({
                artist: 'AC-DC',
                title: 'Thunderstruck'
            });
        });

        it('should handle title with dashes by using greedy match for title', () => {
            const result = parseFilename('The Doors - L.A. Woman - Live', '%artist - %title');
            expect(result).toEqual({
                artist: 'The Doors',
                title: 'L.A. Woman - Live'
            });
        });

        it('should handle Windows path separators', () => {
            const result = parseFilename('Rock\\90s\\Nirvana - Smells Like Teen Spirit.mp3', '%artist - %title');
            expect(result).toEqual({
                artist: 'Nirvana',
                title: 'Smells Like Teen Spirit'
            });
        });

        it('should return Unknown Artist/Title if pattern does not match', () => {
            const result = parseFilename('RandomFileName', '%artist - %title');
            expect(result).toEqual({
                artist: 'Unknown Artist',
                title: 'RandomFileName'
            });
        });

        it('should return Unknown Artist/Title for empty filename', () => {
            const result = parseFilename('', '%artist - %title');
            expect(result).toEqual({
                artist: 'Unknown Artist',
                title: 'Unknown Title'
            });
        });
    });

    describe('with custom patterns', () => {
        it('should parse with pattern "%title by %artist"', () => {
            const result = parseFilename('Hey Jude by The Beatles', '%title by %artist');
            expect(result).toEqual({
                artist: 'The Beatles',
                title: 'Hey Jude'
            });
        });

        it('should parse with pattern "%artist_%title"', () => {
            const result = parseFilename('Pink Floyd_Comfortably Numb', '%artist_%title');
            expect(result).toEqual({
                artist: 'Pink Floyd',
                title: 'Comfortably Numb'
            });
        });

        it('should parse with pattern "[%artist] %title"', () => {
            const result = parseFilename('[Led Zeppelin] Stairway to Heaven', '[%artist] %title');
            expect(result).toEqual({
                artist: 'Led Zeppelin',
                title: 'Stairway to Heaven'
            });
        });

        it('should parse with pattern "%artist - [%title]"', () => {
            const result = parseFilename('David Bowie - [Space Oddity]', '%artist - [%title]');
            expect(result).toEqual({
                artist: 'David Bowie',
                title: 'Space Oddity'
            });
        });

        it('should handle special regex characters in pattern', () => {
            const result = parseFilename('Artist (Year) - Title.mp3', '%artist (Year) - %title');
            expect(result).toEqual({
                artist: 'Artist',
                title: 'Title'
            });
        });

        it('should be case-insensitive in matching', () => {
            const result = parseFilename('ARTIST - TITLE', '%artist - %title');
            expect(result).toEqual({
                artist: 'ARTIST',
                title: 'TITLE'
            });
        });
    });

    describe('edge cases', () => {
        it('should handle filenames with multiple extensions', () => {
            const result = parseFilename('Song.backup.mp3', '%artist - %title');
            expect(result).toEqual({
                artist: 'Unknown Artist',
                title: 'Song.backup'
            });
        });

        it('should handle very long file paths', () => {
            const longPath = 'folder1/folder2/folder3/folder4/Artist - Title.mp3';
            const result = parseFilename(longPath, '%artist - %title');
            expect(result).toEqual({
                artist: 'Artist',
                title: 'Title'
            });
        });

        it('should handle Unicode characters', () => {
            const result = parseFilename('Café del Mar - Señorita', '%artist - %title');
            expect(result).toEqual({
                artist: 'Café del Mar',
                title: 'Señorita'
            });
        });

        it('should handle numbers in artist and title', () => {
            const result = parseFilename('Blink-182 - All The Small Things', '%artist - %title');
            expect(result).toEqual({
                artist: 'Blink-182',
                title: 'All The Small Things'
            });
        });
    });
});

describe('validatePattern', () => {
    it('should return valid pattern unchanged', () => {
        expect(validatePattern('%artist - %title')).toBe('%artist - %title');
    });

    it('should return valid custom pattern unchanged', () => {
        expect(validatePattern('%title by %artist')).toBe('%title by %artist');
    });

    it('should trim whitespace from pattern', () => {
        expect(validatePattern('  %artist - %title  ')).toBe('%artist - %title');
    });

    it('should return default pattern if pattern is null', () => {
        expect(validatePattern(null)).toBe('%artist - %title');
    });

    it('should return default pattern if pattern is undefined', () => {
        expect(validatePattern(undefined)).toBe('%artist - %title');
    });

    it('should return default pattern if pattern is empty string', () => {
        expect(validatePattern('')).toBe('%artist - %title');
    });

    it('should return default pattern if pattern is not a string', () => {
        expect(validatePattern(123)).toBe('%artist - %title');
        expect(validatePattern({})).toBe('%artist - %title');
        expect(validatePattern([])).toBe('%artist - %title');
    });

    it('should return default pattern if missing %artist', () => {
        expect(validatePattern('Just %title')).toBe('%artist - %title');
    });

    it('should return default pattern if missing %title', () => {
        expect(validatePattern('Just %artist')).toBe('%artist - %title');
    });

    it('should return default pattern if missing both placeholders', () => {
        expect(validatePattern('No placeholders here')).toBe('%artist - %title');
    });

    it('should accept pattern with both placeholders in any order', () => {
        expect(validatePattern('%title - %artist')).toBe('%title - %artist');
    });
});
