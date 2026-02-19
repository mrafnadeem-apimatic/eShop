import { test, expect } from '@playwright/test';

test('Checkout with PayPal payment method', async ({ page }) => {
  page.on('pageerror', (error) => {
    // Surface any client-side errors from Blazor/JS in the test output.
    console.log('PAGE ERROR:', error.message);
  });

  page.on('console', (msg) => {
    console.log(`BROWSER CONSOLE [${msg.type()}]:`, msg.text());
  });

  page.on('requestfailed', (request) => {
    console.log('REQUEST FAILED:', request.url(), request.failure()?.errorText);
  });

  // Stub the PayPal JS SDK to avoid external dependencies and provide a deterministic test button.
  await page.route('https://www.paypal.com/sdk/js*', async (route) => {
    console.log('Intercepting PayPal SDK request:', route.request().url());
    await route.fulfill({
      status: 200,
      contentType: 'application/javascript',
      body: `
        window.paypal = {
          Buttons: function(config) {
            return {
              render: function(selector) {
                const container = document.querySelector(selector);
                if (!container) return;

                const button = document.createElement('button');
                button.id = 'paypal-test-button';
                button.type = 'button';
                button.textContent = 'Pay with PayPal (Test)';

                button.addEventListener('click', () => {
                  if (config && typeof config.createOrder === 'function') {
                    Promise.resolve(config.createOrder()).then(orderId => {
                      if (config.onApprove) {
                        config.onApprove({ orderID: orderId }, {});
                      }
                    });
                  }
                });

                container.appendChild(button);
              }
            };
          }
        };
      `,
    });
  });

  // Stub the backend PayPal order creation endpoint so tests do not call the real PayPal APIs.
  await page.route('**/api/paypal/order', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        paypalOrderId: 'TEST-PAYPAL-ORDER-ID',
        approvalUrl: 'https://www.paypal.com/checkoutnow?token=TEST',
      }),
    });
  });

  await page.goto('/');

  await expect(page.getByRole('heading', { name: 'Ready for a new adventure?' })).toBeVisible();

  // Add an item to the shopping bag.
  await page.getByRole('link', { name: 'Adventurer GPS Watch' }).click();
  await page.getByRole('button', { name: 'Add to shopping bag' }).click();
  await page.getByRole('link', { name: 'shopping bag' }).click();
  await expect(page.getByRole('heading', { name: 'Shopping bag' })).toBeVisible();

  // Navigate directly to checkout.
  await page.goto('/checkout');
  await page.waitForURL('**/checkout');

  // Give the interactive checkout page a brief moment to render.
  await page.waitForTimeout(1000);
  const content = await page.content();
  console.log('Checkout page URL:', await page.url());
  console.log('Checkout page title text includes "Checkout":', content.includes('Checkout'));
  console.log('Checkout page includes "Login":', content.includes('Login'));
  console.log('Checkout page includes "Shopping bag":', content.includes('Shopping bag'));
  const hasPayPalText = content.includes('Pay with PayPal');
  console.log('PayPal text present on checkout page:', hasPayPalText);
  console.log('Checkout page HTML preview:', content.slice(0, 500));

  // Switch payment method to PayPal.
  await page.locator('input[type="radio"][name="payment-method"][value="PayPal"]').first().check();

  // PayPal container and test button should be rendered by the stubbed SDK.
  const paypalContainer = page.locator('#paypal-button-container');
  await expect(paypalContainer).toBeVisible();

  const paypalTestButton = page.locator('#paypal-test-button');
  await expect(paypalTestButton).toBeVisible();
});

