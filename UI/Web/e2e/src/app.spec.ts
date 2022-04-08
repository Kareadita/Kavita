import { test, expect } from '@playwright/test';

test('When not authenticated, should be redirected to login page', async ({ page }) => {
  await page.goto('http://localhost:4200/', { waitUntil: 'networkidle' });
  console.log('url: ', page.url());
  expect(page.url()).toBe('http://localhost:4200/login');
});