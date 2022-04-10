import { expect, test } from "@playwright/test";

test.use({ storageState: 'storage/admin.json' });

test('When on login page, side nav should not render', async ({ page }) => {
    await page.goto('http://localhost:4200/login', { waitUntil: 'networkidle' });
    await expect(page.locator(".side-nav")).toHaveCount(0)
});

test('When on library page, side nav should render', async ({ page }) => {
    await page.goto('http://localhost:4200/library', { waitUntil: 'networkidle' });
    await expect(page.locator(".side-nav")).toHaveCount(1)
});