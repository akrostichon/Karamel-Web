// Karaoke player - CDG and audio synchronization

import CDGraphics from 'https://cdn.jsdelivr.net/npm/cdgraphics@7.0.0/+esm';
import { getMp3Data, getCdgData } from './fileAccess.js';

let cdgPlayer = null;
let audioElement = null;
let canvasElement = null;
let animationFrameId = null;

export function initializePlayer() {
    try {
        // Get DOM elements
        audioElement = document.getElementById('audioPlayer');
        canvasElement = document.getElementById('cdgCanvas');
        
        if (!audioElement || !canvasElement) {
            console.error('Audio or canvas element not found');
            return;
        }

        // Get file data from fileAccess module
        const mp3Data = getMp3Data();
        const cdgData = getCdgData();
        
        if (!mp3Data || !cdgData) {
            console.error('File data not available');
            return;
        }

        // Create blob and object URL for MP3
        const mp3Blob = new Blob([mp3Data], { type: 'audio/mpeg' });
        const mp3Url = URL.createObjectURL(mp3Blob);

        // Set audio source
        audioElement.src = mp3Url;
        audioElement.load();

        // Initialize CDG player (pass buffer directly to constructor)
        const context = canvasElement.getContext('2d');
        cdgPlayer = new CDGraphics(cdgData.buffer);

        // Set up event listeners
        audioElement.addEventListener('timeupdate', onTimeUpdate);
        audioElement.addEventListener('play', onPlay);
        audioElement.addEventListener('pause', onPause);
        audioElement.addEventListener('ended', onEnded);
        audioElement.addEventListener('seeked', onSeeked);

        console.log('Player initialized successfully');
        
        // Draw initial frame
        renderFrame();

    } catch (error) {
        console.error('Error initializing player:', error);
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
    console.log('Playback started');
    startAnimation();
}

function onPause() {
    console.log('Playback paused');
    stopAnimation();
}

function onEnded() {
    console.log('Playback ended');
    stopAnimation();
}

function onSeeked() {
    console.log('Seeked to:', audioElement.currentTime);
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
        console.error('Error rendering frame:', error);
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
}
