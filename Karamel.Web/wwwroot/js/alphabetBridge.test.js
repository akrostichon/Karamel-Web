import { describe, it, expect, beforeEach, vi } from 'vitest';

// vi.hoisted ensures the mock object is created before vi.mock() factory executes
const { mockLogger } = vi.hoisted(() => ({
    mockLogger: {
        debug: vi.fn(),
        info: vi.fn(),
        warn: vi.fn(),
        error: vi.fn(),
    },
}));

vi.mock('./logger.js', () => ({
    createLogger: vi.fn(() => mockLogger),
}));

import { scrollToArtistSection, observeArtistSections, disconnectArtistSectionObserver } from './alphabetBridge.js';

describe('alphabetBridge.js', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        // Remove any leftover test elements
        document.querySelectorAll('[id^="letter-"]').forEach(el => el.remove());
    });

    // ── scrollToArtistSection ──────────────────────────────────────────

    describe('scrollToArtistSection', () => {
        it('scrolls to naturalTop = scrollY + rect.top after temporarily setting position to static', () => {
            // Arrange
            const element = document.createElement('div');
            element.id = 'letter-S';
            document.body.appendChild(element);

            // When position is temporarily static, the element sits at its natural layout
            // position: simulate rect.top = 300 from the viewport while scrollY = 500.
            element.getBoundingClientRect = vi.fn(() => ({ top: 300 }));
            Object.defineProperty(window, 'scrollY', { configurable: true, value: 500 });
            window.scrollTo = vi.fn();

            // Act
            scrollToArtistSection('S');

            // Assert — naturalTop = 500 + 300 = 800
            expect(window.scrollTo).toHaveBeenCalledWith({ top: 800, behavior: 'instant' });
        });

        it('scrolls to top of document when element is at natural position 0 and page scrolled down', () => {
            // Simulate: A header's natural layout position is near the top (rect.top = -2800
            // when page is scrolled to 3000 and A is at document offset ~200).
            const element = document.createElement('div');
            element.id = 'letter-A';
            document.body.appendChild(element);

            element.getBoundingClientRect = vi.fn(() => ({ top: -2800 }));
            Object.defineProperty(window, 'scrollY', { configurable: true, value: 3000 });
            window.scrollTo = vi.fn();

            scrollToArtistSection('A');

            // naturalTop = 3000 + (-2800) = 200 — scrolls back near top
            expect(window.scrollTo).toHaveBeenCalledWith({ top: 200, behavior: 'instant' });
        });

        it('restores the element\'s original inline position style after measuring', () => {
            const element = document.createElement('div');
            element.id = 'letter-R';
            element.style.position = 'sticky';
            document.body.appendChild(element);
            element.getBoundingClientRect = vi.fn(() => ({ top: 0 }));
            Object.defineProperty(window, 'scrollY', { configurable: true, value: 0 });
            window.scrollTo = vi.fn();

            scrollToArtistSection('R');

            // The sticky override must be reverted
            expect(element.style.position).toBe('sticky');
        });

        it('logs a warning and does not throw when element is absent', () => {
            // Arrange — ensure #letter-Z does not exist
            const existing = document.getElementById('letter-Z');
            if (existing) existing.remove();

            // Act + Assert — must not throw
            expect(() => scrollToArtistSection('Z')).not.toThrow();
            expect(mockLogger.warn).toHaveBeenCalledWith(
                expect.stringContaining('letter-Z')
            );
        });
    });

    // ── observeArtistSections ──────────────────────────────────────────

    describe('observeArtistSections', () => {
        it('invokes OnLetterVisible callback when a section intersects', async () => {
            // Arrange — build a fake section header in the DOM
            const section = document.createElement('div');
            section.id = 'letter-A';
            section.className = 'artist-section-header';
            document.body.appendChild(section);

            let observerCallback = null;
            const mockObserver = {
                observe: vi.fn(),
                disconnect: vi.fn(),
            };
            global.IntersectionObserver = vi.fn(function (cb) {
                observerCallback = cb;
                return mockObserver;
            });

            const dotNetRef = {
                invokeMethodAsync: vi.fn().mockResolvedValue(undefined),
            };

            // Act
            observeArtistSections(dotNetRef);

            // Simulate section entering the viewport
            observerCallback([{ isIntersecting: true, target: section }]);

            // Allow the async invokeMethodAsync to settle
            await Promise.resolve();

            // Assert
            expect(dotNetRef.invokeMethodAsync).toHaveBeenCalledWith('OnLetterVisible', 'A');
        });

        it('uses findCurrentLetter when all entries are exiting (scrolling up)', async () => {
            // Arrange — two sections: A at viewport top (y=0, simulating sticky), B below top
            const sectionA = document.createElement('div');
            sectionA.id = 'letter-A';
            sectionA.className = 'artist-section-header';
            document.body.appendChild(sectionA);

            const sectionB = document.createElement('div');
            sectionB.id = 'letter-B';
            sectionB.className = 'artist-section-header';
            document.body.appendChild(sectionB);

            // Simulate: A is sticky at viewport top, B has moved below
            sectionA.getBoundingClientRect = vi.fn(() => ({ top: 0 }));
            sectionB.getBoundingClientRect = vi.fn(() => ({ top: 200 }));

            let observerCallback = null;
            const mockObserver = { observe: vi.fn(), disconnect: vi.fn() };
            global.IntersectionObserver = vi.fn(function (cb) { observerCallback = cb; return mockObserver; });

            const dotNetRef = { invokeMethodAsync: vi.fn().mockResolvedValue(undefined) };
            observeArtistSections(dotNetRef);

            // Simulate B exiting the observation zone (no intersecting entries)
            observerCallback([{ isIntersecting: false, target: sectionB }]);

            await Promise.resolve();

            // A has rect.top=0 so findCurrentLetter returns 'A'
            expect(dotNetRef.invokeMethodAsync).toHaveBeenCalledWith('OnLetterVisible', 'A');
        });

        it('returns the deepest sticky section when multiple sections are at top', async () => {
            // Arrange — A, B both sticky at y=0, C below (simulating being in B's section)
            const sections = ['A', 'B', 'C'].map(letter => {
                const el = document.createElement('div');
                el.id = `letter-${letter}`;
                el.className = 'artist-section-header';
                document.body.appendChild(el);
                return el;
            });
            const [sectionA, sectionB, sectionC] = sections;

            sectionA.getBoundingClientRect = vi.fn(() => ({ top: 0 }));
            sectionB.getBoundingClientRect = vi.fn(() => ({ top: 0 }));
            sectionC.getBoundingClientRect = vi.fn(() => ({ top: 300 }));

            let observerCallback = null;
            const mockObserver = { observe: vi.fn(), disconnect: vi.fn() };
            global.IntersectionObserver = vi.fn(function (cb) { observerCallback = cb; return mockObserver; });

            const dotNetRef = { invokeMethodAsync: vi.fn().mockResolvedValue(undefined) };
            observeArtistSections(dotNetRef);

            // Simulate C exiting the zone (no intersecting entries)
            observerCallback([{ isIntersecting: false, target: sectionC }]);

            await Promise.resolve();

            // B is the last section with top=0, so it is the current one
            expect(dotNetRef.invokeMethodAsync).toHaveBeenCalledWith('OnLetterVisible', 'B');
        });

        it('does not invoke callback when all entries exit and no section is at top', async () => {
            // Arrange — a single section well below the viewport top
            const section = document.createElement('div');
            section.id = 'letter-M';
            section.className = 'artist-section-header';
            document.body.appendChild(section);
            section.getBoundingClientRect = vi.fn(() => ({ top: 400 }));

            let observerCallback = null;
            const mockObserver = { observe: vi.fn(), disconnect: vi.fn() };
            global.IntersectionObserver = vi.fn(function (cb) { observerCallback = cb; return mockObserver; });

            const dotNetRef = { invokeMethodAsync: vi.fn().mockResolvedValue(undefined) };
            observeArtistSections(dotNetRef);

            observerCallback([{ isIntersecting: false, target: section }]);
            await Promise.resolve();

            expect(dotNetRef.invokeMethodAsync).not.toHaveBeenCalled();
        });

        it('disconnects previous observer before creating a new one', () => {
            // Arrange — two section headers
            ['A', 'B'].forEach(letter => {
                const el = document.createElement('div');
                el.id = `letter-${letter}`;
                el.className = 'artist-section-header';
                document.body.appendChild(el);
            });

            const firstObserver = { observe: vi.fn(), disconnect: vi.fn() };
            const secondObserver = { observe: vi.fn(), disconnect: vi.fn() };
            let callCount = 0;
            global.IntersectionObserver = vi.fn(function () {
                return callCount++ === 0 ? firstObserver : secondObserver;
            });

            const dotNetRef = { invokeMethodAsync: vi.fn() };

            // Act — call twice
            observeArtistSections(dotNetRef);
            observeArtistSections(dotNetRef);

            // Assert — first observer was disconnected
            expect(firstObserver.disconnect).toHaveBeenCalled();
        });
    });

    // ── disconnectArtistSectionObserver ────────────────────────────────

    describe('disconnectArtistSectionObserver', () => {
        it('calls observer.disconnect when an observer is active', () => {
            // Arrange
            const section = document.createElement('div');
            section.className = 'artist-section-header';
            section.id = 'letter-Q';
            document.body.appendChild(section);

            const mockObserver = { observe: vi.fn(), disconnect: vi.fn() };
            global.IntersectionObserver = vi.fn(function () { return mockObserver; });

            const dotNetRef = { invokeMethodAsync: vi.fn() };
            observeArtistSections(dotNetRef);

            // Act
            disconnectArtistSectionObserver();

            // Assert
            expect(mockObserver.disconnect).toHaveBeenCalled();
        });

        it('does not throw when no observer is active', () => {
            // Ensure observer is cleared first
            disconnectArtistSectionObserver();
            expect(() => disconnectArtistSectionObserver()).not.toThrow();
        });
    });
});
