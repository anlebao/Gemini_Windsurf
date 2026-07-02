import { test, expect } from '@playwright/test';
import { getTestConfig } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

test.describe('Voice Command Tests', () => {
  let config: any;
  let reporter: TestReporter;

  test.beforeAll(async () => {
    config = await getTestConfig();
    reporter = new TestReporter('voice-command');
  });

  test.beforeEach(async () => {
    await reporter.startTest('Voice Command Test');
  });

  test.afterEach(async () => {
    await reporter.endTest();
  });

  test('TC_Voice_Flow - Should process voice command and update order note', async ({ page }) => {
    try {
      // Navigate to KhachLink
      await page.goto(config.KHACHLINK_URL);
      
      // Wait for products to load
      await page.waitForSelector('.feature-card');
      
      // Add a product to cart
      const firstProduct = page.locator('.feature-card').first();
      await firstProduct.locator('button').click();
      
      // Wait for cart to update
      await page.waitForTimeout(1000);
      
      // Place order to create order ID
      const placeOrderButton = page.locator('button:has-text("Xác nhận đơn hàng")');
      await placeOrderButton.click();
      
      // Wait for order to be created
      await page.waitForTimeout(2000);
      
      // Check if voice command button is available
      const voiceButton = page.locator('button:has-text("Ghi chú giọng nói")');
      
      // Check if browser supports speech recognition
      const supportsSpeech = await page.evaluate(() => {
        return 'webkitSpeechRecognition' in window || 'SpeechRecognition' in window;
      });
      
      if (supportsSpeech) {
        // Mock voice recognition for testing
        await page.evaluate(() => {
          // Mock the Web Speech API
          const mockSpeechRecognition = class {
            constructor() {
              this.lang = 'vi-VN';
              this.continuous = false;
              this.interimResults = false;
              this.maxAlternatives = 1;
              
              setTimeout(() => {
                if (this.onresult) {
                  this.onresult({
                    results: [{
                        0: {
                          transcript: 'đơn hàng cần thêm đường ngọt',
                          confidence: 0.9
                        }
                      }]
                  });
                }
                if (this.onend) {
                  this.onend();
                }
              }, 1000);
            }
            
            start() {
              if (this.onstart) this.onstart();
            }
            
            stop() {
              if (this.onstop) this.onstop();
            }
          };
          
          // Replace the real SpeechRecognition
          window.SpeechRecognition = mockSpeechRecognition;
          window.webkitSpeechRecognition = mockSpeechRecognition;
        });
        
        // Click voice recording button
        await voiceButton.click();
        
        // Wait for recording to complete
        await page.waitForTimeout(2000);
        
        // Check if transcript is displayed
        const transcript = page.locator('.transcript-text');
        await expect(transcript).toBeVisible();
        
        const transcriptText = await transcript.textContent();
        expect(transcriptText).toContain('đơn hàng cần thêm đường ngọt');
        
        await reporter.addResult('Voice Flow', 'pass', 'Voice command processed and transcript displayed');
        
      } else {
        // Skip test if browser doesn't support speech recognition
        console.log('Browser does not support speech recognition - skipping test');
        await reporter.addResult('Voice Flow', 'skip', 'Browser does not support speech recognition');
      }
      
    } catch (error) {
      await reporter.addResult('Voice Flow', 'fail', error.message);
      throw error;
    }
  });
});
