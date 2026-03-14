/**
 * alphabetBridge.js - Alphabet navigation bridge for artist browse mode
 * Provides scroll-to-letter and section-observation functionality for the A–Z bar.
 */

import { createLogger } from './logger.js';

const logger = createLogger('AlphabetBridge');

/**
 * Scrolls the artist list to the section header for the given letter.
 *
 * `scrollIntoView` is a no-op when the element is pinned by `position: sticky`
 * (browser considers it already in view).  `getBoundingClientRect().top` is also
 * 0 when pinned, making `window.scrollY + rect.top` equal the current scroll
 * position — another no-op.  `offsetTop` can equally report the pinned (visual)
 * offset rather than the natural layout offset in some browsers.
 *
 * The reliable fix: temporarily override the element's inline `position` to
 * `static`, measure its `getBoundingClientRect()` (which now reflects the true
 * natural layout position), then restore the style and scroll.  All three steps
 * happen synchronously in a single JS task — no visual paint occurs in between.
 *
 * @param {string} letter - The uppercase letter to scroll to (e.g. 'A', 'S')
 */
export function scrollToArtistSection(letter) {
    const element = document.getElementById(`letter-${letter}`);
    if (!element) {
        logger.warn(`scrollToArtistSection: element #letter-${letter} not found`);
        return;
    }
    // Temporarily disable sticky to read the element's natural document position.
    const savedPosition = element.style.position;
    element.style.position = 'static';
    const naturalTop = window.scrollY + element.getBoundingClientRect().top;
    element.style.position = savedPosition;
    window.scrollTo({ top: naturalTop, behavior: 'instant' });
}

/** @type {IntersectionObserver|null} */
let observer = null;

/**
 * Finds the current letter section by inspecting which section headers are pinned
 * at the viewport top.  Because headers use `position: sticky; top: 0`, every
 * section we have scrolled past will have getBoundingClientRect().top === 0.
 * The deepest (last in document order) such header is the one we are currently
 * inside.
 *
 * @returns {string|null} uppercase letter key, or null if none found
 */
function findCurrentLetter() {
    const sections = document.querySelectorAll('.artist-section-header');
    let current = null;
    for (const section of sections) {
        if (section.getBoundingClientRect().top <= 1) {
            // Sticky (or naturally at top) — we have scrolled into or past this section
            current = section.id.replace('letter-', '');
        } else {
            // Sections are in document order; first one below the threshold means done
            break;
        }
    }
    return current;
}

/**
 * Starts observing all .artist-section-header elements and notifies the .NET
 * component via invokeMethodAsync when a section enters the top viewport strip.
 * @param {object} dotNetRef - DotNetObjectReference for invoking C# callbacks
 */
export function observeArtistSections(dotNetRef) {
    if (observer) {
        observer.disconnect();
        observer = null;
    }

    const sections = document.querySelectorAll('.artist-section-header');
    if (sections.length === 0) return;

    observer = new IntersectionObserver((entries) => {
        // Look for a section that just entered the observation zone (scrolling down)
        const entering = entries.find(e => e.isIntersecting);
        if (entering) {
            const letter = entering.target.id.replace('letter-', '');
            dotNetRef.invokeMethodAsync('OnLetterVisible', letter)
                .catch(err => logger.warn(`OnLetterVisible callback failed: ${err}`));
        } else {
            // All entries are exiting — this means we are scrolling upward or snapping
            // back to a higher position.  Sticky headers from previously-visited sections
            // remain at viewport y=0 indefinitely, so they never re-fire
            // isIntersecting=true.  Query the DOM directly to find the current section.
            const letter = findCurrentLetter();
            if (letter) {
                dotNetRef.invokeMethodAsync('OnLetterVisible', letter)
                    .catch(err => logger.warn(`OnLetterVisible callback failed: ${err}`));
            }
        }
    }, {
        threshold: 0,
        rootMargin: '-1px 0px -90% 0px'
    });

    sections.forEach(section => observer.observe(section));
}

/**
 * Disconnects the active IntersectionObserver, if any.
 */
export function disconnectArtistSectionObserver() {
    if (observer) {
        observer.disconnect();
        observer = null;
    }
}
