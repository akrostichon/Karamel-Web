// Karaoke player - CDG and audio synchronization

import CDGraphics from '/lib/cdgraphics/cdgraphics.esm.js';
import * as byteStore from './byteStore.js';
import { createLogger } from './logger.js';

const logger = createLogger('Player');

let cdgPlayer = null;
let audioElement = null;
let canvasElement = null;
let animationFrameId = null;
let dotNetRef = null;

export function initializePlayer() {
    return initializePlayerWithCallback(null);
}

export function initializePlayerWithCallback(dotNetReference) {
    try {
        dotNetRef = dotNetReference;
        
        // Get DOM elements
        audioElement = document.getElementById('audioPlayer');
        canvasElement = document.getElementById('cdgCanvas');
        
        if (!audioElement || !canvasElement) {
            logger.error('Audio or canvas element not found', {
                hasAudio: !!audioElement,
                hasCanvas: !!canvasElement
            });
            return;
        }

        // Get file data from byteStore
        const mp3Data = byteStore.getBytes('mp3');
        const cdgData = byteStore.getBytes('cdg');

        if (!mp3Data || !cdgData) {
            const error = new Error('Song files not loaded - loadSongFiles must be called before initializePlayerWithCallback');
            logger.error(error.message, {
                hasMp3Data: !!mp3Data,
                hasCdgData: !!cdgData
            });
            throw error;
        }

        // Prefer cached object URL from byteStore
        let mp3Url = byteStore.createObjectUrl('mp3', 'audio/mpeg');
        if (!mp3Url) {
            const mp3Blob = new Blob([mp3Data.buffer.slice(mp3Data.byteOffset, mp3Data.byteOffset + mp3Data.byteLength)], { type: 'audio/mpeg' });
            mp3Url = URL.createObjectURL(mp3Blob);
        }

        // Set audio source
        audioElement.src = mp3Url;
        audioElement.load();

        // Initialize CDG player (pass buffer directly to constructor)
        cdgPlayer = new CDGraphics(cdgData.buffer);

        // Set up event listeners
        audioElement.addEventListener('timeupdate', onTimeUpdate);
        audioElement.addEventListener('play', onPlay);
        audioElement.addEventListener('pause', onPause);
        audioElement.addEventListener('ended', onEnded);
        audioElement.addEventListener('seeked', onSeeked);

        logger.debug('Player initialized successfully');
        
        // Draw initial frame
        renderFrame();
        
        // Auto-play
        audioElement.play().catch(err => logger.error('Auto-play failed', { error: err.message }));

    } catch (error) {
        logger.error('Error initializing player', { error: error.message, stack: error.stack });
        throw error;
    }
}

function onTimeUpdate() {
    if (audioElement && cdgPlayer) {
        // Render frame based on current audio time
        renderFrame();
    }
}

function onPlay() {
    logger.debug('Playback started');
    startAnimation();
}

function onPause() {
    logger.debug('Playback paused');
    stopAnimation();
}

function onEnded() {
    logger.debug('Playback ended');
    stopAnimation();
    
    // Call .NET callback if available
    if (dotNetRef) {
        dotNetRef.invokeMethodAsync('OnSongEnded')
            .catch(err => logger.error('Error calling OnSongEnded', { error: err.message }));
    }
}

function onSeeked() {
    logger.debug('Seeked to', { currentTime: audioElement.currentTime });
    renderFrame();
}

function startAnimation() {
    if (!animationFrameId) {
        animate();
    }
}

function stopAnimation() {
    if (animationFrameId) {
        cancelAnimationFrame(animationFrameId);
        animationFrameId = null;
    }
}

function animate() {
    renderFrame();
    animationFrameId = requestAnimationFrame(animate);
}

function renderFrame() {
    if (!cdgPlayer || !canvasElement || !audioElement) {
        return;
    }

    try {
        const context = canvasElement.getContext('2d');
        const currentTime = audioElement.currentTime;

        // Render CDG frame for current time
        const frame = cdgPlayer.render(currentTime, {
            forceKey: false
        });

        // Draw the frame if it changed
        if (frame && frame.isChanged && frame.imageData) {
            // CDG standard size is 300x216
            context.putImageData(frame.imageData, 0, 0);
        }
    } catch (error) {
        logger.error('Error rendering frame', { error: error.message, stack: error.stack });
    }
}

export function dispose() {
    stopAnimation();
    
    if (audioElement) {
        audioElement.removeEventListener('timeupdate', onTimeUpdate);
        audioElement.removeEventListener('play', onPlay);
        audioElement.removeEventListener('pause', onPause);
        audioElement.removeEventListener('ended', onEnded);
        audioElement.removeEventListener('seeked', onSeeked);
        audioElement.pause();
        audioElement.src = '';
    }

    cdgPlayer = null;
    audioElement = null;
    canvasElement = null;
    dotNetRef = null;
}

export function pausePlayback() {
    if (audioElement) {
        audioElement.pause();
    }
}

export function resumePlayback() {
    if (audioElement) {
        audioElement.play().catch(err => logger.error('Resume failed', { error: err.message }));
    }
}

export function stopPlayback() {
    if (audioElement) {
        audioElement.pause();
        audioElement.currentTime = 0;
    }
    stopAnimation();
}
