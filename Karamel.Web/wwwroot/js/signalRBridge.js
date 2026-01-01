// signalRBridge.js
// Real SignalR client with graceful fallback to BroadcastChannel + sessionStorage.
// Exposes the same API used by `SessionService.cs` so Blazor interop keeps working.

let broadcastChannel = null;
let isMainTab = false;
let currentSessionId = null;
let tabId = null;
let hubConnection = null;
let usingSignalR = false;

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

async function tryConnectSignalR(sessionId, linkToken) {
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

		hubConnection = new signalR.HubConnectionBuilder()
			.withUrl('/hubs/playlist', urlOptions)
			.withAutomaticReconnect()
			.build();

		// Wire receive handler
		hubConnection.on('ReceivePlaylistUpdated', (dto) => {
			// Map DTO shape to legacy session-state expected by client
			try {
				const items = (dto.items || dto.Items || []).map(i => ({
					id: i.id || i.Id || i.Id, artist: i.artist || i.Artist || i.Artist,
					title: i.title || i.Title || i.Title, addedBySinger: i.singerName || i.SingerName || null
				}));

				const data = {
					queue: items,
					currentSong: null,
					singerSongCounts: {}
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
export function initializeSession(sessionId, asMainTab, linkToken) {
	if (!sessionId) throw new Error('sessionId is required');

	currentSessionId = sessionId;
	isMainTab = !!asMainTab;
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
	tryConnectSignalR(sessionId, linkToken).catch(() => {});

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
export async function addItemToPlaylist(item) {
	if (usingSignalR && hubConnection) {
		try {
			await hubConnection.invoke('AddItemAsync', item);
			return true;
		} catch (e) {
			console.warn('AddItemAsync via SignalR failed, falling back to local broadcast:', e);
		}
	}

	// Fallback: update local storage and broadcast
	broadcastStateUpdate('playlist-updated', { queue: [item] });
	return false;
}

export async function removeItemFromPlaylist(itemId) {
	if (usingSignalR && hubConnection) {
		try {
			await hubConnection.invoke('RemoveItemAsync', itemId);
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

export async function reorderPlaylist(newOrder) {
	if (usingSignalR && hubConnection) {
		try {
			await hubConnection.invoke('ReorderAsync', newOrder);
			return true;
		} catch (e) {
			console.warn('ReorderAsync via SignalR failed, falling back to local broadcast:', e);
		}
	}

	// Fallback: persist new order and broadcast
	const state = getSessionState();
	if (state) {
		state.playlist = state.playlist || { queue: [] };
		state.playlist.queue = newOrder;
		sessionStorage.setItem(getSessionKey(currentSessionId), JSON.stringify(state));
		broadcastStateUpdate('playlist-updated', state.playlist);
	}
	return false;
}

export function saveLibraryToSessionStorage(sessionId, libraryData) {
	try {
		if (!sessionId) throw new Error('sessionId is required');
		const sessionState = getSessionStateForSession(sessionId);
		sessionState.library = libraryData;
		sessionStorage.setItem(getSessionKey(sessionId), JSON.stringify(sessionState));
		console.log('Library saved to sessionStorage for session', sessionId, ':', libraryData.songs?.length || 0, 'songs');
	} catch (error) {
		console.error('Failed to save library to sessionStorage:', error);
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

export function generateSessionUrl(path, sessionId) {
	const url = new URL(path, window.location.origin);
	url.searchParams.set('session', sessionId);
	return url.toString();
}

export function getSessionIdFromUrl() {
	const params = new URLSearchParams(window.location.search);
	return params.get('session');
}

export function setupStateSyncListener(dotNetRef) {
	const handler = (event) => {
		if (event.type === 'session-state-synced') {
			dotNetRef.invokeMethodAsync('OnStateSynced');
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
