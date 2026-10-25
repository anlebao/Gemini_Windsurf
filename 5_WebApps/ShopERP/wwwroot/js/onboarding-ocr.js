/**
 * Onboarding OCR (S1 — 2026-10-25): Client-side Vietnamese document OCR
 * for the Tenant OCR Onboarding wizard (ShopERP /admin/onboarding).
 *
 * Uses Tesseract.js + vie.traineddata (tessdata_fast — already shipped at
 * /js/lib/ocr/vie.traineddata.gz, used by guard-camera.js as 'eng+vie').
 *
 * IMPORTANT: This creates its OWN Tesseract worker — guard-camera.js's worker
 * applies a plate char-whitelist which would destroy document OCR, so the two
 * workers must not be shared.
 *
 * Exposes:
 *  - recognizeDocument(file|canvas)  → { lines:[{text,confidence,bbox}], text }
 *  - extractGpdk(file|canvas)         → { mst, name, address, legalForm, owner,
 *                                         businessType, hkdGroupHint, confidence, warnings }
 *  - extractSignName(file|canvas)     → { value, confidence }
 *  - extractMenu(files)               → { productLines:[{name,price,lineNo,confidence}], warnings }
 *  - fileToCanvas(file)               → HTMLCanvasElement
 *  - unload()                         → terminate worker
 */
window.vananOnboardingOcr = (function () {
    'use strict';

    const OCR_BASE = '/js/lib/ocr';
    let _workerPromise = null;

    // ── Tesseract worker (lazy singleton) ────────────────────────────────

    function _ensureTesseract() {
        if (window.Tesseract) return Promise.resolve();
        return new Promise((resolve, reject) => {
            const existing = document.querySelector('script[src*="tesseract.min.js"]');
            if (existing) { resolve(); return; }
            const script = document.createElement('script');
            script.src = `${OCR_BASE}/tesseract.min.js`;
            script.async = true;
            script.onload = () => resolve();
            script.onerror = () => reject(new Error('Tesseract library failed to load'));
            document.head.appendChild(script);
        });
    }

    async function _getWorker() {
        if (_workerPromise) return _workerPromise;
        _workerPromise = (async () => {
            await _ensureTesseract();
            // 'vie+eng': vie for Vietnamese diacritics, eng strengthens digits.
            // NO char whitelist, PSM 3 (auto page segmentation) — full documents.
            const worker = await Tesseract.createWorker('vie+eng', 1, {
                workerPath: `${OCR_BASE}/worker.min.js`,
                corePath: OCR_BASE,
                langPath: OCR_BASE,
                logger: () => {}
            });
            await worker.setParameters({ tessedit_pageseg_mode: '3' });
            return worker;
        })().catch((err) => {
            _workerPromise = null;
            throw err;
        });
        return _workerPromise;
    }

    // ── Preprocessing: grayscale + contrast + upscale 2x ─────────────────

    function _preprocess(canvas) {
        const maxDim = 2400; // cap memory on large phone photos
        const scale = Math.min(2, maxDim / Math.max(canvas.width, canvas.height));
        const w = Math.max(1, Math.round(canvas.width * scale));
        const h = Math.max(1, Math.round(canvas.height * scale));

        const out = document.createElement('canvas');
        out.width = w;
        out.height = h;
        const ctx = out.getContext('2d', { willReadFrequently: true });
        ctx.drawImage(canvas, 0, 0, w, h);

        const img = ctx.getImageData(0, 0, w, h);
        const d = img.data;
        for (let i = 0; i < d.length; i += 4) {
            const gray = 0.299 * d[i] + 0.587 * d[i + 1] + 0.114 * d[i + 2];
            // Contrast stretch around 128 (+45%), clamp
            const c = Math.min(255, Math.max(0, 128 + (gray - 128) * 1.45));
            d[i] = c;
            d[i + 1] = c;
            d[i + 2] = c;
        }
        ctx.putImageData(img, 0, 0);
        return out;
    }

    // ── Core recognition ─────────────────────────────────────────────────

    async function _recognizeLines(input) {
        const canvas = input instanceof HTMLCanvasElement ? input : await fileToCanvas(input);
        const worker = await _getWorker();
        const prepped = _preprocess(canvas);
        const { data } = await worker.recognize(prepped);
        const lines = (data.lines || [])
            .map((l) => ({
                text: (l.text || '').trim(),
                confidence: typeof l.confidence === 'number' ? l.confidence : 0,
                bbox: l.bbox
                    ? { x0: l.bbox.x0 || 0, y0: l.bbox.y0 || 0, x1: l.bbox.x1 || 0, y1: l.bbox.y1 || 0 }
                    : null
            }))
            .filter((l) => l.text.length > 0);
        return { lines, text: (data.text || '').trim() };
    }

    function fileToCanvas(file) {
        return new Promise((resolve, reject) => {
            const url = URL.createObjectURL(file);
            const img = new Image();
            img.onload = () => {
                const canvas = document.createElement('canvas');
                canvas.width = img.naturalWidth;
                canvas.height = img.naturalHeight;
                canvas.getContext('2d').drawImage(img, 0, 0);
                URL.revokeObjectURL(url);
                resolve(canvas);
            };
            img.onerror = () => {
                URL.revokeObjectURL(url);
                reject(new Error('Failed to decode image'));
            };
            img.src = url;
        });
    }

    // ── GPKD field extraction (Case A) ───────────────────────────────────

    const MST_RE = /\b\d{10}(?:-\d{3})?\b/;
    const LEGAL_FORMS = [
        { re: /hợp\s*tác\s*xã/i, value: 'HTX', businessType: 'Company' },
        { re: /hộ\s*kinh\s*doanh/i, value: 'HKD', businessType: 'HouseholdBusiness' },
        { re: /trách\s*nhiệm\s*hữu\s*hạn|tnhh/i, value: 'TNHH', businessType: 'Company' },
        { re: /cổ\s*phần/i, value: 'Cổ phần', businessType: 'Company' },
        { re: /doanh\s*nghiệp\s*tư\s*nhân|dntn/i, value: 'DNTN', businessType: 'Company' }
    ];

    /** Value after label "Tên doanh nghiệp:" — falls back to the next non-empty line. */
    function _valueAfterLabel(lines, labels, index) {
        const line = lines[index].text;
        const colonIdx = line.indexOf(':');
        if (colonIdx >= 0 && colonIdx < line.length - 1) {
            const v = line.slice(colonIdx + 1).trim();
            if (v.length >= 2) return { value: v, inline: true };
        }
        // Value on next line
        for (let i = index + 1; i < Math.min(index + 3, lines.length); i++) {
            const next = lines[i].text;
            if (!next) continue;
            // Stop if next line is another label (contains colon + known label)
            if (/[:：]/.test(next) && labels.some((l) => next.toLowerCase().includes(l))) break;
            return { value: next, inline: false };
        }
        return { value: null, inline: false };
    }

    async function extractGpdk(input) {
        const { lines } = await _recognizeLines(input);
        const fullText = lines.map((l) => l.text).join('\n');
        const warnings = [];

        const result = {
            mst: null, name: null, address: null, legalForm: null, owner: null,
            businessType: 'Company', hkdGroupHint: null,
            confidence: { mst: 0, name: 0, address: 0, legalForm: 0, owner: 0 },
            warnings
        };

        // ── MST: standalone 10/13-digit line preferred, else first match ──
        let mstLineIdx = -1;
        for (let i = 0; i < lines.length; i++) {
            const m = lines[i].text.match(MST_RE);
            if (m && m[0].replace(/-/g, '').length >= 10) {
                result.mst = m[0];
                result.confidence.mst = lines[i].confidence;
                mstLineIdx = i;
                break;
            }
        }
        if (!result.mst) {
            const m = fullText.match(MST_RE);
            if (m) { result.mst = m[0]; result.confidence.mst = 50; }
            else warnings.push('Không tìm thấy mã số thuế — cần nhập tay.');
        }

        // ── Legal form ──
        const legalHit = lines.find((l) => LEGAL_FORMS.some((f) => f.re.test(l.text)));
        if (legalHit) {
            const hit = LEGAL_FORMS.find((f) => f.re.test(legalHit.text));
            result.legalForm = hit.value;
            result.businessType = hit.businessType;
            result.confidence.legalForm = legalHit.confidence;
            if (hit.value === 'HKD') {
                // HKDGroup không OCR được từ GPKD (phụ thuộc kê khai thuế) — sysadmin chọn trong wizard
                result.hkdGroupHint = null;
            }
        } else {
            warnings.push('Không nhận diện được loại hình pháp lý — cần chọn tay.');
        }

        // ── Name: label "tên doanh nghiệp / tên công ty / tên hộ kinh doanh" ──
        const nameLabels = ['tên doanh nghiệp', 'tên công ty', 'tên hộ kinh doanh', 'tên hộ'];
        const nameIdx = lines.findIndex((l) => nameLabels.some((n) => l.text.toLowerCase().includes(n)));
        if (nameIdx >= 0) {
            const v = _valueAfterLabel(lines, nameLabels, nameIdx);
            result.name = v.value;
            result.confidence.name = lines[nameIdx].confidence;
        }
        if (!result.name) {
            // Fallback: longest non-numeric line (not a label line)
            const longest = [...lines]
                .filter((l) => l.text.length >= 5 && !/^\d+$/.test(l.text))
                .sort((a, b) => b.text.length - a.text.length)[0];
            if (longest) { result.name = longest.text; result.confidence.name = longest.confidence; }
            warnings.push('Tên doanh nghiệp dự đoán từ dòng dài nhất — cần kiểm tra.');
        }

        // ── Address: label "địa chỉ / trụ sở" ──
        const addrLabels = ['địa chỉ', 'trụ sở'];
        const addrIdx = lines.findIndex((l) => addrLabels.some((a) => l.text.toLowerCase().includes(a)));
        if (addrIdx >= 0) {
            const v = _valueAfterLabel(lines, addrLabels, addrIdx);
            result.address = v.value;
            result.confidence.address = lines[addrIdx].confidence;
        }
        if (!result.address) warnings.push('Không tìm thấy địa chỉ — cần nhập tay.');

        // ── Owner: label "người đại diện / chủ hộ / giám đốc" ──
        const ownerLabels = ['người đại diện', 'chủ hộ', 'giám đốc'];
        const ownerIdx = lines.findIndex((l) => ownerLabels.some((o) => l.text.toLowerCase().includes(o)));
        if (ownerIdx >= 0) {
            const v = _valueAfterLabel(lines, ownerLabels, ownerIdx);
            result.owner = v.value;
            result.confidence.owner = lines[ownerIdx].confidence;
        }

        return result;
    }

    // ── Sign name extraction (Case B primary, Case A cross-check) ────────

    async function extractSignName(input) {
        const { lines } = await _recognizeLines(input);
        const candidates = lines
            .map((l) => ({
                text: l.text,
                h: l.bbox ? l.bbox.y1 - l.bbox.y0 : 0,
                y0: l.bbox ? l.bbox.y0 : 0,
                conf: l.confidence
            }))
            .filter((c) => c.text.length >= 4 && /[A-Za-zÀ-ỹ]/u.test(c.text) && c.h > 0);

        if (!candidates.length) return { value: null, confidence: 0 };

        // Largest font line = sign text (shop signs are big). Concatenate
        // same-height adjacent lines (multi-word / two-row signs).
        candidates.sort((a, b) => b.h - a.h);
        const top = candidates[0];
        const sameRow = candidates
            .filter((c) => c.h >= top.h * 0.8)
            .sort((a, b) => a.y0 - b.y0);
        const value = sameRow.map((c) => c.text).join(' ').replace(/\s+/g, ' ').trim();
        return { value, confidence: Math.min(...sameRow.map((c) => c.conf)) };
    }

    // ── Menu extraction (both cases) ─────────────────────────────────────

    // Price at end of line: "35.000đ", "25k", "25 nghìn", "1.500.000", "45.000 VNĐ"
    const PRICE_RE = /(\d[\d.,]*)\s*(?:nghìn|vnđ|đồng)?\s*(?:đ|k)?\s*$/i;
    const NOISE_RE = /^(thực\s*đơn|menu|khai\s*vị|món\s*chính|tráng\s*miệng|đồ\s*uống|nước|ăn\s*vặt)$/i;

    function _parsePrice(raw, full) {
        const cleaned = raw.replace(/\./g, '').replace(',', '.');
        const num = parseFloat(cleaned);
        if (isNaN(num)) return null;
        const unit = (full.match(/(nghìn|vnđ|đồng|đ|k)/i) || [])[0] || '';
        if (/nghìn|k/i.test(unit)) return Math.round(num * 1000);
        if (/vnđ|đồng|đ/i.test(unit)) return Math.round(num);
        // No unit: must look like a real VN price (>= 100), else not a price
        return num >= 100 ? Math.round(num) : null;
    }

    function _parseMenuLine(text, lineNo) {
        const m = text.match(PRICE_RE);
        if (!m) return { name: text, price: null };
        const name = text.slice(0, m.index).replace(/\s+$/, '').trim();
        const price = _parsePrice(m[1], m[0]);
        if (price === null) return { name: text, price: null };
        if (!name || name.length < 2) return { name: null, price };
        return { name, price };
    }

    async function extractMenu(files) {
        const imageResults = [];
        for (const file of files) {
            try {
                const { lines } = await _recognizeLines(file);
                imageResults.push(lines);
            } catch (e) {
                // skip failed image, keep going
            }
        }

        // Dedupe repeated lines across photos (headers/footers/watermarks appear
        // at the same position in every shot) — normalize text + rough y band.
        const seen = new Map(); // normalized text -> count
        for (const lines of imageResults) {
            for (const l of lines) {
                const key = l.text.toLowerCase().replace(/\s+/g, ' ').trim();
                seen.set(key, (seen.get(key) || 0) + 1);
            }
        }

        const productLines = [];
        const warnings = [];
        let lineNo = 0;
        const photos = imageResults.length;

        for (const lines of imageResults) {
            for (const l of lines) {
                const norm = l.text.toLowerCase().replace(/\s+/g, ' ').trim();
                if (NOISE_RE.test(norm)) continue;
                if (/^[\d\s.,-]+$/.test(norm) && norm.length <= 6) continue; // page numbers
                if (/\d{9,}/.test(norm)) continue;                            // phone numbers
                if (seen.get(norm) > 1 && photos > 1) continue;              // repeated header/footer

                const parsed = _parseMenuLine(l.text, lineNo);
                if (parsed.name === null) {
                    // Price-only line: attach to previous product (e.g. price on its own row)
                    if (productLines.length > 0) {
                        productLines[productLines.length - 1].price = parsed.price;
                    }
                    continue;
                }
                lineNo++;
                productLines.push({
                    name: parsed.name,
                    price: parsed.price,
                    lineNo,
                    confidence: l.confidence
                });
            }
        }

        if (!productLines.length) warnings.push('Không đọc được sản phẩm nào từ ảnh menu — chụp lại hoặc nhập tay.');
        const lowConf = productLines.filter((p) => p.confidence < 60).length;
        if (lowConf > 0) warnings.push(`${lowConf} dòng có độ tin cậy thấp — cần rà soát.`);

        return { productLines, warnings };
    }

    async function unload() {
        if (_workerPromise) {
            try {
                const worker = await _workerPromise;
                await worker.terminate();
            } catch (e) { /* best-effort */ }
            _workerPromise = null;
        }
    }

    return {
        recognizeDocument: _recognizeLines,
        extractGpdk,
        extractSignName,
        extractMenu,
        fileToCanvas,
        unload
    };
})();