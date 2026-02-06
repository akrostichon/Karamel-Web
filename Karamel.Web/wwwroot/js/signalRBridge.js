// signalRBridge.js
// Real SignalR client with graceful fallback to BroadcastChannel + sessionStorage.
// Exposes the same API used by `SessionService.cs` so Blazor interop keeps working.

let broadcastChannel = null;
let isMainTab = false;
let currentSessionId = null;
let tabId = null;
let hubConnection = null;
let usingSignalR = false;
let backendBaseUrl = null;
let currentLinkToken = null;

function getChannelName(sessionId) {
	return `karamel-session-${sessionId}`;
}

function getSessionKey(sessionId) {
	return `karamel-session-${sessionId}`;
}

// Dynamically load SignalR script from CDN if needed
async function ensureSignalRLoaded() {
	if (typeof signalR !== 'undefined') {
		return true;
	}

	// Try dynamic import first (works if installed as module)
	try {
		const pkg = '@microsoft' + '/signalr';
		const mod = await import(pkg);
		if (mod && (mod.HubConnection || mod.HubConnectionBuilder || mod.signalR)) {
			window.signalR = mod;
			return true;
		}
	} catch (e) {
		// ignore and fallback to CDN
	}

	// Fallback: inject script tag from CDN (UMD build exposes global `signalR`)
	return new Promise((resolve) => {
		const existing = document.querySelector('script[data-signalr]');
		if (existing) {
			existing.addEventListener('load', () => resolve(typeof signalR !== 'undefined'));
			existing.addEventListener('error', () => resolve(false));
			return;
		}

		const script = document.createElement('script');
		script.setAttribute('data-signalr', '1');
		script.src = 'https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/7.0.5/signalr.min.js';
		script.onload = () => resolve(typeof signalR !== 'undefined');
		script.onerror = () => resolve(false);
		document.head.appendChild(script);
	});
}

async function tryConnectSignalR(sessionId, linkToken, backendUrl) {
	try {
		const ok = await ensureSignalRLoaded();
		if (!ok) return false;

		// Build connection to hub. If a linkToken is provided, prefer accessTokenFactory
		// and also include X-Link-Token header for transports that use headers.
		const urlOptions = {};
		if (linkToken) {
			urlOptions.accessTokenFactory = () => linkToken;
			urlOptions.headers = { 'X-Link-Token': linkToken };
		}

		// Use backend URL if provided, otherwise use relative path
		const hubUrl = backendUrl ? `${backendUrl}/hubs/playlist` : '/hubs/playlist';

		hubConnection = new signalR.HubConnectionBuilder()
			.withUrl(hubUrl, urlOptions)
			.withAutomaticReconnect()
			.build();

		// Wire receive handler
		hubConnection.on('ReceivePlaylistUpdated', (dto) => {
			// Map DTO shape to session-state expected by client
			try {
				const items = (dto.items || dto.Items || []).map(i => ({
					id: i.songId || i.SongId,  // Use Song ID (for library lookup), not playlist item ID
					artist: i.artist || i.Artist,
					title: i.title || i.Title,
					addedBySinger: i.singerName || i.SingerName || null,
					status: i.status || i.Status || 0,  // NEW: Include song status (0=Queued, 1=UpNext, 2=NowPlaying, 3=Completed)
					itemId: i.id || i.Id  // NEW: Playlist item ID for status updates
				}));

				// Extract currentSong from DTO (first NowPlaying item, or null)
				const currentSongDto = dto.currentSong || dto.CurrentSong;
				const currentSong = currentSongDto ? {
					id: currentSongDto.songId || currentSongDto.SongId,
					artist: currentSongDto.artist || currentSongDto.Artist,
					title: currentSongDto.title || currentSongDto.Title,
					addedBySinger: currentSongDto.singerName || currentSongDto.SingerName || null,
					status: currentSongDto.status || currentSongDto.Status || 0,
					itemId: currentSongDto.id || currentSongDto.Id
				} : null;

				// Calculate singer song counts
				const singerSongCounts = {};
				items.forEach(item => {
					const singer = item.addedBySinger || 'Unknown';
					singerSongCounts[singer] = (singerSongCounts[singer] || 0) + 1;
				});

				const data = {
					queue: items,
					currentSong: currentSong,  // CHANGED: Extract from DTO instead of hardcoding null
					singerSongCounts
				};

				// Persist to sessionStorage and fire update event used by Blazor
				if (currentSessionId) {
					const state = getSessionStateForSession(currentSessionId);
					state.playlist = data;
					sessionStorage.setItem(getSessionKey(currentSessionId), JSON.stringify(state));
				}

				const event = new CustomEvent('session-state-updated', { detail: { type: 'playlist-updated', data } });
				window.dispatchEvent(event);
			} catch (e) {
				console.warn('Error handling ReceivePlaylistUpdated:', e);
			}
		});

		await hubConnection.start();
		// Join session group
		await hubConnection.invoke('JoinSession', sessionId);
		usingSignalR = true;
		return true;
	} catch (e) {
		console.warn('SignalR connection failed, falling back to BroadcastChannel:', e);
		usingSignalR = false;
		hubConnection = null;
		return false;
	}
}

/**
 * Initialize session bridge (SignalR preferred, BroadcastChannel fallback)
 */
export function initializeSession(sessionId, asMainTab, linkToken, backendUrl) {
	if (!sessionId) throw new Error('sessionId is required');

	console.log(`signalRBridge.initializeSession called: sessionId=${sessionId}, linkToken=${linkToken ? '(present)' : '(null)'}, backendUrl=${backendUrl}`);

	currentSessionId = sessionId;
	isMainTab = !!asMainTab;
	backendBaseUrl = backendUrl || null;
	currentLinkToken = linkToken || null;

	console.log(`signalRBridge: Stored currentLinkToken=${currentLinkToken ? '(present)' : '(null)'}`);
	try {
		tabId = (typeof crypto !== 'undefined' && crypto.randomUUID) ? crypto.randomUUID() : ('tab-' + Math.random().toString(36).slice(2));
	} catch (e) {
		tabId = 'tab-' + Math.random().toString(36).slice(2);
	}

	// Create BroadcastChannel synchronously to preserve original behavior expected in tests
	try {
		broadcastChannel = new BroadcastChannel(getChannelName(sessionId));
	} catch (e) {
		throw new Error('Broadcast Channel API is not supported in this browser');
	}

	if (broadcastChannel) {
		if (isMainTab) {
			broadcastChannel.onmessage = (event) => {
				if (event.data && event.data.senderId === tabId) return;
				try {
					// Respond to state requests from secondary tabs
					if (event.data && event.data.type === 'request-state') {
						const requested = getSessionStateForSession(sessionId);
						broadcastChannel.postMessage({ type: 'state-sync-response', data: requested, senderId: tabId });
						return;
					}
					// Respond to health pings
					if (event.data && event.data.type === 'ping') {
						broadcastChannel.postMessage({ type: 'ping-response', senderId: tabId });
						return;
					}
					// Fallback to normal handling for other message types
					handleBroadcastMessage(event.data);
				} catch (e) {
					console.error('Error handling broadcast message on main tab:', e);
				}
			};
		} else {
			broadcastChannel.onmessage = (event) => {
				if (event.data && event.data.senderId === tabId) return;
				if (event.data && event.data.type === 'state-sync-response') {
					sessionStorage.setItem(getSessionKey(sessionId), JSON.stringify(event.data.data));
					const stateEvent = new CustomEvent('session-state-synced', { detail: event.data.data });
					window.dispatchEvent(stateEvent);
				} else {
					handleBroadcastMessage(event.data);
				}
			};

			// Request state from main tab
			broadcastChannel.postMessage({ type: 'request-state', timestamp: Date.now(), senderId: tabId });
		}
	}

	// Attempt SignalR connection in background; do not block initialization
	tryConnectSignalR(sessionId, linkToken, backendUrl).catch(() => {});

	console.log(`Session bridge initialized as ${isMainTab ? 'MAIN' : 'SECONDARY'} tab for session ${sessionId} (signalR=${usingSignalR})`);
}

function handleBroadcastMessage(message) {
	try {
		saveToSessionStorage(message.type, message.data);
		const event = new CustomEvent('session-state-updated', { detail: message });
		window.dispatchEvent(event);
	} catch (e) {
		console.warn('Error in handleBroadcastMessage', e);
	}
}

function saveToSessionStorage(type, data) {
	try {
		if (!currentSessionId) return;
		const sessionState = getSessionStateForSession(currentSessionId);
		switch (type) {
			case 'playlist-updated':
				sessionState.playlist = data;
				break;
			case 'session-settings':
				sessionState.session = data;
				break;
			case 'current-song':
				sessionState.currentSong = data;
				break;
			default:
				console.warn('Unknown state type:', type);
				return;
		}
		sessionStorage.setItem(getSessionKey(currentSessionId), JSON.stringify(sessionState));
	} catch (e) {
		console.error('Failed to save to sessionStorage:', e);
	}
}

export function broadcastStateUpdate(type, data) {
	// Persist locally first
	saveToSessionStorage(type, data);

	// Emit local BroadcastChannel message if available
	const message = { type, data, timestamp: Date.now(), senderId: tabId };
	if (usingSignalR && hubConnection) {
		// We don't have a generic server-side method for arbitrary state types.
		// For playlist updates, prefer to let the server be the source of truth.
		// For now, just rely on BroadcastChannel to notify other tabs and persist to storage.
	}

	if (broadcastChannel) {
		broadcastChannel.postMessage(message);
	}
}

// RPC helpers: call server hub methods when connected, otherwise persist locally and broadcast
// Updated signature: addItemToPlaylist(songId, singerName)
// Matches backend PlaylistHub.AddItemAsync(sessionId, songId, singerName)
// Uses one-playlist-per-session architecture (no playlistId parameter needed)
export async function addItemToPlaylist(songId, singerName) {
	if (!currentSessionId) {
		console.error('addItemToPlaylist: No current session ID');
		return false;
	}

	if (usingSignalR && hubConnection) {
		try {
			// PlaylistHub.AddItemAsync(Guid sessionId, Guid songId, string? singerName)
			// One playlist per session - no playlistId parameter needed
			await hubConnection.invoke('AddItemAsync', currentSessionId, songId, singerName || null);
			return true;
		} catch (e) {
			console.warn('AddItemAsync via SignalR failed, falling back to local broadcast:', e);
		}
	}

	// Fallback: create a minimal item object for local broadcast
	// Note: This fallback doesn't have full song metadata, just ID
	const fallbackItem = {
		id: crypto.randomUUID(),
		songId: songId,
		addedBySinger: singerName || null,
		artist: '', // Will be enriched by main tab
		title: ''   // Will be enriched by main tab
	};
	broadcastStateUpdate('playlist-updated', { queue: [fallbackItem] });
	return false;
}

export async function removeItemFromPlaylist(itemId) {
	if (!currentSessionId) {
		console.error('removeItemFromPlaylist: No current session ID');
		return false;
	}

	if (usingSignalR && hubConnection) {
		try {
			// PlaylistHub.RemoveItemAsync(Guid sessionId, Guid itemId)
			await hubConnection.invoke('RemoveItemAsync', currentSessionId, itemId);
			return true;
		} catch (e) {
			console.warn('RemoveItemAsync via SignalR failed, falling back to local broadcast:', e);
		}
	}

	// Fallback: remove locally and broadcast
	const state = getSessionState();
	if (state && state.playlist && Array.isArray(state.playlist.queue)) {
		state.playlist.queue = state.playlist.queue.filter(i => i.id !== itemId);
		sessionStorage.setItem(getSessionKey(currentSessionId), JSON.stringify(state));
		broadcastStateUpdate('playlist-updated', state.playlist);
	}
	return false;
}

export async function reorderPlaylist(from, to) {
	if (!currentSessionId) {
		console.error('reorderPlaylist: No current session ID');
		return false;
	}

	if (usingSignalR && hubConnection) {
		try {
			// PlaylistHub.ReorderAsync(Guid sessionId, int from, int to)
			await hubConnection.invoke('ReorderAsync', currentSessionId, from, to);
			return true;
		} catch (e) {
			console.warn('ReorderAsync via SignalR failed, falling back to local broadcast:', e);
		}
	}

	// Fallback: persist new order and broadcast
	const state = getSessionState();
	if (state) {
		state.playlist = state.playlist || { queue: [] };
		// Note: fallback doesn't support from/to reordering - would need array manipulation
		sessionStorage.setItem(getSessionKey(currentSessionId), JSON.stringify(state));
		broadcastStateUpdate('playlist-updated', state.playlist);
	}
	return false;
}

/**
 * Set the status of a specific playlist item.
 * @param {string} itemId - Playlist item ID (not song ID)
 * @param {number} status - SongStatus enum value (0=Queued, 1=UpNext, 2=NowPlaying, 3=Completed)
 * @returns {Promise<boolean>} True if successful via SignalR
 */
export async function setSongStatus(itemId, status) {
	if (!currentSessionId) {
		console.error('setSongStatus: No current session ID');
		return false;
	}

	if (usingSignalR && hubConnection) {
		try {
			// PlaylistHub.SetSongStatusAsync(Guid sessionId, Guid itemId, int status)
			await hubConnection.invoke('SetSongStatusAsync', currentSessionId, itemId, status);
			return true;
		} catch (e) {
			console.warn('SetSongStatusAsync via SignalR failed:', e);
			return false;
		}
	}

	console.warn('setSongStatus: SignalR not connected, cannot update status');
	return false;
}

/**
 * Advance to the next song: marks current NowPlaying as Completed, marks first UpNext as NowPlaying.
 * @returns {Promise<boolean>} True if successful via SignalR
 */
export async function advanceToNextSong() {
	if (!currentSessionId) {
		console.error('advanceToNextSong: No current session ID');
		return false;
	}

	if (usingSignalR && hubConnection) {
		try {
			// PlaylistHub.AdvanceToNextSongAsync(Guid sessionId)
			await hubConnection.invoke('AdvanceToNextSongAsync', currentSessionId);
			return true;
		} catch (e) {
			console.warn('AdvanceToNextSongAsync via SignalR failed:', e);
			return false;
		}
	}

	console.warn('advanceToNextSong: SignalR not connected, cannot advance song');
	return false;
}

/**
 * Complete the current song without advancing to the next one.
 * Marks current NowPlaying as Completed. Next song remains in queue.
 * @returns {Promise<boolean>} True if successful via SignalR
 */
export async function completeCurrentSong() {
	if (!currentSessionId) {
		console.error('completeCurrentSong: No current session ID');
		return false;
	}

	if (usingSignalR && hubConnection) {
		try {
			// PlaylistHub.CompleteCurrentSongAsync(Guid sessionId)
			await hubConnection.invoke('CompleteCurrentSongAsync', currentSessionId);
			return true;
		} catch (e) {
			console.warn('CompleteCurrentSongAsync via SignalR failed:', e);
			return false;
		}
	}

	console.warn('completeCurrentSong: SignalR not connected, cannot complete song');
	return false;
}

/**
 * Clear all queued and up-next songs from the playlist, preserving the currently playing song.
 * @returns {Promise<boolean>} True if successful via SignalR
 */
export async function clearQueue() {
	if (!currentSessionId) {
		console.error('clearQueue: No current session ID');
		return false;
	}

	if (usingSignalR && hubConnection) {
		try {
			// PlaylistHub.ClearQueueAsync(Guid sessionId)
			await hubConnection.invoke('ClearQueueAsync', currentSessionId);
			return true;
		} catch (e) {
			console.warn('ClearQueueAsync via SignalR failed:', e);
			return false;
		}
	}

	console.warn('clearQueue: SignalR not connected, cannot clear queue');
	return false;
}

export function isUsingSignalR() {
	return !!usingSignalR && !!hubConnection && hubConnection.state === (window.signalR ? window.signalR.HubConnectionState?.Connected : 1);
}

export async function fetchLibraryPage(sessionId, page = 1, pageSize = 50, search = null, sort = null) {
	if (!sessionId) throw new Error('sessionId is required');

	// Prefer SignalR RPC when connected
	if (usingSignalR && hubConnection) {
		try {
			const res = await hubConnection.invoke('GetLibraryPage', sessionId, page, pageSize, search, sort);
			// Expect shape: { items, page, pageSize, totalCount }
			return res;
		} catch (e) {
			console.warn('fetchLibraryPage via SignalR failed, falling back to REST:', e);
		}
	}

	// REST fallback
	try {
		console.log(`fetchLibraryPage REST fallback: currentLinkToken=${currentLinkToken ? '(present)' : '(null)'}`);
		const params = new URLSearchParams();
		params.set('page', String(page));
		params.set('pageSize', String(pageSize));
		if (search) params.set('search', search);
		if (sort) params.set('sort', sort);

		// Use backend URL if available, otherwise use relative path
		const baseUrl = backendBaseUrl || '';
		const url = `${baseUrl}/api/sessions/${sessionId}/library?${params.toString()}`;
		const headers = { 'Accept': 'application/json' };
		
		// Always use stored currentLinkToken for library fetches
		if (currentLinkToken) {
			headers['X-Link-Token'] = currentLinkToken;
			console.log(`fetchLibraryPage: Added X-Link-Token header`);
		} else {
			console.warn(`fetchLibraryPage: No currentLinkToken available!`);
		}
		const resp = await fetch(url, { method: 'GET', headers });
		if (!resp.ok) {
			console.warn('fetchLibraryPage REST failed', resp.status, await resp.text());
			return { items: [], page, pageSize, totalCount: 0 };
		}
		const items = await resp.json();
		const total = parseInt(resp.headers.get('X-Total-Count') || '0');
		return { items, page, pageSize, totalCount: total };
	} catch (e) {
		console.warn('fetchLibraryPage exception', e);
		return { items: [], page, pageSize, totalCount: 0 };
	}
}

export async function searchLibrary(sessionId, query, maxResults = 10) {
	if (!sessionId) throw new Error('sessionId is required');
	if (!query) return [];

	// Prefer SignalR when available
	if (usingSignalR && hubConnection) {
		try {
			const res = await hubConnection.invoke('SearchLibrary', sessionId, query, maxResults);
			return res;
		} catch (e) {
			console.warn('searchLibrary via SignalR failed, falling back to REST:', e);
		}
	}

	try {
		const params = new URLSearchParams();
		params.set('search', query);
		params.set('page', '1');
		params.set('pageSize', String(maxResults));
		// Use backend URL if available, otherwise use relative path
		const baseUrl = backendBaseUrl || '';
		const url = `${baseUrl}/api/sessions/${sessionId}/library?${params.toString()}`;
		const headers = {};
		if (currentLinkToken) {
			headers['X-Link-Token'] = currentLinkToken;
		}
		const resp = await fetch(url, { headers });
		if (!resp.ok) return [];
		const items = await resp.json();
		return items;
	} catch (e) {
		console.warn('searchLibrary REST failed', e);
		return [];
	}
}

export async function uploadLibraryToServer(sessionId, libraryData, options = {}) {
	try {
		if (!sessionId) throw new Error('sessionId is required');
		// Use backend URL if available, otherwise use relative path
		const baseUrl = backendBaseUrl || '';
		const url = `${baseUrl}/api/sessions/${sessionId}/library/bulk`;
		const headers = { 'Content-Type': 'application/json' };
		
		// Use provided token or fall back to stored currentLinkToken
		const tokenToUse = options.linkToken || currentLinkToken;
		console.log(`uploadLibraryToServer: options.linkToken=${options.linkToken ? '(present)' : '(null)'}, currentLinkToken=${currentLinkToken ? '(present)' : '(null)'}, using=${tokenToUse ? '(present)' : '(null)'}`);
		if (tokenToUse) {
			headers['X-Link-Token'] = tokenToUse;
			console.log(`uploadLibraryToServer: X-Link-Token header set`);
		} else {
			console.warn('uploadLibraryToServer: No link token available - request will likely fail');
		}

		// PRIVACY: Sanitize payload - only send id, artist, title, metadataJson (never file paths)
		const songs = (libraryData && libraryData.songs) 
			? libraryData.songs.map(s => ({ 
				id: s.id, 
				artist: s.artist || '', 
				title: s.title || '', 
				metadataJson: s.metadataJson || null  // Future: duration/genre (never paths)
			})) 
			: [];

		const resp = await fetch(url, { method: 'POST', headers, body: JSON.stringify(songs) });
		if (!resp.ok) {
			console.warn('uploadLibraryToServer failed', resp.status, await resp.text());
			return false;
		}
		return true;
	} catch (e) {
		console.warn('uploadLibraryToServer exception', e);
		return false;
	}
}

export function getSessionStateForSession(sessionId) {
	try {
		if (!sessionId) throw new Error('sessionId is required');
		const stored = sessionStorage.getItem(getSessionKey(sessionId));
		return stored ? JSON.parse(stored) : { session: null, library: null, playlist: null, currentSong: null };
	} catch (error) {
		console.error('Failed to read from sessionStorage:', error);
		return { session: null, library: null, playlist: null, currentSong: null };
	}
}

export function getSessionState() {
	if (!currentSessionId) return { session: null, library: null, playlist: null, currentSong: null };
	return getSessionStateForSession(currentSessionId);
}

export function clearSessionState() {
	try {
		if (currentSessionId) {
			sessionStorage.removeItem(getSessionKey(currentSessionId));
		}
		if (broadcastChannel) {
			broadcastChannel.postMessage({ type: 'session-ended', timestamp: Date.now() });
			broadcastChannel.close();
			broadcastChannel = null;
		}
		if (usingSignalR && hubConnection) {
			try {
				hubConnection.invoke('LeaveSession', currentSessionId).catch(() => {});
				hubConnection.stop().catch(() => {});
			} catch (e) {}
			hubConnection = null;
			usingSignalR = false;
		}
		console.log('Session state cleared for session', currentSessionId);
		currentSessionId = null;
	} catch (error) {
		console.error('Failed to clear session state:', error);
	}
}

export function generateSessionUrl(path, sessionId, linkToken = null) {
	const url = new URL(path, window.location.origin);
	url.searchParams.set('session', sessionId);
	if (linkToken) {
		url.searchParams.set('token', linkToken);
	}
	return url.toString();
}

export function getSessionIdFromUrl() {
	const params = new URLSearchParams(window.location.search);
	return params.get('session');
}

export function setupStateSyncListener(dotNetRef) {
	// If we already have session state in sessionStorage, signal immediate sync
	try {
		const state = getSessionState();
		if (state && state.session) {
			dotNetRef.invokeMethodAsync('OnStateSynced').catch(() => {});
			return;
		}
	} catch (e) {
		// ignore
	}

	const handler = (event) => {
		if (event.type === 'session-state-synced') {
			dotNetRef.invokeMethodAsync('OnStateSynced').catch(() => {});
			window.removeEventListener('session-state-synced', handler);
		}
	};
	window.addEventListener('session-state-synced', handler);
	setTimeout(() => window.removeEventListener('session-state-synced', handler), 3000);
}

export function setupStateUpdateListener(dotNetRef) {
	const handler = (event) => {
		if (event.type === 'session-state-updated') {
			dotNetRef.invokeMethodAsync('OnStateUpdated', event.detail.type, event.detail.data);
		}
	};
	window.addEventListener('session-state-updated', handler);
}

export function checkMainTabAlive() {
	return new Promise((resolve) => {
		if (isMainTab) { resolve(true); return; }
		if (usingSignalR && hubConnection && hubConnection.state === signalR.HubConnectionState.Connected) {
			resolve(true); return;
		}
		if (!broadcastChannel) { resolve(false); return; }

		const timeoutId = setTimeout(() => {
			if (broadcastChannel && broadcastChannel.removeEventListener) {
				broadcastChannel.removeEventListener('message', handlePing);
			}
			resolve(false);
		}, 2000);

		function handlePing(event) {
			if (event.data && event.data.type === 'ping-response') {
				clearTimeout(timeoutId);
				if (broadcastChannel && broadcastChannel.removeEventListener) {
					broadcastChannel.removeEventListener('message', handlePing);
				}
				resolve(true);
			}
		}

		if (broadcastChannel && broadcastChannel.addEventListener) {
			broadcastChannel.addEventListener('message', handlePing);
			broadcastChannel.postMessage({ type: 'ping' });
		} else {
			resolve(false);
		}
	});
}

// Ensure main-tab ping responder
if (typeof window !== 'undefined') {
	window.addEventListener('beforeunload', () => {
		if (isMainTab && broadcastChannel) {
			broadcastChannel.postMessage({ type: 'main-tab-closing', timestamp: Date.now() });
		}
	});
}
