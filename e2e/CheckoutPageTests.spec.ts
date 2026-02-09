import { test, expect } from '@playwright/test';

test('Checkout page shows shipping and payment sections', async ({ page }) => {
  await page.goto('/checkout');

  await expect(page.getByRole('heading', { name: 'Checkout' })).toBeVisible();

  await expect(page.getByRole('heading', { name: 'Shipping address' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Payment' })).toBeVisible();

  await expect(
    page.getByText('Use the built-in card checkout or pay with PayPal.')
  ).toBeVisible();
  await expect(page.getByText('Or pay securely with PayPal:')).toBeVisible();
});

test('Shows validation when required fields are missing on card checkout', async ({ page }) => {
  await page.goto('/');

  await expect(
    page.getByRole('heading', { name: 'Ready for a new adventure?' })
  ).toBeVisible();

  await page.getByRole('link', { name: 'Adventurer GPS Watch' }).click();
  await expect(
    page.getByRole('heading', { name: 'Adventurer GPS Watch' })
  ).toBeVisible();

  await page.getByRole('button', { name: 'Add to shopping bag' }).click();
  await page.getByRole('link', { name: 'shopping bag' }).click();

  await expect(page.getByRole('heading', { name: 'Shopping bag' })).toBeVisible();

  await page.getByRole('link', { name: 'Check out' }).click();
  await expect(page.getByRole('heading', { name: 'Checkout' })).toBeVisible();

  // Clear one of the required fields to trigger validation
  await page.getByLabel('Address').fill('');

  await page.getByRole('button', { name: 'Place order (card)' }).click();

  await expect(
    page.getByText('The Street field is required.').first()
  ).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Checkout' })).toBeVisible();
});

