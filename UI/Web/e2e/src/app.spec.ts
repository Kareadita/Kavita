import { test, expect } from '@playwright/test';

test('When not authenticated, should be redirected to login page', async ({ page }) => {
    await page.goto('http://localhost:4200/', { waitUntil: 'networkidle' });
    expect(page.url()).toBe('http://localhost:4200/login');
});

test('When not authenticated, should be redirected to login page from an authenticated page', async ({ page }) => {
    await page.goto('http://localhost:4200/library', { waitUntil: 'networkidle' });
    expect(page.url()).toBe('http://localhost:4200/login');
});

// Not sure how to test when we need localStorage: https://github.com/microsoft/playwright/issues/6258
// test('When authenticated, should be redirected to library page', async ({ page }) => {
//     await page.goto('http://localhost:4200/', { waitUntil: 'networkidle' });
//     console.log('url: ', page.url());
//     expect(page.url()).toBe('http://localhost:4200/library');
// });