// 🎤 Voice Note — Browser Speech Recognition API (vi-VN)
// Loaded via index.html (Blazor WASM cannot execute inline <script> in .razor)

let recognition = null;
let voiceDotNetRef = null;
let voiceTargetId = null; // ID of textarea to fill (for inline mode)

// Initialize speech recognition
window.initializeSpeechRecognition = function (dotNetReference, targetId) {
    voiceDotNetRef = dotNetReference;
    voiceTargetId = targetId || null;

    const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!SpeechRecognition) {
        console.warn('Speech Recognition API not supported in this browser.');
        return false;
    }

    recognition = new SpeechRecognition();
    recognition.lang = 'vi-VN';
    recognition.continuous = false;
    recognition.interimResults = false;
    recognition.maxAlternatives = 1;

    recognition.onresult = function (event) {
        const transcript = event.results[0][0].transcript;
        console.log('Transcription:', transcript);

        // Fill inline textarea if targetId is set
        if (voiceTargetId) {
            const textarea = document.getElementById(voiceTargetId);
            if (textarea) {
                // Append to existing text (don't overwrite)
                if (textarea.value && !textarea.value.endsWith(' ')) {
                    textarea.value += ' ' + transcript;
                } else {
                    textarea.value += transcript;
                }
                // Trigger input event so Blazor @bind updates
                textarea.dispatchEvent(new Event('input', { bubbles: true }));
            }
        }

        // Notify Blazor
        if (voiceDotNetRef) {
            voiceDotNetRef.invokeMethodAsync('SetTranscriptionText', transcript);
        }
    };

    recognition.onerror = function (event) {
        console.error('Speech recognition error:', event.error);
        let errorMessage = 'Lỗi nhận dạng giọng nói.';
        switch (event.error) {
            case 'no-speech': errorMessage = 'Không phát hiện giọng nói. Vui lòng thử lại.'; break;
            case 'audio-capture': errorMessage = 'Không thể truy cập micro. Vui lòng cấp quyền micro.'; break;
            case 'not-allowed': errorMessage = 'Quyền truy cập micro bị từ chối.'; break;
            case 'network': errorMessage = 'Lỗi mạng. Vui lòng kiểm tra kết nối.'; break;
        }
        if (voiceDotNetRef) {
            voiceDotNetRef.invokeMethodAsync('OnVoiceError', errorMessage);
        }
    };

    recognition.onend = function () {
        window.isVoiceRecording = false;
        if (voiceDotNetRef) {
            voiceDotNetRef.invokeMethodAsync('OnRecordingEnd');
        }
    };

    return true;
};

// Check browser support
window.isSpeechRecognitionSupported = function () {
    return !!(window.SpeechRecognition || window.webkitSpeechRecognition);
};

// Start recording
window.startRecording = function () {
    if (recognition && !window.isVoiceRecording) {
        window.isVoiceRecording = true;
        try {
            recognition.start();
            console.log('Started recording in Vietnamese...');
        } catch (e) {
            // recognition.start() throws if already started — reset state
            window.isVoiceRecording = false;
            console.error('startRecording error:', e);
        }
    }
};

// Stop recording
window.stopRecording = function () {
    if (recognition && window.isVoiceRecording) {
        window.isVoiceRecording = false;
        try {
            recognition.stop();
            console.log('Stopped recording...');
        } catch (e) {
            console.error('stopRecording error:', e);
        }
    }
};
