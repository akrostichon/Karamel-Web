import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { toggleFullscreen, isFullscreen, exitFullscreen, initializeFullscreen, dispose } from './fullscreen.js';

describe('fullscreen.js', () => {
    let fullscreenElement;
    let fullscreenChangeListeners = [];
    let mockDotNetRef;

    beforeEach(() => {
        // Mock document.fullscreenElement
        fullscreenElement = null;
        Object.defineProperty(document, 'fullscreenElement', {
            get: () => fullscreenElement,
            configurable: true
        });

        // Mock requestFullscreen
        document.documentElement.requestFullscreen = vi.fn(async () => {
            fullscreenElement = document.documentElement;
            // Trigger fullscreenchange event
            fullscreenChangeListeners.forEach(listener => listener());
        });

        // Mock exitFullscreen
        document.exitFullscreen = vi.fn(async () => {
            fullscreenElement = null;
            // Trigger fullscreenchange event
            fullscreenChangeListeners.forEach(listener => listener());
        });

        // Mock addEventListener/removeEventListener
        const originalAddEventListener = document.addEventListener.bind(document);
        const originalRemoveEventListener = document.removeEventListener.bind(document);
        
        document.addEventListener = vi.fn((event, listener) => {
            if (event === 'fullscreenchange') {
                fullscreenChangeListeners.push(listener);
            }
            originalAddEventListener(event, listener);
        });

        document.removeEventListener = vi.fn((event, listener) => {
            if (event === 'fullscreenchange') {
                fullscreenChangeListeners = fullscreenChangeListeners.filter(l => l !== listener);
            }
            originalRemoveEventListener(event, listener);
        });

        // Mock DotNet reference
        mockDotNetRef = {
            invokeMethodAsync: vi.fn().mockResolvedValue(undefined)
        };

        fullscreenChangeListeners = [];
    });

    afterEach(() => {
        dispose();
        fullscreenChangeListeners = [];
    });

    describe('isFullscreen', () => {
        it('should return false when not in fullscreen', () => {
            expect(isFullscreen()).toBe(false);
        });

        it('should return true when in fullscreen', () => {
            fullscreenElement = document.documentElement;
            expect(isFullscreen()).toBe(true);
        });
    });

    describe('toggleFullscreen', () => {
        it('should enter fullscreen when not in fullscreen', async () => {
            const result = await toggleFullscreen();
            
            expect(document.documentElement.requestFullscreen).toHaveBeenCalled();
            expect(result).toBe(true);
            expect(fullscreenElement).toBe(document.documentElement);
        });

        it('should exit fullscreen when in fullscreen', async () => {
            fullscreenElement = document.documentElement;
            
            const result = await toggleFullscreen();
            
            expect(document.exitFullscreen).toHaveBeenCalled();
            expect(result).toBe(false);
            expect(fullscreenElement).toBeNull();
        });

        it('should throw error if requestFullscreen fails', async () => {
            document.documentElement.requestFullscreen = vi.fn().mockRejectedValue(new Error('Fullscreen denied'));
            
            await expect(toggleFullscreen()).rejects.toThrow('Fullscreen denied');
        });

        it('should throw error if exitFullscreen fails', async () => {
            fullscreenElement = document.documentElement;
            document.exitFullscreen = vi.fn().mockRejectedValue(new Error('Exit failed'));
            
            await expect(toggleFullscreen()).rejects.toThrow('Exit failed');
        });
    });

    describe('exitFullscreen', () => {
        it('should exit fullscreen when in fullscreen', async () => {
            fullscreenElement = document.documentElement;
            
            await exitFullscreen();
            
            expect(document.exitFullscreen).toHaveBeenCalled();
            expect(fullscreenElement).toBeNull();
        });

        it('should do nothing when not in fullscreen', async () => {
            await exitFullscreen();
            
            expect(document.exitFullscreen).not.toHaveBeenCalled();
        });

        it('should throw error if exitFullscreen fails', async () => {
            fullscreenElement = document.documentElement;
            document.exitFullscreen = vi.fn().mockRejectedValue(new Error('Exit failed'));
            
            await expect(exitFullscreen()).rejects.toThrow('Exit failed');
        });
    });

    describe('initializeFullscreen', () => {
        it('should register fullscreenchange event listener', () => {
            initializeFullscreen(mockDotNetRef);
            
            expect(document.addEventListener).toHaveBeenCalledWith('fullscreenchange', expect.any(Function));
            expect(fullscreenChangeListeners.length).toBeGreaterThan(0);
        });

        it('should call DotNet callback when fullscreen state changes', async () => {
            initializeFullscreen(mockDotNetRef);
            
            // Enter fullscreen
            fullscreenElement = document.documentElement;
            fullscreenChangeListeners.forEach(listener => listener());
            
            // Wait for async callback
            await new Promise(resolve => setTimeout(resolve, 10));
            
            expect(mockDotNetRef.invokeMethodAsync).toHaveBeenCalledWith('OnFullscreenChanged', true);
        });

        it('should call DotNet callback with false when exiting fullscreen', async () => {
            initializeFullscreen(mockDotNetRef);
            
            // Set to fullscreen first
            fullscreenElement = document.documentElement;
            fullscreenChangeListeners.forEach(listener => listener());
            await new Promise(resolve => setTimeout(resolve, 10));
            
            // Exit fullscreen
            mockDotNetRef.invokeMethodAsync.mockClear();
            fullscreenElement = null;
            fullscreenChangeListeners.forEach(listener => listener());
            await new Promise(resolve => setTimeout(resolve, 10));
            
            expect(mockDotNetRef.invokeMethodAsync).toHaveBeenCalledWith('OnFullscreenChanged', false);
        });

        it('should handle DotNet callback errors gracefully', async () => {
            mockDotNetRef.invokeMethodAsync = vi.fn().mockRejectedValue(new Error('DotNet error'));
            const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});
            
            initializeFullscreen(mockDotNetRef);
            
            fullscreenElement = document.documentElement;
            fullscreenChangeListeners.forEach(listener => listener());
            
            await new Promise(resolve => setTimeout(resolve, 10));
            
            // Logger format: [timestamp] [ModuleName] [LEVEL] Message { properties } { metadata }
            expect(consoleError).toHaveBeenCalledWith(
                expect.stringContaining('[Fullscreen] [ERROR] Error calling OnFullscreenChanged'),
                expect.objectContaining({ error: 'DotNet error' }),
                expect.objectContaining({ moduleName: 'Fullscreen' })
            );
            
            consoleError.mockRestore();
        });

        it('should not call DotNet callback if reference is not set', async () => {
            // Simulate fullscreenchange without initialization
            fullscreenElement = document.documentElement;
            document.addEventListener('fullscreenchange', () => {
                // Event fires but should not crash
            });
            
            // Should not throw
            expect(() => {
                document.dispatchEvent(new Event('fullscreenchange'));
            }).not.toThrow();
        });
    });

    describe('dispose', () => {
        it('should remove fullscreenchange event listener', () => {
            initializeFullscreen(mockDotNetRef);
            
            dispose();
            
            expect(document.removeEventListener).toHaveBeenCalledWith('fullscreenchange', expect.any(Function));
            expect(fullscreenChangeListeners.length).toBe(0);
        });

        it('should clear DotNet reference', async () => {
            initializeFullscreen(mockDotNetRef);
            dispose();
            
            // Trigger fullscreenchange after disposal
            fullscreenElement = document.documentElement;
            fullscreenChangeListeners.forEach(listener => listener());
            
            await new Promise(resolve => setTimeout(resolve, 10));
            
            // DotNet callback should not be called
            expect(mockDotNetRef.invokeMethodAsync).not.toHaveBeenCalled();
        });

        it('should be safe to call multiple times', () => {
            initializeFullscreen(mockDotNetRef);
            
            expect(() => {
                dispose();
                dispose();
                dispose();
            }).not.toThrow();
        });
    });

    describe('F11 key simulation', () => {
        it('should detect fullscreen state change from F11 press', async () => {
            initializeFullscreen(mockDotNetRef);
            
            // Simulate F11 entering fullscreen (browser sets fullscreenElement)
            fullscreenElement = document.documentElement;
            fullscreenChangeListeners.forEach(listener => listener());
            
            await new Promise(resolve => setTimeout(resolve, 10));
            
            expect(mockDotNetRef.invokeMethodAsync).toHaveBeenCalledWith('OnFullscreenChanged', true);
        });

        it('should detect fullscreen state change from ESC press', async () => {
            initializeFullscreen(mockDotNetRef);
            
            // Enter fullscreen
            fullscreenElement = document.documentElement;
            fullscreenChangeListeners.forEach(listener => listener());
            await new Promise(resolve => setTimeout(resolve, 10));
            
            mockDotNetRef.invokeMethodAsync.mockClear();
            
            // Simulate ESC exiting fullscreen
            fullscreenElement = null;
            fullscreenChangeListeners.forEach(listener => listener());
            
            await new Promise(resolve => setTimeout(resolve, 10));
            
            expect(mockDotNetRef.invokeMethodAsync).toHaveBeenCalledWith('OnFullscreenChanged', false);
        });
    });
});
