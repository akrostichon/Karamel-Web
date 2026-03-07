import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import * as player from './player.js';

describe('player.js - Dual-mode architecture', () => {
    let mockVideoElement;
    let mockDotNetRef;
    let videoEventListeners = {};

    beforeEach(() => {
        const classNames = new Set();

        // Mock video element
        mockVideoElement = {
            src: '',
            classList: {
                add: vi.fn((className) => classNames.add(className)),
                remove: vi.fn((className) => classNames.delete(className)),
                contains: vi.fn((className) => classNames.has(className))
            },
            play: vi.fn().mockResolvedValue(undefined),
            pause: vi.fn(),
            load: vi.fn(),
            currentTime: 0,
            addEventListener: vi.fn((event, handler) => {
                videoEventListeners[event] = handler;
            }),
            removeEventListener: vi.fn()
        };

        // Mock document.getElementById to return video element
        global.document.getElementById = vi.fn((id) => {
            if (id === 'videoPlayer') {
                return mockVideoElement;
            }
            return null;
        });

        // Mock DotNet reference
        mockDotNetRef = {
            invokeMethodAsync: vi.fn().mockResolvedValue(undefined)
        };

        videoEventListeners = {};
    });

    afterEach(() => {
        vi.clearAllMocks();
    });

    describe('initializeVideoPlayer', () => {
        it('should create video player and set playerMode to video', async () => {
            const videoUrl = 'blob:http://localhost/test-video';

            await player.initializeVideoPlayer(videoUrl, mockDotNetRef);

            expect(mockVideoElement.src).toBe(videoUrl);
            expect(mockVideoElement.classList.add).toHaveBeenCalledWith('is-visible');
            expect(mockVideoElement.classList.contains('is-visible')).toBe(true);
            expect(mockVideoElement.load).toHaveBeenCalled();
            expect(mockVideoElement.addEventListener).toHaveBeenCalledWith('ended', expect.any(Function));
            expect(mockVideoElement.addEventListener).toHaveBeenCalledWith('error', expect.any(Function));
        });

        it('should invoke OnSongEnded when video ends', async () => {
            const videoUrl = 'blob:http://localhost/test-video';

            await player.initializeVideoPlayer(videoUrl, mockDotNetRef);

            // Simulate video ended event
            videoEventListeners['ended']();

            expect(mockDotNetRef.invokeMethodAsync).toHaveBeenCalledWith('OnSongEnded');
        });

        it('should handle error event', async () => {
            const videoUrl = 'blob:http://localhost/test-video';

            await player.initializeVideoPlayer(videoUrl, mockDotNetRef);

            expect(mockVideoElement.addEventListener).toHaveBeenCalledWith('error', expect.any(Function));
        });
    });

    describe('pausePlayback in video mode', () => {
        it('should pause video when in video mode', async () => {
            const videoUrl = 'blob:http://localhost/test-video';

            await player.initializeVideoPlayer(videoUrl, mockDotNetRef);
            player.pausePlayback();

            expect(mockVideoElement.pause).toHaveBeenCalled();
        });
    });

    describe('resumePlayback in video mode', () => {
        it('should resume video when in video mode', async () => {
            const videoUrl = 'blob:http://localhost/test-video';

            await player.initializeVideoPlayer(videoUrl, mockDotNetRef);
            player.resumePlayback();

            expect(mockVideoElement.play).toHaveBeenCalled();
        });
    });

    describe('stopPlayback in video mode', () => {
        it('should stop video and reset currentTime when in video mode', async () => {
            const videoUrl = 'blob:http://localhost/test-video';

            await player.initializeVideoPlayer(videoUrl, mockDotNetRef);
            player.stopPlayback();

            expect(mockVideoElement.pause).toHaveBeenCalled();
            expect(mockVideoElement.currentTime).toBe(0);
        });

        it('should hide video player when disposed', async () => {
            const videoUrl = 'blob:http://localhost/test-video';

            await player.initializeVideoPlayer(videoUrl, mockDotNetRef);
            player.dispose();

            expect(mockVideoElement.classList.remove).toHaveBeenCalledWith('is-visible');
            expect(mockVideoElement.classList.contains('is-visible')).toBe(false);
        });
    });

    describe('CDG mode unchanged', () => {
        it('should not affect existing CDG player functionality', () => {
            // This test verifies that existing CDG functions still work
            // We're not testing the full CDG implementation, just that it's not broken
            expect(player.initializePlayer).toBeDefined();
            expect(player.initializePlayerWithCallback).toBeDefined();
            expect(player.pausePlayback).toBeDefined();
            expect(player.resumePlayback).toBeDefined();
            expect(player.stopPlayback).toBeDefined();
        });
    });

    describe('getPlaybackPosition', () => {
        it('should return 0 when no player is active', () => {
            // After dispose, playerMode is null so should return 0
            player.dispose();
            expect(player.getPlaybackPosition()).toBe(0);
        });

        it('should return videoElement.currentTime when in video mode', async () => {
            const videoUrl = 'blob:http://localhost/test-video';
            mockVideoElement.currentTime = 42.5;

            await player.initializeVideoPlayer(videoUrl, mockDotNetRef);
            const position = player.getPlaybackPosition();

            expect(position).toBe(42.5);
        });

        it('should return 0 when in video mode but currentTime is 0', async () => {
            const videoUrl = 'blob:http://localhost/test-video';
            mockVideoElement.currentTime = 0;

            await player.initializeVideoPlayer(videoUrl, mockDotNetRef);
            const position = player.getPlaybackPosition();

            expect(position).toBe(0);
        });

        it('should be exported as a function', () => {
            expect(player.getPlaybackPosition).toBeDefined();
            expect(typeof player.getPlaybackPosition).toBe('function');
        });
    });
});
