// 🎤 Voice Note — Browser Speech Recognition API (vi-VN)
// Loaded via index.html (Blazor WASM cannot execute inline <script> in .razor)

let recognition = null;
let voiceDotNetRef = null;
let voiceTargetId = null; // ID of textarea to fill (for inline mode)
// #142-comment-fix: Home search mode — interim results + silence auto-submit
let isHomeSearchMode = false;
let silenceTimer = null;
let finalTranscript = '';
const SILENCE_DELAY_MS = 2500; // 2.5s silence → auto-submit

// Initialize speech recognition
window.initializeSpeechRecognition = function (dotNetReference, targetId) {
    voiceDotNetRef = dotNetReference;
    voiceTargetId = targetId || null;
    // #142-comment-fix: targetId = null means home search mode → enable interim results
    isHomeSearchMode = !voiceTargetId;

    const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!SpeechRecognition) {
        console.warn('Speech Recognition API not supported in this browser.');
        return false;
    }

    recognition = new SpeechRecognition();
    recognition.lang = 'vi-VN';
    // #142-comment-fix: home search → continuous + interim results for real-time textbox fill
    recognition.continuous = isHomeSearchMode;
    recognition.interimResults = isHomeSearchMode;
    recognition.maxAlternatives = 1;

    recognition.onresult = function (event) {
        let interimText = '';
        let finalText = '';

        for (let i = event.resultIndex; i < event.results.length; i++) {
            const result = event.results[i];
            if (result.isFinal) {
                finalText += result[0].transcript;
            } else {
                interimText += result[0].transcript;
            }
        }

        if (isHomeSearchMode) {
            // #142-comment-fix: build cumulative final + current interim
            if (finalText) {
                finalTranscript += finalText;
            }
            const displayText = (finalTranscript + interimText).trim();

            // Fill home search textbox via Blazor (real-time as user speaks)
            if (voiceDotNetRef && displayText) {
                voiceDotNetRef.invokeMethodAsync('UpdateVoiceTranscript', displayText);
            }

            // #142-comment-fix: reset silence timer — auto-submit after 2.5s of no new results
            if (silenceTimer) clearTimeout(silenceTimer);
            silenceTimer = setTimeout(function () {
                const submitText = (finalTranscript || displayText).trim();
                if (submitText && voiceDotNetRef) {
                    console.log('[voice] Auto-submitting after silence:', submitText);
                    voiceDotNetRef.invokeMethodAsync('SetTranscriptionText', submitText);
                    finalTranscript = '';
                }
            }, SILENCE_DELAY_MS);
        } else {
            // Original voice-note mode — single final result
            const transcript = event.results[0][0].transcript;
            console.log('Transcription:', transcript);

            // Fill inline textarea if targetId is set
            if (voiceTargetId) {
                const textarea = document.getElementById(voiceTargetId);
                if (textarea) {
                    if (textarea.value && !textarea.value.endsWith(' ')) {
                        textarea.value += ' ' + transcript;
                    } else {
                        textarea.value += transcript;
                    }
                    textarea.dispatchEvent(new Event('input', { bubbles: true }));
                }
            }

            if (voiceDotNetRef) {
                voiceDotNetRef.invokeMethodAsync('SetTranscriptionText', transcript);
            }
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
        // #142-comment-fix: if home search mode + there's pending final transcript,
        // auto-submit immediately (recognition ended before silence timer fired)
        if (isHomeSearchMode && silenceTimer) {
            clearTimeout(silenceTimer);
            silenceTimer = null;
        }
        if (isHomeSearchMode && finalTranscript && finalTranscript.trim() && voiceDotNetRef) {
            const submitText = finalTranscript.trim();
            finalTranscript = '';
            console.log('[voice] Auto-submitting on recognition end:', submitText);
            voiceDotNetRef.invokeMethodAsync('SetTranscriptionText', submitText);
        }
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
        finalTranscript = ''; // #142-comment-fix: reset for new session
        if (silenceTimer) { clearTimeout(silenceTimer); silenceTimer = null; }
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
