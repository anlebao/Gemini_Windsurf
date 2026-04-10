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

  test('TC_Voice_TextCommand - Should process text command via API', async ({ request }) => {
    try {
      const textCommand = {
        CommandText: 'xong đơn 123',
        OrderId: 'TEST_ORDER_123',
        Parameters: 'ready'
      };

      const response = await request.post(`${config.GATEWAY_URL}/api/v1/voicecommand/text-command`, {
        data: textCommand
      });

      expect(response.ok()).toBeTruthy();
      
      const result = await response.json();
      
      // Verify response structure
      expect(result).toHaveProperty('Command');
      expect(result).toHaveProperty('Executed');
      expect(result.Command).toHaveProperty('CommandText', 'xong đơn 123');
      expect(result.Command).toHaveProperty('CommandType', 'update_status');
      expect(result.Command).toHaveProperty('OrderId', 'TEST_ORDER_123');
      
      await reporter.addResult('Voice Text Command', 'pass', 'Text command processed successfully');
      
    } catch (error) {
      await reporter.addResult('Voice Text Command', 'fail', error.message);
      throw error;
    }
  });

  test('TC_Voice_TTS - Should convert text to speech', async ({ request }) => {
    try {
      const ttsRequest = {
        Text: 'Đơn mới: Trà sữa truyền thống - Ghi chú: ít đường',
        Language: 'vi-VN'
      };

      const response = await request.post(`${config.GATEWAY_URL}/api/v1/voicecommand/tts`, {
        data: ttsRequest
      });

      expect(response.ok()).toBeTruthy();
      
      const result = await response.json();
      
      // Verify response structure
      expect(result).toHaveProperty('AudioUrl');
      expect(result.AudioUrl).toContain('tts-api.example.com');
      expect(result.AudioUrl).toContain('audio/');
      expect(result.AudioUrl).toContain('.mp3');
      expect(result.AudioUrl).toContain('lang=vi-VN');
      
      await reporter.addResult('Voice TTS', 'pass', 'Text converted to speech successfully');
      
    } catch (error) {
      await reporter.addResult('Voice TTS', 'fail', error.message);
      throw error;
    }
  });

  test('TC_Voice_AudioStorage - Should handle audio file operations', async ({ request }) => {
    try {
      // Test audio cleanup
      const cleanupResponse = await request.post(`${config.GATEWAY_URL}/api/v1/voicecommand/cleanup-expired`);
      
      expect(cleanupResponse.ok()).toBeTruthy();
      
      const cleanupResult = await cleanupResponse.json();
      
      // Verify cleanup response structure
      expect(cleanupResult).toHaveProperty('CleanedFiles');
      expect(cleanupResult).toHaveProperty('TotalExpired');
      expect(cleanupResult).toHaveProperty('Timestamp');
      
      await reporter.addResult('Voice Audio Storage', 'pass', 'Audio storage operations working correctly');
      
    } catch (error) {
      await reporter.addResult('Voice Audio Storage', 'fail', error.message);
      throw error;
    }
  });

  test('TC_Voice_StatusUpdate - Should update order status via voice command', async ({ request }) => {
    try {
      const statusCommand = {
        CommandText: 'xong đơn ABC123',
        OrderId: 'ABC123',
        Parameters: 'ready'
      };

      const response = await request.post(`${config.GATEWAY_URL}/api/v1/voicecommand/text-command`, {
        data: statusCommand
      });

      expect(response.ok()).toBeTruthy();
      
      const result = await response.json();
      
      // Verify command was executed
      expect(result.Executed).toBe(true);
      expect(result.Command.CommandType).toBe('update_status');
      expect(result.Command.Parameters).toBe('ready');
      
      await reporter.addResult('Voice Status Update', 'pass', 'Order status updated via voice command');
      
    } catch (error) {
      await reporter.addResult('Voice Status Update', 'fail', error.message);
      throw error;
    }
  });
});
