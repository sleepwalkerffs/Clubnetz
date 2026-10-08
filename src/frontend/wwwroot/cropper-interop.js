// Self-contained Cropper.js interop module.
// Dynamically injects cropper.min.js (and its CSS) if they are not already on the
// page, so this module works correctly even when index.html is served from the
// browser cache without those tags.

let _cropperInstance = null;

// Single shared promise so concurrent initCropper calls never double-load the script.
let _loadPromise = null;

function ensureCropperCssLoaded() {
    if (document.querySelector('link[href*="cropper.min.css"]')) return;
    const link = document.createElement('link');
    link.rel = 'stylesheet';
    link.href = '/lib/cropper/cropper.min.css';
    document.head.appendChild(link);
}

function ensureCropperScriptLoaded() {
    if (typeof window.Cropper === 'function') return Promise.resolve();
    if (_loadPromise) return _loadPromise;

    _loadPromise = new Promise((resolve, reject) => {
        // Script tag may already exist but not yet finished loading
        const existing = document.querySelector('script[src*="cropper.min.js"]');
        if (existing) {
            existing.addEventListener('load', resolve, { once: true });
            existing.addEventListener('error', () => reject(new Error('cropper.min.js failed to load')), { once: true });
            return;
        }
        const script = document.createElement('script');
        script.src = '/lib/cropper/cropper.min.js';
        script.onload = resolve;
        script.onerror = () => reject(new Error('cropper.min.js failed to load'));
        document.head.appendChild(script);
    });

    return _loadPromise;
}

export async function initCropper(imgElementId) {
    ensureCropperCssLoaded();
    await ensureCropperScriptLoaded();

    const img = document.getElementById(imgElementId);
    if (!img) {
        console.error('[cropper-interop] Element not found:', imgElementId);
        return;
    }

    if (_cropperInstance) {
        _cropperInstance.destroy();
        _cropperInstance = null;
    }

    // Wait for the image to decode so Cropper.js gets correct dimensions.
    if (!img.complete) {
        await new Promise(resolve => img.addEventListener('load', resolve, { once: true }));
    }

    _cropperInstance = new window.Cropper(img, {
        aspectRatio: 1,
        viewMode: 1,
        dragMode: 'move',
        autoCropArea: 0.8,
        guides: true,
        center: true,
        highlight: false,
        cropBoxMovable: true,
        cropBoxResizable: true,
        toggleDragModeOnDblclick: false
    });
}

export function getCroppedDataUrl(width, height, quality) {
    if (!_cropperInstance) return null;
    return _cropperInstance
        .getCroppedCanvas({ width, height })
        .toDataURL('image/jpeg', quality);
}

export function destroyCropper() {
    if (_cropperInstance) {
        _cropperInstance.destroy();
        _cropperInstance = null;
    }
}
