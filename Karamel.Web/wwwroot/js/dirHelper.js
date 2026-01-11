export async function getDirectoryHandleByPath(rootHandle, path) {
    if (!path) return rootHandle;
    const parts = path.split('/').filter(p => p && p.length > 0);
    let current = rootHandle;
    for (const part of parts) {
        try {
            current = await current.getDirectoryHandle(part);
        } catch (err) {
            throw new Error(`Directory not found while traversing path: ${part} (full path: ${path})`);
        }
    }
    return current;
}

export async function getFileHandleByPath(rootHandle, pathWithFileName) {
    const parts = pathWithFileName.split('/').filter(p => p && p.length > 0);
    if (parts.length === 0) throw new Error('Invalid path');
    const fileName = parts.pop();
    let dir = rootHandle;
    for (const part of parts) {
        try {
            dir = await dir.getDirectoryHandle(part);
        } catch (err) {
            throw new Error(`Directory not found while traversing to file: ${part} (full path: ${pathWithFileName})`);
        }
    }
    try {
        return await dir.getFileHandle(fileName);
    } catch (err) {
        throw new Error(`File not found: ${fileName} (full path: ${pathWithFileName})`);
    }
}

export async function safeGetDirectoryHandle(rootHandle, path) {
    try {
        return await getDirectoryHandleByPath(rootHandle, path);
    } catch (err) {
        return null;
    }
}

export default { getDirectoryHandleByPath, getFileHandleByPath, safeGetDirectoryHandle };
