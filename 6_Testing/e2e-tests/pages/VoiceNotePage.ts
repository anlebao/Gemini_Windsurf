import { Page, expect } from '@playwright/test';

/**
 * W4-T5: Page Object for Voice Note UI (KhachLink).
 * Handles STT (SpeechRecognition) mocking + voice note recording + text submission.
 * Web Speech API is NOT available in Playwright headless — must mock before page load.
 */
export class VoiceNotePage {
  readonly page: Page;

  constructor(page: Page) {
    this.page = page;
  }

  /**
   * Mock Web Speech API BEFORE navigating to voice note page.
   * Call this before page.goto() to ensure mocks are in place.
   * Mocks both SpeechRecognition (STT) and speechSynthesis (TTS).
   */
  async mockSpeechAPI(transcript: string = 'Không đá, ít đường'): Promise<void> {
    await this.page.addInitScript((text) => {
      // Mock SpeechRecognition
      const MockSpeechRecognition = class {
        lang = 'vi-VN';
        continuous = false;
        interimResults = false;
        maxAlternatives = 1;
        onresult: any = null;
        onend: any = null;
        onerror: any = null;
        onstart: any = null;

        start() {
          if (this.onstart) this.onstart();
          setTimeout(() => {
            if (this.onresult) {
              this.onresult({
                results: [{ 0: { transcript: text, confidence: 0.9 }, isFinal: true }]
              });
            }
            if (this.onend) this.onend();
          }, 100);
        }
        stop() {}
      };
      (window as any).SpeechRecognition = MockSpeechRecognition;
      (window as any).webkitSpeechRecognition = MockSpeechRecognition;

      // Mock speechSynthesis (TTS)
      (window as any).speechSynthesis = {
        speak: () => {},
        cancel: () => {},
        pending: false,
        paused: false,
        speaking: false,
        onvoiceschanged: null,
        getVoices: () => [],
      };
    }, transcript);
  }

  /**
   * Navigate to voice note page.
   */
  async goto(orderId?: string): Promise<void> {
    const url = orderId ? `/voice-note?OrderId=${orderId}` : '/voice-note';
    await this.page.goto(url);
    await this.page.waitForLoadState('networkidle');
  }

  /**
   * Click "Bắt đầu ghi âm" button to start STT recording.
   */
  async startRecording(): Promise<void> {
    const btn = this.page.locator('button:has-text("Bắt đầu ghi âm")');
    await expect(btn).toBeVisible({ timeout: 10000 });
    await btn.click();
  }

  /**
   * Wait for transcription text to appear (mock fires after 100ms).
   */
  async waitForTranscription(timeout: number = 5000): Promise<string> {
    const transcriptEl = this.page.getByTestId('transcription-text');
    await expect(transcriptEl).toBeVisible({ timeout });
    const text = await transcriptEl.textContent();
    return text || '';
  }

  /**
   * Submit voice note (click "Gửi ghi chú" button).
   */
  async submitVoiceNote(): Promise<void> {
    const btn = this.page.locator('button:has-text("Gửi ghi chú")');
    await expect(btn).toBeVisible({ timeout: 5000 });
    await btn.click();
  }

  /**
   * Full flow: start recording → wait for transcription → submit.
   */
  async recordAndSubmit(expectedTranscript: string): Promise<void> {
    await this.startRecording();
    const transcript = await this.waitForTranscription();
    expect(transcript).toContain(expectedTranscript);
    await this.submitVoiceNote();
  }

  /**
   * Verify voice note toggle is OFF — text-only input should be visible.
   */
  async verifyToggleOff(): Promise<void> {
    const textInput = this.page.getByTestId('text-note-input');
    await expect(textInput).toBeVisible({ timeout: 5000 });
  }

  /**
   * Verify voice note toggle is ON — STT UI should be visible.
   */
  async verifyToggleOn(): Promise<void> {
    const container = this.page.getByTestId('voice-note-container');
    await expect(container).toBeVisible({ timeout: 5000 });
  }
}
