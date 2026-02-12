import { describe, it, expect, beforeEach, vi } from 'vitest';
import { LogLevel, createLogger } from './logger.js';

describe('logger', () => {
    let consoleLogSpy, consoleWarnSpy, consoleErrorSpy;
    let mockAppInsights;
    let storage;

    beforeEach(() => {
        // Reset window globals
        delete window.logLevel;
        delete window.appInsights;
        delete window.karamel;
        
        // Mock location
        Object.defineProperty(window, 'location', {
            value: { hostname: 'localhost' },
            writable: true,
            configurable: true
        });

        // Mock sessionStorage with proper implementation
        storage = {};
        global.sessionStorage = {
            getItem: (key) => storage[key] || null,
            setItem: (key, value) => { storage[key] = value; },
            removeItem: (key) => { delete storage[key]; },
            clear: () => { Object.keys(storage).forEach(k => delete storage[k]); },
            get length() { return Object.keys(storage).length; },
            key: (index) => Object.keys(storage)[index] || null
        };

        // Spy on console methods and clear any existing calls
        if (consoleLogSpy) {
            consoleLogSpy.mockClear();
        } else {
            consoleLogSpy = vi.spyOn(console, 'log').mockImplementation(() => {});
        }
        
        if (consoleWarnSpy) {
            consoleWarnSpy.mockClear();
        } else {
            consoleWarnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
        }
        
        if (consoleErrorSpy) {
            consoleErrorSpy.mockClear();
        } else {
            consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
        }

        // Mock Application Insights
        mockAppInsights = {
            trackEvent: vi.fn(),
            trackException: vi.fn()
        };
    });

    describe('LogLevel enum', () => {
        it('should have correct numeric values', () => {
            expect(LogLevel.Debug).toBe(0);
            expect(LogLevel.Info).toBe(1);
            expect(LogLevel.Warn).toBe(2);
            expect(LogLevel.Error).toBe(3);
        });
    });

    describe('createLogger', () => {
        it('should create logger with module name', () => {
            const logger = createLogger('TestModule');
            expect(logger).toBeDefined();
            expect(logger.debug).toBeInstanceOf(Function);
            expect(logger.info).toBeInstanceOf(Function);
            expect(logger.warn).toBeInstanceOf(Function);
            expect(logger.error).toBeInstanceOf(Function);
        });

        describe('debug logging', () => {
            it('should log debug messages in development mode', () => {
                window.logLevel = LogLevel.Debug;
                const logger = createLogger('TestModule');
                
                logger.debug('Test debug message', { key: 'value' });
                
                expect(consoleLogSpy).toHaveBeenCalledTimes(1);
                const [message, properties] = consoleLogSpy.mock.calls[0];
                expect(message).toContain('[TestModule]');
                expect(message).toContain('[DEBUG]');
                expect(message).toContain('Test debug message');
                expect(properties).toHaveProperty('moduleName', 'TestModule');
                expect(properties).toHaveProperty('timestamp');
                expect(properties).toHaveProperty('key', 'value');
            });

            it('should NOT log debug messages when log level is Info', () => {
                window.logLevel = LogLevel.Info;
                const logger = createLogger('TestModule');
                
                logger.debug('Test debug message');
                
                expect(consoleLogSpy).not.toHaveBeenCalled();
            });

            it('should NOT log debug messages when log level is Warn', () => {
                window.logLevel = LogLevel.Warn;
                const logger = createLogger('TestModule');
                
                logger.debug('Test debug message');
                
                expect(consoleLogSpy).not.toHaveBeenCalled();
            });

            it('should NOT send debug to Application Insights', () => {
                window.appInsights = mockAppInsights;
                window.logLevel = LogLevel.Debug;
                const logger = createLogger('TestModule');
                
                logger.debug('Test debug message');
                
                expect(mockAppInsights.trackEvent).not.toHaveBeenCalled();
                expect(mockAppInsights.trackException).not.toHaveBeenCalled();
            });
        });

        describe('info logging', () => {
            it('should log info messages when log level allows', () => {
                window.logLevel = LogLevel.Info;
                const logger = createLogger('TestModule');
                
                logger.info('Test info message', { userId: '123' });
                
                expect(consoleLogSpy).toHaveBeenCalledTimes(1);
                const [message, properties] = consoleLogSpy.mock.calls[0];
                expect(message).toContain('[TestModule]');
                expect(message).toContain('[INFO]');
                expect(message).toContain('Test info message');
                expect(properties).toHaveProperty('userId', '123');
            });

            it('should NOT log info messages when log level is Warn', () => {
                window.logLevel = LogLevel.Warn;
                const logger = createLogger('TestModule');
                
                logger.info('Test info message');
                
                expect(consoleLogSpy).not.toHaveBeenCalled();
            });

            it('should track info as custom event in Application Insights', () => {
                window.appInsights = mockAppInsights;
                window.karamel = { telemetryStarted: true }; // User has given consent
                window.logLevel = LogLevel.Info;
                const logger = createLogger('TestModule');
                
                logger.info('Test info message', { operation: 'test' });
                
                expect(mockAppInsights.trackEvent).toHaveBeenCalledTimes(1);
                const call = mockAppInsights.trackEvent.mock.calls[0][0];
                expect(call.name).toBe('TestModule.Info');
                expect(call.properties).toHaveProperty('moduleName', 'TestModule');
                expect(call.properties).toHaveProperty('message', 'Test info message');
                expect(call.properties).toHaveProperty('operation', 'test');
            });

            it('should track info even when console log level blocks it', () => {
                window.appInsights = mockAppInsights;
                window.karamel = { telemetryStarted: true }; // User has given consent
                window.logLevel = LogLevel.Warn; // Blocks console output
                const logger = createLogger('TestModule');
                
                logger.info('Test info message');
                
                expect(consoleLogSpy).not.toHaveBeenCalled();
                expect(mockAppInsights.trackEvent).toHaveBeenCalledTimes(1);
            });
        });

        describe('warn logging', () => {
            it('should log warn messages when log level allows', () => {
                window.logLevel = LogLevel.Warn;
                const logger = createLogger('TestModule');
                
                logger.warn('Test warning', { reason: 'fallback' });
                
                expect(consoleWarnSpy).toHaveBeenCalledTimes(1);
                const [message, properties] = consoleWarnSpy.mock.calls[0];
                expect(message).toContain('[TestModule]');
                expect(message).toContain('[WARN]');
                expect(message).toContain('Test warning');
                expect(properties).toHaveProperty('reason', 'fallback');
            });

            it('should track warn as custom event in Application Insights', () => {
                window.appInsights = mockAppInsights;
                window.karamel = { telemetryStarted: true }; // User has given consent
                window.logLevel = LogLevel.Warn;
                const logger = createLogger('TestModule');
                
                logger.warn('Test warning', { scenario: 'edge-case' });
                
                expect(mockAppInsights.trackEvent).toHaveBeenCalledTimes(1);
                const call = mockAppInsights.trackEvent.mock.calls[0][0];
                expect(call.name).toBe('TestModule.Warning');
                expect(call.properties).toHaveProperty('message', 'Test warning');
                expect(call.properties).toHaveProperty('scenario', 'edge-case');
            });
        });

        describe('error logging', () => {
            it('should log error messages with Error object', () => {
                window.logLevel = LogLevel.Error;
                const logger = createLogger('TestModule');
                const testError = new Error('Test error');
                
                logger.error('Operation failed', testError, { operation: 'loadFile' });
                
                expect(consoleErrorSpy).toHaveBeenCalledTimes(1);
                const [message, errorObj, properties] = consoleErrorSpy.mock.calls[0];
                expect(message).toContain('[TestModule]');
                expect(message).toContain('[ERROR]');
                expect(message).toContain('Operation failed');
                expect(errorObj).toBe(testError);
                expect(properties).toHaveProperty('operation', 'loadFile');
            });

            it('should log error messages without Error object', () => {
                window.logLevel = LogLevel.Error;
                const logger = createLogger('TestModule');
                
                logger.error('Something went wrong', null, { code: 500 });
                
                expect(consoleErrorSpy).toHaveBeenCalledTimes(1);
                const [message, properties] = consoleErrorSpy.mock.calls[0];
                expect(message).toContain('Something went wrong');
                expect(properties).toHaveProperty('code', 500);
            });

            it('should track error with Error object in Application Insights', () => {
                window.appInsights = mockAppInsights;
                window.karamel = { telemetryStarted: true }; // User has given consent
                window.logLevel = LogLevel.Error;
                const logger = createLogger('TestModule');
                const testError = new Error('Test error');
                
                logger.error('Operation failed', testError, { operation: 'loadFile' });
                
                expect(mockAppInsights.trackException).toHaveBeenCalledTimes(1);
                const call = mockAppInsights.trackException.mock.calls[0][0];
                expect(call.exception).toBe(testError);
                expect(call.properties).toHaveProperty('message', 'Operation failed');
                expect(call.properties).toHaveProperty('operation', 'loadFile');
            });

            it('should create Error object if none provided for Application Insights', () => {
                window.appInsights = mockAppInsights;
                window.karamel = { telemetryStarted: true }; // User has given consent
                window.logLevel = LogLevel.Error;
                const logger = createLogger('TestModule');
                
                logger.error('Something went wrong', null, { code: 500 });
                
                expect(mockAppInsights.trackException).toHaveBeenCalledTimes(1);
                const call = mockAppInsights.trackException.mock.calls[0][0];
                expect(call.exception).toBeInstanceOf(Error);
                expect(call.exception.message).toBe('Something went wrong');
                expect(call.properties).toHaveProperty('code', 500);
            });
        });

        describe('session ID integration', () => {
            it('should include session ID in properties when available', () => {
                sessionStorage.setItem('karamel-session-abc123', 'some-data');
                window.logLevel = LogLevel.Debug;
                const logger = createLogger('TestModule');
                
                logger.debug('Test message');
                
                const [, properties] = consoleLogSpy.mock.calls[0];
                expect(properties).toHaveProperty('sessionId', 'abc123');
            });

            it('should NOT include session ID when not in sessionStorage', () => {
                window.logLevel = LogLevel.Debug;
                const logger = createLogger('TestModule');
                
                logger.debug('Test message');
                
                const [, properties] = consoleLogSpy.mock.calls[0];
                expect(properties).not.toHaveProperty('sessionId');
            });
        });

        describe('log level defaults', () => {
            it('should default to Debug when hostname is localhost', () => {
                window.location.hostname = 'localhost';
                const logger = createLogger('TestModule');
                
                logger.debug('Test message');
                
                expect(consoleLogSpy).toHaveBeenCalledTimes(1);
            });

            it('should default to Debug when hostname is 127.0.0.1', () => {
                window.location.hostname = '127.0.0.1';
                const logger = createLogger('TestModule');
                
                logger.debug('Test message');
                
                expect(consoleLogSpy).toHaveBeenCalledTimes(1);
            });

            it('should default to Warn when hostname is production domain', () => {
                window.location.hostname = 'rg-karamel-prod.azurewebsites.net';
                const logger = createLogger('TestModule');
                
                logger.debug('Test message');
                
                expect(consoleLogSpy).not.toHaveBeenCalled();
            });

            it('should use window.logLevel even in development if explicitly set', () => {
                window.location.hostname = 'localhost';
                window.logLevel = LogLevel.Error;
                const logger = createLogger('TestModule');
                
                logger.debug('Test message');
                logger.warn('Warning message');
                
                expect(consoleLogSpy).not.toHaveBeenCalled();
                expect(consoleWarnSpy).not.toHaveBeenCalled();
            });
        });

        describe('message formatting', () => {
            it('should include timestamp in ISO format', () => {
                window.logLevel = LogLevel.Debug;
                const logger = createLogger('TestModule');
                
                logger.debug('Test message');
                
                const [message] = consoleLogSpy.mock.calls[0];
                // Check for ISO timestamp pattern
                expect(message).toMatch(/\[\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z\]/);
            });

            it('should include module name in brackets', () => {
                window.logLevel = LogLevel.Debug;
                const logger = createLogger('SignalRBridge');
                
                logger.debug('Test message');
                
                const [message] = consoleLogSpy.mock.calls[0];
                expect(message).toContain('[SignalRBridge]');
            });
        });
    });
});
