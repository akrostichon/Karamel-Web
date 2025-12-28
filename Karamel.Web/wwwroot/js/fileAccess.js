// File System Access API wrapper for loading MP3 and CDG files
// Store file data in module-level variables to avoid JSON serialization issues

let mp3Data = null;
let cdgData = null;

export async function pickMp3File() {
    try {
        const [fileHandle] = await window.showOpenFilePicker({
            types: [{
                description: 'MP3 Audio Files',
                accept: { 'audio/mpeg': ['.mp3'] }
            }],
            multiple: false
        });
        
        const file = await fileHandle.getFile();
        const arrayBuffer = await file.arrayBuffer();
        mp3Data = new Uint8Array(arrayBuffer);
        
        return {
            name: file.name,
            size: file.size
        };
    } catch (error) {
        console.error('Error picking MP3 file:', error);
        return null;
    }
}

export async function pickCdgFile() {
    try {
        const [fileHandle] = await window.showOpenFilePicker({
            types: [{
                description: 'CDG Graphics Files',
                accept: { 'application/octet-stream': ['.cdg'] }
            }],
            multiple: false
        });
        
        const file = await fileHandle.getFile();
        const arrayBuffer = await file.arrayBuffer();
        cdgData = new Uint8Array(arrayBuffer);
        
        return {
            name: file.name,
            size: file.size
        };
    } catch (error) {
        console.error('Error picking CDG file:', error);
        return null;
    }
}

export function getMp3Data() {
    return mp3Data;
}

export function getCdgData() {
    return cdgData;
}

export function hasFiles() {
    return mp3Data !== null && cdgData !== null;
}
