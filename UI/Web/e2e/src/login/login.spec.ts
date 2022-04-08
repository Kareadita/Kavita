import { expect, test } from "@playwright/test";

test('When not authenticated, should be redirected to login page', async ({ page }) => {
    await page.goto('http://localhost:4200/', { waitUntil: 'networkidle' });
    expect(page.url()).toBe('http://localhost:4200/login');
});

test('Should be able to log in', async ({ page }) => {

    await page.goto('http://localhost:4200/login', { waitUntil: 'networkidle' });
    const username = page.locator('#username');
    expect(username).toBeEditable();
    const password = page.locator('#password');
    expect(password).toBeEditable();

    await username.type('Joe');
    await password.type('P4ssword');

    const button =  page.locator('button[type="submit"]');
    await button.click();

    await page.waitForTimeout(1000);
    await page.waitForLoadState();
    expect(page.url()).toBe('http://localhost:4200/library');
});