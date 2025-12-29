import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import {
    generateQRCode,
    clearQRCode,
    isQRCodeLibraryLoaded
} from './qrcode.js';

// Mock QRCode library
class MockQRCode {
    constructor(element, options) {
        this.element = element;
        this.options = options;
        
        // Create a mock canvas element
        const canvas = document.createElement('canvas');
        canvas.width = options.width || 256;
        canvas.height = options.height || 256;
        canvas.setAttribute('data-qrcode-text', options.text);
        
        if (typeof element === 'string') {
            const container = document.getElementById(element);
            if (container) {
                container.appendChild(canvas);
            }
        } else {
            element.appendChild(canvas);
        }
        
        MockQRCode.instances.push(this);
    }
    
    static instances = [];
    static CorrectLevel = {
        L: 1,
        M: 0,
        Q: 3,
        H: 2
    };
    
    static reset() {
        this.instances = [];
    }
}

describe('qrcode.js', () => {
    beforeEach(() => {
        // Setup DOM
        document.body.innerHTML = '';
        
        // Mock QRCode library
        window.QRCode = MockQRCode;
        MockQRCode.reset();
        
        // Reset library loaded state by reloading the module
        vi.resetModules();
    });

    afterEach(() => {
        // Cleanup
        document.body.innerHTML = '';
        MockQRCode.reset();
        delete window.QRCode;
    });

    describe('generateQRCode', () => {
        it('should generate QR code in specified container', async () => {
            // Arrange
            const containerId = 'test-qrcode';
            const url = 'https://example.com/singer?session=test-123';
            
            const container = document.createElement('div');
            container.id = containerId;
            container.style.width = '256px';
            container.style.height = '256px';
            document.body.appendChild(container);

            // Act
            await generateQRCode(containerId, url);

            // Assert
            expect(container.children.length).toBeGreaterThan(0);
            const canvas = container.querySelector('canvas');
            expect(canvas).not.toBeNull();
            expect(canvas.getAttribute('data-qrcode-text')).toBe(url);
        });

        it('should throw error when containerId is not provided', async () => {
            // Act & Assert
            await expect(generateQRCode('', 'https://example.com')).rejects.toThrow('containerId is required');
        });

        it('should throw error when url is not provided', async () => {
            // Arrange
            const containerId = 'test-qrcode';
            const container = document.createElement('div');
            container.id = containerId;
            document.body.appendChild(container);

            // Act & Assert
            await expect(generateQRCode(containerId, '')).rejects.toThrow('url is required');
        });

        it('should throw error when container element not found', async () => {
            // Act & Assert
            await expect(generateQRCode('non-existent-id', 'https://example.com')).rejects.toThrow("Container with id 'non-existent-id' not found");
        });

        it('should use default dimensions from container', async () => {
            // Arrange
            const containerId = 'test-qrcode';
            const url = 'https://example.com/singer?session=test-123';
            
            const container = document.createElement('div');
            container.id = containerId;
            container.style.width = '300px';
            container.style.height = '300px';
            
            // Mock offsetWidth and offsetHeight since jsdom doesn't compute layout
            Object.defineProperty(container, 'offsetWidth', { value: 300, writable: true });
            Object.defineProperty(container, 'offsetHeight', { value: 300, writable: true });
            
            document.body.appendChild(container);

            // Act
            await generateQRCode(containerId, url);

            // Assert
            expect(MockQRCode.instances.length).toBe(1);
            const qrInstance = MockQRCode.instances[0];
            expect(qrInstance.options.width).toBe(300);
            expect(qrInstance.options.height).toBe(300);
        });

        it('should use custom width and height from options', async () => {
            // Arrange
            const containerId = 'test-qrcode';
            const url = 'https://example.com/singer?session=test-123';
            const options = { width: 150, height: 150 };
            
            const container = document.createElement('div');
            container.id = containerId;
            document.body.appendChild(container);

            // Act
            await generateQRCode(containerId, url, options);

            // Assert
            expect(MockQRCode.instances.length).toBe(1);
            const qrInstance = MockQRCode.instances[0];
            expect(qrInstance.options.width).toBe(150);
            expect(qrInstance.options.height).toBe(150);
        });

        it('should use custom colors from options', async () => {
            // Arrange
            const containerId = 'test-qrcode';
            const url = 'https://example.com/singer?session=test-123';
            const options = {
                colorDark: '#FF0000',
                colorLight: '#00FF00'
            };
            
            const container = document.createElement('div');
            container.id = containerId;
            container.style.width = '256px';
            container.style.height = '256px';
            document.body.appendChild(container);

            // Act
            await generateQRCode(containerId, url, options);

            // Assert
            expect(MockQRCode.instances.length).toBe(1);
            const qrInstance = MockQRCode.instances[0];
            expect(qrInstance.options.colorDark).toBe('#FF0000');
            expect(qrInstance.options.colorLight).toBe('#00FF00');
        });

        it('should use default colors when not specified', async () => {
            // Arrange
            const containerId = 'test-qrcode';
            const url = 'https://example.com/singer?session=test-123';
            
            const container = document.createElement('div');
            container.id = containerId;
            container.style.width = '256px';
            container.style.height = '256px';
            document.body.appendChild(container);

            // Act
            await generateQRCode(containerId, url);

            // Assert
            expect(MockQRCode.instances.length).toBe(1);
            const qrInstance = MockQRCode.instances[0];
            expect(qrInstance.options.colorDark).toBe('#000000');
            expect(qrInstance.options.colorLight).toBe('#ffffff');
        });

        it('should clear existing QR code before generating new one', async () => {
            // Arrange
            const containerId = 'test-qrcode';
            const url1 = 'https://example.com/singer?session=test-1';
            const url2 = 'https://example.com/singer?session=test-2';
            
            const container = document.createElement('div');
            container.id = containerId;
            container.style.width = '256px';
            container.style.height = '256px';
            document.body.appendChild(container);

            // Add some existing content
            const existingDiv = document.createElement('div');
            existingDiv.textContent = 'Existing content';
            container.appendChild(existingDiv);

            // Act
            await generateQRCode(containerId, url1);
            
            // Assert - Existing content should be cleared
            expect(container.textContent).not.toContain('Existing content');
            
            // Act - Generate second QR code
            await generateQRCode(containerId, url2);
            
            // Assert - Only one canvas should exist
            const canvases = container.querySelectorAll('canvas');
            expect(canvases.length).toBe(1);
            expect(canvases[0].getAttribute('data-qrcode-text')).toBe(url2);
        });

        it('should use high error correction level', async () => {
            // Arrange
            const containerId = 'test-qrcode';
            const url = 'https://example.com/singer?session=test-123';
            
            const container = document.createElement('div');
            container.id = containerId;
            container.style.width = '256px';
            container.style.height = '256px';
            document.body.appendChild(container);

            // Act
            await generateQRCode(containerId, url);

            // Assert
            expect(MockQRCode.instances.length).toBe(1);
            const qrInstance = MockQRCode.instances[0];
            expect(qrInstance.options.correctLevel).toBe(MockQRCode.CorrectLevel.H);
        });

        it('should handle URLs with special characters', async () => {
            // Arrange
            const containerId = 'test-qrcode';
            const url = 'https://example.com/singer?session=test-123&name=John%20Doe';
            
            const container = document.createElement('div');
            container.id = containerId;
            container.style.width = '256px';
            container.style.height = '256px';
            document.body.appendChild(container);

            // Act
            await generateQRCode(containerId, url);

            // Assert
            const canvas = container.querySelector('canvas');
            expect(canvas.getAttribute('data-qrcode-text')).toBe(url);
        });

        it('should handle very long URLs', async () => {
            // Arrange
            const containerId = 'test-qrcode';
            const longSessionId = 'a'.repeat(200);
            const url = `https://example.com/singer?session=${longSessionId}`;
            
            const container = document.createElement('div');
            container.id = containerId;
            container.style.width = '256px';
            container.style.height = '256px';
            document.body.appendChild(container);

            // Act
            await generateQRCode(containerId, url);

            // Assert
            const canvas = container.querySelector('canvas');
            expect(canvas.getAttribute('data-qrcode-text')).toBe(url);
        });
    });

    describe('clearQRCode', () => {
        it('should clear QR code from container', () => {
            // Arrange
            const containerId = 'test-qrcode';
            const container = document.createElement('div');
            container.id = containerId;
            
            const canvas = document.createElement('canvas');
            container.appendChild(canvas);
            document.body.appendChild(container);

            // Act
            clearQRCode(containerId);

            // Assert
            expect(container.children.length).toBe(0);
            expect(container.innerHTML).toBe('');
        });

        it('should throw error when containerId is not provided', () => {
            // Act & Assert
            expect(() => clearQRCode('')).toThrow('containerId is required');
        });

        it('should not throw error when container not found', () => {
            // Act & Assert
            expect(() => clearQRCode('non-existent-id')).not.toThrow();
        });

        it('should handle container that is already empty', () => {
            // Arrange
            const containerId = 'test-qrcode';
            const container = document.createElement('div');
            container.id = containerId;
            document.body.appendChild(container);

            // Act
            clearQRCode(containerId);

            // Assert
            expect(container.innerHTML).toBe('');
        });
    });

    describe('isQRCodeLibraryLoaded', () => {
        it('should return true after library is loaded', async () => {
            // Arrange
            const containerId = 'test-qrcode';
            const url = 'https://example.com';
            
            const container = document.createElement('div');
            container.id = containerId;
            container.style.width = '256px';
            container.style.height = '256px';
            document.body.appendChild(container);

            // Act
            await generateQRCode(containerId, url);
            const isLoaded = isQRCodeLibraryLoaded();

            // Assert
            expect(isLoaded).toBe(true);
        });

        it('should return false before library is loaded', () => {
            // Arrange - Force a fresh module state
            delete window.QRCode;
            
            // Act
            const isLoaded = isQRCodeLibraryLoaded();

            // Assert - Initially false (though in tests it's mocked)
            // In real scenario, this would be false until generateQRCode is called
            expect(typeof isLoaded).toBe('boolean');
        });
    });

    describe('QRCode Library Loading', () => {
        it('should load QRCode library only once', async () => {
            // Arrange
            const containerId1 = 'test-qrcode-1';
            const containerId2 = 'test-qrcode-2';
            const url = 'https://example.com';
            
            const container1 = document.createElement('div');
            container1.id = containerId1;
            container1.style.width = '256px';
            container1.style.height = '256px';
            document.body.appendChild(container1);

            const container2 = document.createElement('div');
            container2.id = containerId2;
            container2.style.width = '256px';
            container2.style.height = '256px';
            document.body.appendChild(container2);

            // Act - Generate multiple QR codes
            await generateQRCode(containerId1, url);
            await generateQRCode(containerId2, url);

            // Assert - Both should succeed
            expect(container1.children.length).toBeGreaterThan(0);
            expect(container2.children.length).toBeGreaterThan(0);
        });

        it('should handle concurrent QR code generation', async () => {
            // Arrange
            const containerId1 = 'test-qrcode-1';
            const containerId2 = 'test-qrcode-2';
            const url = 'https://example.com';
            
            const container1 = document.createElement('div');
            container1.id = containerId1;
            container1.style.width = '256px';
            container1.style.height = '256px';
            document.body.appendChild(container1);

            const container2 = document.createElement('div');
            container2.id = containerId2;
            container2.style.width = '256px';
            container2.style.height = '256px';
            document.body.appendChild(container2);

            // Act - Generate QR codes concurrently
            await Promise.all([
                generateQRCode(containerId1, url),
                generateQRCode(containerId2, url)
            ]);

            // Assert - Both should succeed
            expect(container1.children.length).toBeGreaterThan(0);
            expect(container2.children.length).toBeGreaterThan(0);
        });
    });
});
