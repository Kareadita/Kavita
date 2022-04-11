import { expect, test } from "@playwright/test";

test('When on login page, clicking Forgot Password should redirect', async ({ page }) => {
    await page.goto('http://localhost:4200/login', { waitUntil: 'networkidle' });

    await page.click('a[routerlink="/registration/reset-password"]')
    await page.waitForLoadState('networkidle');

    expect(page.url()).toBe('http://localhost:4200/registration/reset-password');
});

test('Going directly to reset url should stay on the page', async ({page}) => {
    await page.goto('http://localhost:4200/registration/reset-password', { waitUntil: 'networkidle' });
    const email = page.locator('#email');
    expect(email).toBeEditable();
})

test('Submitting an email, should give a prompt to user, redirect back to login', async ({ page }) => {
    await page.goto('http://localhost:4200/registration/reset-password', { waitUntil: 'networkidle' });

    const email = page.locator('#email');
    expect(email).toBeEditable();

    await email.type('XXX@gmail.com');

    const button =  page.locator('button[type="submit"]');
    await button.click();

    const toastr = page.locator('#toast-container div[role="alertdialog"]')
    await expect(toastr).toHaveText('An email will be sent to the email if it exists in our database');
    await page.waitForLoadState('networkidle');

    expect(page.url()).toBe('http://localhost:4200/login');
});