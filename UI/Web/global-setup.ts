import { Browser, chromium, FullConfig, request } from '@playwright/test';

async function globalSetup(config: FullConfig) {
    let requestContext = await request.newContext();
    var token = await requestContext.post('http://localhost:5000/account/login', {
    form: {
        'user': 'Joe',
        'password': 'P4ssword'
    }
    });
    //console.log(token.json());
    // Save signed-in state to 'storageState.json'.
    //await requestContext.storageState({ path: 'adminStorageState.json' });
    await requestContext.dispose();

    requestContext = await request.newContext();
    await requestContext.post('http://localhost:5000/account/login', {
    form: {
        'user': 'nonadmin',
        'password': 'P4ssword'
    }
    });
    // Save signed-in state to 'storageState.json'.
    //await requestContext.storageState({ path: 'nonAdminStorageState.json' });
    await requestContext.dispose();
}


 
// async function globalSetup (config: FullConfig) {
//   const browser = await chromium.launch()
//   await saveStorage(browser, 'nonadmin', 'P4ssword', 'storage/user.json')
//   await saveStorage(browser, 'Joe', 'P4ssword', 'storage/admin.json')
//   await browser.close()
// }
 
async function saveStorage (browser: Browser, username: string, password: string, saveStoragePath: string) {
  const page = await browser.newPage()
  await page.goto('http://localhost:5000/account/login')
  await page.type('#username', username)
  await page.type('#password', password)
  await page.click('button[type="submit"]')
  await page.context().storageState({ path: saveStoragePath })
}

export default globalSetup;