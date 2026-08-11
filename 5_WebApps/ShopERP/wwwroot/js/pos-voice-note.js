// #114 r2: POS voice note — browser Speech Recognition API (vi-VN)
// Used by POS Create.razor for staff to dictate order notes hands-free

let posRecognition = null;
let posDotNetRef = null;

window.vananPosStartRecording = function (dotNetReference) {
    posDotNetRef = dotNetReference;

    const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!SpeechRecognition) {
        if (posDotNetRef) {
            posDotNetRef.invokeMethodAsync('PosOnVoiceError', 'Trình duyệt không hỗ trợ nhận dạng giọng nói.');
        }
        return false;
    }

    // Cancel any existing recognition
    if (posRecognition) {
        try { posRecognition.stop(); } catch (e) { /* ignore */ }
    }

    posRecognition = new SpeechRecognition();
    posRecognition.lang = 'vi-VN';
    posRecognition.continuous = false;
    posRecognition.interimResults = false;
    posRecognition.maxAlternatives = 1;

    posRecognition.onresult = function (event) {
        const transcript = event.results[0][0].transcript;
        console.log('POS transcription:', transcript);
        if (posDotNetRef) {
            posDotNetRef.invokeMethodAsync('PosSetTranscription', transcript);
        }
    };

    posRecognition.onerror = function (event) {
        console.error('POS speech recognition error:', event.error);
        let msg = 'Lỗi nhận dạng giọng nói.';
        switch (event.error) {
            case 'no-speech': msg = 'Không phát hiện giọng nói.'; break;
            case 'audio-capture': msg = 'Không truy cập được micro.'; break;
            case 'not-allowed': msg = 'Quyền micro bị từ chối.'; break;
            case 'network': msg = 'Lỗi mạng.'; break;
        }
        if (posDotNetRef) {
            posDotNetRef.invokeMethodAsync('PosOnVoiceError', msg);
        }
    };

    posRecognition.onend = function () {
        // Recognition ended — Blazor handles state via PosSetTranscription or PosOnVoiceError
        console.log('POS recognition ended');
    };

    try {
        posRecognition.start();
        console.log('POS recording started (vi-VN)');
    } catch (e) {
        console.error('POS startRecording error:', e);
        if (posDotNetRef) {
            posDotNetRef.invokeMethodAsync('PosOnVoiceError', 'Không thể bắt đầu ghi âm.');
        }
    }
    return true;
};

window.vananPosStopRecording = function () {
    if (posRecognition) {
        try {
            posRecognition.stop();
            console.log('POS recording stopped');
        } catch (e) {
            console.error('POS stopRecording error:', e);
        }
    }
};
