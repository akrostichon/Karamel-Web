// signalRBridge.js
// Real SignalR client with graceful fallback to BroadcastChannel + sessionStorage.
// Exposes the same API used by `SessionService.cs` so Blazor interop keeps working.

import { createLogger } from './logger.js';

const logger = createLogger('SignalRBridge');

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
		script.src = '/lib/signalr/signalr.min.js';
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

		// Configure timeouts to match backend settings in Program.cs (ClientTimeoutInterval: 60s, KeepAliveInterval: 15s)
		// These must be aligned to prevent premature client disconnects during long operations (e.g., large playlist updates)
		hubConnection = new signalR.HubConnectionBuilder()
			.withUrl(hubUrl, urlOptions)
			.withAutomaticReconnect()
			.withServerTimeout(60000)      // Must match backend ClientTimeoutInterval (60 seconds)
			.withKeepAliveInterval(15000)  // Must match backend KeepAliveInterval (15 seconds)
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
				logger.warn('Error handling ReceivePlaylistUpdated', { error: e.message, sessionId });
			}
		});

		// Wire session lifecycle events
		hubConnection.on('ReceiveSessionPaused', () => {
			logger.debug('Received ReceiveSessionPaused', { sessionId });
			const event = new CustomEvent('session-state-updated', { detail: { type: 'session-paused', data: {} } });
			window.dispatchEvent(event);
		});

		hubConnection.on('ReceiveSessionResumed', () => {
			logger.debug('Received ReceiveSessionResumed', { sessionId });
			const event = new CustomEvent('session-state-updated', { detail: { type: 'session-resumed', data: {} } });
			window.dispatchEvent(event);
		});

		hubConnection.on('ReceiveConfigUpdated', async (config) => {
			logger.info('Received ReceiveConfigUpdated', { sessionId, theme: config?.theme ?? 'null' });
			// Apply theme immediately in JS so all connected tabs update without waiting for Blazor
			if (config && config.theme) {
				try {
					const themeModule = await import('./themeToggle.js');
					themeModule.setTheme(config.theme);
				} catch (e) {
					logger.warn('Failed to apply theme from config update', { error: e.message });
				}
			}
			// Re-broadcast via BroadcastChannel so same-device tabs (e.g. the main tab running
			// NextSongView) receive the config update even if their own SignalR connection failed.
			if (broadcastChannel) {
				try {
					broadcastChannel.postMessage({ type: 'config-updated', data: config || {}, timestamp: Date.now(), senderId: tabId });
				} catch (e) {
					logger.warn('Failed to re-broadcast config update via BroadcastChannel', { error: e.message });
				}
			}
			const event = new CustomEvent('session-state-updated', { detail: { type: 'config-updated', data: config || {} } });
			window.dispatchEvent(event);
		});

		await hubConnection.start();
		// Join session group
		await hubConnection.invoke('JoinSession', sessionId);
		usingSignalR = true;
		return true;
	} catch (e) {
		logger.warn('SignalR connection failed, falling back to BroadcastChannel', { error: e.message, sessionId });
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

	logger.debug('initializeSession called', { 
		sessionId, 
		asMainTab, 
		hasLinkToken: !!linkToken, 
		backendUrl 
	});

	currentSessionId = sessionId;
	isMainTab = !!asMainTab;
	backendBaseUrl = backendUrl || null;
	currentLinkToken = linkToken || null;

	logger.debug('Stored currentLinkToken', { hasToken: !!currentLinkToken, sessionId });
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
					logger.error('Error handling broadcast message on main tab', e, { sessionId });
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

	logger.info('Session bridge initialized', { 
		role: isMainTab ? 'MAIN' : 'SECONDARY', 
		sessionId, 
		usingSignalR 
	});
}

function handleBroadcastMessage(message) {
	try {
		saveToSessionStorage(message.type, message.data);
		const event = new CustomEvent('session-state-updated', { detail: message });
		window.dispatchEvent(event);

		// If session-settings include a theme, apply it on this tab
		if (message && message.type === 'session-settings' && message.data && message.data.theme) {
			import('./themeToggle.js').then(module => {
				try {
					module.setTheme(message.data.theme);
					logger.debug('Applied theme from session-settings', { theme: message.data.theme });
				} catch (e) {
					logger.warn('Failed to apply theme from session-settings', { error: e.message });
				}
			}).catch(e => {
				logger.warn('Error while attempting to apply theme from broadcast', { error: e.message });
			});
		}

		// If config-updated includes a theme, apply it on this tab (handles main-tab
		// re-broadcast from a secondary tab that received ReceiveConfigUpdated via SignalR)
		if (message && message.type === 'config-updated' && message.data && message.data.theme) {
			import('./themeToggle.js').then(module => {
				try {
					module.setTheme(message.data.theme);
					logger.info('Applied theme from config-updated broadcast', { theme: message.data.theme });
				} catch (e) {
					logger.warn('Failed to apply theme from config-updated broadcast', { error: e.message });
				}
			}).catch(e => {
				logger.warn('Error while attempting to apply theme from config-updated broadcast', { error: e.message });
			});
		}
	} catch (e) {
		logger.warn('Error in handleBroadcastMessage', { error: e.message, messageType: message?.type });
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
			case 'config-updated':
			case 'session-paused':
			case 'session-resumed':
				// These are ephemeral events; no session-storage persistence needed.
				return;
			default:
				logger.warn('Unknown state type', { type });
				return;
	}
	sessionStorage.setItem(getSessionKey(currentSessionId), JSON.stringify(sessionState));
	} catch (e) {
		logger.error('Failed to save to sessionStorage', e, { type });
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
		logger.error('addItemToPlaylist: No current session ID', null, { operation: 'addItemToPlaylist', songId });
		return false;
	}

	if (usingSignalR && hubConnection) {
		try {
			// PlaylistHub.AddItemAsync(Guid sessionId, Guid songId, string? singerName)
			// One playlist per session - no playlistId parameter needed
			await hubConnection.invoke('AddItemAsync', currentSessionId, songId, singerName || null);
			return true;
		} catch (e) {
			logger.warn('AddItemAsync via SignalR failed, falling back to local broadcast', { 
				error: e.message, 
				sessionId: currentSessionId, 
				songId, 
				singerName 
			});
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
		logger.error('removeItemFromPlaylist: No current session ID', null, { operation: 'removeItemFromPlaylist', itemId });
		return false;
	}

	if (usingSignalR && hubConnection) {
		try {
			// PlaylistHub.RemoveItemAsync(Guid sessionId, Guid itemId)
			await hubConnection.invoke('RemoveItemAsync', currentSessionId, itemId);
			return true;
		} catch (e) {
			logger.warn('RemoveItemAsync via SignalR failed, falling back to local broadcast', { 
				error: e.message, 
				sessionId: currentSessionId, 
				itemId 
			});
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
		logger.error('reorderPlaylist: No current session ID', null, { operation: 'reorderPlaylist', from, to });
		return false;
	}

	if (usingSignalR && hubConnection) {
		try {
			// PlaylistHub.ReorderAsync(Guid sessionId, int from, int to)
			await hubConnection.invoke('ReorderAsync', currentSessionId, from, to);
			return true;
		} catch (e) {
			logger.warn('ReorderAsync via SignalR failed, falling back to local broadcast', { 
				error: e.message, 
				sessionId: currentSessionId, 
				from, 
				to 
			});
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
		logger.error('setSongStatus: No current session ID', null, { operation: 'setSongStatus', itemId, status });
		return false;
	}

	if (usingSignalR && hubConnection) {
		try {
			// PlaylistHub.SetSongStatusAsync(Guid sessionId, Guid itemId, int status)
			await hubConnection.invoke('SetSongStatusAsync', currentSessionId, itemId, status);
			return true;
		} catch (e) {
			logger.warn('SetSongStatusAsync via SignalR failed', { 
				error: e.message, 
				sessionId: currentSessionId, 
				itemId, 
				status 
			});
			return false;
		}
	}

	logger.warn('setSongStatus: SignalR not connected, cannot update status', { itemId, status });
	return false;
}

/**
 * Advance to the next song: marks current NowPlaying as Completed, marks first UpNext as NowPlaying.
 * @returns {Promise<boolean>} True if successful via SignalR
 */
export async function advanceToNextSong() {
	if (!currentSessionId) {
		logger.error('advanceToNextSong: No current session ID', null, { operation: 'advanceToNextSong' });
		return false;
	}

	if (usingSignalR && hubConnection) {
		try {
			// PlaylistHub.AdvanceToNextSongAsync(Guid sessionId)
			await hubConnection.invoke('AdvanceToNextSongAsync', currentSessionId);
			return true;
		} catch (e) {
			logger.warn('AdvanceToNextSongAsync via SignalR failed', { 
				error: e.message, 
				sessionId: currentSessionId 
			});
			return false;
		}
	}

	logger.warn('advanceToNextSong: SignalR not connected, cannot advance song', { sessionId: currentSessionId });
	return false;
}

/**
 * Complete the current song without advancing to the next one.
 * Marks current NowPlaying as Completed. Next song remains in queue.
 * @returns {Promise<boolean>} True if successful via SignalR
 */
export async function completeCurrentSong() {
	if (!currentSessionId) {
		logger.error('completeCurrentSong: No current session ID', null, { operation: 'completeCurrentSong' });
		return false;
	}

	if (usingSignalR && hubConnection) {
		try {
			// PlaylistHub.CompleteCurrentSongAsync(Guid sessionId)
			await hubConnection.invoke('CompleteCurrentSongAsync', currentSessionId);
			return true;
		} catch (e) {
			logger.warn('CompleteCurrentSongAsync via SignalR failed', { 
				error: e.message, 
				sessionId: currentSessionId 
			});
			return false;
		}
	}

	logger.warn('completeCurrentSong: SignalR not connected, cannot complete song', { sessionId: currentSessionId });
	return false;
}

/**
 * Clear all queued and up-next songs from the playlist, preserving the currently playing song.
 * @returns {Promise<boolean>} True if successful via SignalR
 */
export async function clearQueue() {
	if (!currentSessionId) {
		logger.error('clearQueue: No current session ID', null, { operation: 'clearQueue' });
		return false;
	}

	if (usingSignalR && hubConnection) {
		try {
			// PlaylistHub.ClearQueueAsync(Guid sessionId)
			await hubConnection.invoke('ClearQueueAsync', currentSessionId);
			return true;
		} catch (e) {
			logger.warn('ClearQueueAsync via SignalR failed', { 
				error: e.message, 
				sessionId: currentSessionId 
			});
			return false;
		}
	}

	logger.warn('clearQueue: SignalR not connected, cannot clear queue', { sessionId: currentSessionId });
	return false;
}

export function isUsingSignalR() {
	return !!usingSignalR && !!hubConnection && hubConnection.state === (window.signalR ? window.signalR.HubConnectionState?.Connected : 1);
}

/**
 * Pause the session by invoking hub PauseSessionAsync.
 * The hub broadcasts ReceiveSessionPaused to all clients.
 * @returns {Promise<boolean>} True if hub invocation succeeded
 */
export async function pauseSession() {
	if (!currentSessionId) {
		logger.error('pauseSession: No current session ID', null, { operation: 'pauseSession' });
		return false;
	}

	if (usingSignalR && hubConnection) {
		try {
			await hubConnection.invoke('PauseSessionAsync', currentSessionId);
			return true;
		} catch (e) {
			logger.warn('PauseSessionAsync via SignalR failed', { error: e.message, sessionId: currentSessionId });
			return false;
		}
	}

	logger.warn('pauseSession: SignalR not connected, cannot pause session', { sessionId: currentSessionId });
	return false;
}

/**
 * Resume the session by invoking hub ResumeSessionAsync.
 * The hub broadcasts ReceiveSessionResumed to all clients.
 * @returns {Promise<boolean>} True if hub invocation succeeded
 */
export async function resumeSession() {
	if (!currentSessionId) {
		logger.error('resumeSession: No current session ID', null, { operation: 'resumeSession' });
		return false;
	}

	if (usingSignalR && hubConnection) {
		try {
			await hubConnection.invoke('ResumeSessionAsync', currentSessionId);
			return true;
		} catch (e) {
			logger.warn('ResumeSessionAsync via SignalR failed', { error: e.message, sessionId: currentSessionId });
			return false;
		}
	}

	logger.warn('resumeSession: SignalR not connected, cannot resume session', { sessionId: currentSessionId });
	return false;
}

/**
 * Send config update to hub UpdateSessionConfigAsync.
 * The hub persists the config and broadcasts ReceiveConfigUpdated to all clients.
 * @param {object} config - Config object with camelCase properties
 * @returns {Promise<boolean>} True if hub invocation succeeded
 */
export async function updateSessionConfig(config) {
	if (!currentSessionId) {
		logger.error('updateSessionConfig: No current session ID', null, { operation: 'updateSessionConfig' });
		return false;
	}

	if (usingSignalR && hubConnection) {
		try {
			await hubConnection.invoke('UpdateSessionConfigAsync', currentSessionId, config);
			return true;
		} catch (e) {
			logger.warn('UpdateSessionConfigAsync via SignalR failed', { error: e.message, sessionId: currentSessionId });
			return false;
		}
	}

	logger.warn('updateSessionConfig: SignalR not connected', { sessionId: currentSessionId });
	return false;
}

export async function fetchLibraryPage(sessionId, page = 1, pageSize = 50, search = null, sort = null) {
	if (!sessionId) throw new Error('sessionId is required');

	const correlationId = Math.random().toString(36).substring(2, 10);
	
	// Enhanced debug logging with correlation ID and structured data
	logger.debug(`[DIAG:${correlationId}] fetchLibraryPage START`, { 
		sessionId, 
		page, 
		pageSize, 
		search: search || 'null', 
		sort: sort || 'null',
		usingSignalR,
		hubConnectionState: hubConnection?.state,
		hasLinkToken: !!currentLinkToken,
		linkTokenLength: currentLinkToken?.length
	});

	// Prefer SignalR RPC when connected
	if (usingSignalR && hubConnection) {
		try {
			logger.debug(`[DIAG:${correlationId}] Attempting SignalR RPC`, { sessionId, operation: 'GetLibraryPage' });
			const startTime = Date.now();
			const res = await hubConnection.invoke('GetLibraryPage', sessionId, page, pageSize, search, sort);
			const duration = Date.now() - startTime;
			logger.debug(`[DIAG:${correlationId}] SignalR RPC SUCCESS`, { 
				duration, 
				itemCount: res.items?.length ?? 0, 
				totalCount: res.totalCount ?? 0 
			});
			return res;
		} catch (e) {
			logger.warn(`[WARN:${correlationId}] SignalR RPC failed, falling back to REST`, { error: e.message });
		}
	} else {
		logger.debug(`[DIAG:${correlationId}] SignalR not available, using REST fallback`, { 
			usingSignalR, 
			hasHubConnection: !!hubConnection 
		});
	}

	// REST fallback
	try {
		const params = new URLSearchParams();
		params.set('page', String(page));
		params.set('pageSize', String(pageSize));
		if (search) params.set('search', search);
		if (sort) params.set('sort', sort);

		const baseUrl = backendBaseUrl || '';
		const url = `${baseUrl}/api/sessions/${sessionId}/library?${params.toString()}`;
		
		const headers = { 'Accept': 'application/json' };
		
		if (currentLinkToken) {
			headers['X-Link-Token'] = currentLinkToken;
			logger.debug(`[DIAG:${correlationId}] REST request with X-Link-Token`, { 
				url, 
				tokenLength: currentLinkToken.length 
			});
		} else {
			// CRITICAL: This will track in Application Insights as an error
			logger.error(`[ERROR:${correlationId}] NO currentLinkToken available - request will likely fail`, { 
				sessionId, 
				url,
				operation: 'fetchLibraryPage'
			});
		}
		
		const startTime = Date.now();
		const resp = await fetch(url, { method: 'GET', headers });
		const duration = Date.now() - startTime;
		
		logger.debug(`[DIAG:${correlationId}] REST response received`, { 
			duration, 
			status: resp.status, 
			statusText: resp.statusText 
		});
		
		if (!resp.ok) {
			const errorBody = await resp.text();
			// CRITICAL: This will track in Application Insights as an error
			logger.error(`[ERROR:${correlationId}] REST request failed`, { 
				status: resp.status, 
				errorBody: errorBody.substring(0, 200), // Truncate long error messages
				sessionId,
				url
			});
			return { items: [], page, pageSize, totalCount: 0 };
		}
		
		const items = await resp.json();
		const total = parseInt(resp.headers.get('X-Total-Count') || '0');
		
		logger.debug(`[DIAG:${correlationId}] fetchLibraryPage SUCCESS`, { 
			itemCount: items.length, 
			totalCount: total,
			sessionId
		});
		
		return { items, page, pageSize, totalCount: total };
	} catch (e) {
		// CRITICAL: This will track in Application Insights as an exception
		logger.error(`[ERROR:${correlationId}] fetchLibraryPage exception`, { 
			error: e.message, 
			stack: e.stack?.substring(0, 500),
			sessionId
		});
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
			logger.warn('searchLibrary via SignalR failed, falling back to REST', { 
				error: e.message, 
				sessionId, 
				query 
			});
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
		logger.warn('searchLibrary REST failed', { error: e.message, sessionId, query });
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
		logger.debug('uploadLibraryToServer token resolution', { 
			hasOptionsToken: !!options.linkToken, 
			hasCurrentToken: !!currentLinkToken, 
			usingToken: !!tokenToUse, 
			sessionId 
		});
		if (tokenToUse) {
			headers['X-Link-Token'] = tokenToUse;
			logger.debug('uploadLibraryToServer: X-Link-Token header set', { sessionId });
		} else {
			logger.warn('uploadLibraryToServer: No link token available - request will likely fail', { sessionId });
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
			const errorText = await resp.text();
			logger.warn('uploadLibraryToServer failed', { 
				status: resp.status, 
				errorText, 
				sessionId, 
				songCount: songs.length 
			});
			return false;
		}
		return true;
	} catch (e) {
		logger.warn('uploadLibraryToServer exception', { error: e.message, sessionId });
		return false;
	}
}

export function getSessionStateForSession(sessionId) {
	try {
		if (!sessionId) throw new Error('sessionId is required');
		const stored = sessionStorage.getItem(getSessionKey(sessionId));
		return stored ? JSON.parse(stored) : { session: null, library: null, playlist: null, currentSong: null };
	} catch (error) {
		logger.error('Failed to read from sessionStorage', error, { sessionId });
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
		logger.info('Session state cleared', { sessionId: currentSessionId });
		currentSessionId = null;
	} catch (error) {
		logger.error('Failed to clear session state', error);
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
			dotNetRef.invokeMethodAsync('HandleBroadcastMessage', event.detail.type, event.detail.data);
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
