// Camera barcode scanning: native BarcodeDetector when available, ZXing fallback (e.g. iOS Safari).
const FORMATS = ['ean_13', 'ean_8', 'upc_a', 'upc_e', 'itf'];
let session = null;

export async function start(video, dotnetRef) {
    stop();
    if (!navigator.mediaDevices?.getUserMedia) {
        throw new Error('Camera access is not available. Use HTTPS and a browser with camera support.');
    }

    const stream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: { ideal: 'environment' } },
        audio: false
    });
    const s = { stream, video, active: true, controls: null };
    session = s;
    video.srcObject = stream;
    video.setAttribute('playsinline', '');
    await video.play();

    const report = async (code) => {
        if (!s.active) return;
        s.active = false;
        stop();
        await dotnetRef.invokeMethodAsync('OnDetected', code);
    };

    if ('BarcodeDetector' in window) {
        const supported = await window.BarcodeDetector.getSupportedFormats();
        const formats = FORMATS.filter(f => supported.includes(f));
        if (formats.length > 0) {
            const detector = new window.BarcodeDetector({ formats });
            const loop = async () => {
                if (!s.active) return;
                try {
                    const codes = await detector.detect(video);
                    if (codes.length > 0) {
                        await report(codes[0].rawValue);
                        return;
                    }
                } catch { /* frame not ready */ }
                setTimeout(loop, 150);
            };
            loop();
            return;
        }
    }

    const { BrowserMultiFormatReader } = await import('https://cdn.jsdelivr.net/npm/@zxing/browser@0.1.5/+esm');
    if (!s.active) return;
    const reader = new BrowserMultiFormatReader();
    s.controls = await reader.decodeFromVideoElement(video, (result) => {
        if (result) report(result.getText());
    });
}

export function stop() {
    const s = session;
    if (!s) return;
    session = null;
    s.active = false;
    try { s.controls?.stop(); } catch { /* ignore */ }
    s.stream?.getTracks().forEach(t => t.stop());
    if (s.video) s.video.srcObject = null;
}
