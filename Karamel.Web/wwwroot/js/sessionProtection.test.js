import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { setMainTabProtection, removeMainTabProtection } from './sessionProtection.js';

describe('sessionProtection', () => {
    let addEventListenerSpy;
    let removeEventListenerSpy;
    let originalTitle;

    beforeEach(() => {
        // Reset document title
        originalTitle = document.title;
        document.title = 'Test Page - Karamel Karaoke';

        // Spy on addEventListener and removeEventListener
        addEventListenerSpy = vi.spyOn(window, 'addEventListener');
        removeEventListenerSpy = vi.spyOn(window, 'removeEventListener');
    });

    afterEach(() => {
        // Clean up
        removeMainTabProtection();
        document.title = originalTitle;
        vi.restoreAllMocks();
    });

    describe('setMainTabProtection', () => {
        it('should add beforeunload event listener', () => {
            const message = '⚠️ Closing this tab will end the session!';

            setMainTabProtection(message);

            expect(addEventListenerSpy).toHaveBeenCalledWith('beforeunload', expect.any(Function));
        });

        it('should prepend [MAIN] to document title', () => {
            const message = '⚠️ Test warning';
            const originalTitle = document.title;

            setMainTabProtection(message);

            expect(document.title).toBe('🎤 [MAIN] ' + originalTitle);
        });

        it('should not duplicate [MAIN] prefix if called multiple times', () => {
            const message = '⚠️ Test warning';

            setMainTabProtection(message);
            setMainTabProtection(message);

            const mainCount = (document.title.match(/🎤 \[MAIN\]/g) || []).length;
            expect(mainCount).toBe(1);
        });

        it('should prevent default on beforeunload event', () => {
            const message = '⚠️ Test warning';
            setMainTabProtection(message);

            // Get the registered handler
            const call = addEventListenerSpy.mock.calls.find(call => call[0] === 'beforeunload');
            expect(call).toBeDefined();
            const handler = call[1];

            // Create mock event
            const mockEvent = {
                preventDefault: vi.fn(),
                returnValue: undefined
            };

            // Call handler
            const result = handler(mockEvent);

            // Verify preventDefault was called and returnValue was set
            expect(mockEvent.preventDefault).toHaveBeenCalled();
            expect(mockEvent.returnValue).toBe(message);
            expect(result).toBe(message);
        });
    });

    describe('removeMainTabProtection', () => {
        it('should remove beforeunload event listener', () => {
            const message = '⚠️ Test warning';
            setMainTabProtection(message);

            removeMainTabProtection();

            expect(removeEventListenerSpy).toHaveBeenCalledWith('beforeunload', expect.any(Function));
        });

        it('should restore original document title', () => {
            const originalTitle = document.title;
            const message = '⚠️ Test warning';

            setMainTabProtection(message);
            expect(document.title).toContain('[MAIN]');

            removeMainTabProtection();
            expect(document.title).toBe(originalTitle);
        });

        it('should handle multiple removal calls safely', () => {
            const message = '⚠️ Test warning';
            setMainTabProtection(message);

            removeMainTabProtection();
            removeMainTabProtection(); // Should not throw

            expect(removeEventListenerSpy).toHaveBeenCalled();
        });
    });

    describe('edge cases', () => {
        it('should handle empty message', () => {
            setMainTabProtection('');

            expect(addEventListenerSpy).toHaveBeenCalled();
            expect(document.title).toContain('[MAIN]');
        });

        it('should handle very long message', () => {
            const longMessage = 'A'.repeat(1000);

            setMainTabProtection(longMessage);

            const call = addEventListenerSpy.mock.calls.find(call => call[0] === 'beforeunload');
            const handler = call[1];
            const mockEvent = { preventDefault: vi.fn(), returnValue: undefined };

            const result = handler(mockEvent);
            expect(result).toBe(longMessage);
        });
    });
});
