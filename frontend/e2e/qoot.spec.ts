import { expect, test, type Page } from '@playwright/test';

const EMAIL = process.env.QOOT_E2E_EMAIL ?? '';
const PASSWORD = process.env.QOOT_E2E_PASSWORD ?? '';
const stamp = Date.now().toString(36);

test.describe.configure({ mode: 'serial' });
test.skip(!EMAIL || !PASSWORD, 'Set QOOT_E2E_EMAIL and QOOT_E2E_PASSWORD (an administrator on a development database).');

// The dashboard in English for stable labels.
test.beforeEach(async ({ page }) => {
  await page.addInitScript(() => {
    try {
      localStorage.setItem('qoot.admin.lang', 'en');
    } catch {
      /* ignore */
    }
  });
});

async function signIn(page: Page, email = EMAIL, password = PASSWORD) {
  await page.goto('/admin/login');
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).not.toHaveURL(/\/admin\/login/);
}

async function signOut(page: Page) {
  await page.getByRole('button', { name: 'Sign out' }).first().click();
  await expect(page).toHaveURL(/\/admin\/login/);
}

const bi = (page: Page, legend: string, lang: 'Arabic' | 'English') =>
  page.locator('fieldset', { has: page.locator('legend', { hasText: legend }) }).first().getByLabel(lang, { exact: true });

// ------------------------------------------------------------------ public website

test('the Arabic home page renders every section right to left, and switches to English', async ({ page }) => {
  await page.goto('/');
  await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
  for (const selector of ['.hero', '.supplies', '.how', '.who', '.faq', '.contact', '.visit', '.site-footer']) {
    await expect(page.locator(selector).first()).toBeVisible();
  }
  await expect(page.locator('.tile')).toHaveCount(8);
  await page.locator('[data-lang-switch="en"]').first().click();
  await expect(page).toHaveURL(/\/en\/$/);
  await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');
  await expect(page.locator('.hero-title')).toBeVisible();
});

test('the Contact us form sends a message that arrives in the dashboard', async ({ page }) => {
  await page.goto('/en/#contact');
  const form = page.locator('#contact-form');
  await form.locator('[name="name"]').fill(`E2E Visitor ${stamp}`);
  await form.locator('[name="phone"]').fill('+218 91 000 0000');
  await form.locator('[name="email"]').fill('visitor@example.com');
  await form.locator('[name="message"]').fill(`Please call me about daily meals ${stamp}`);
  await form.locator('button[type="submit"]').click();
  await expect(page.locator('.contact-status[data-kind="ok"]')).toBeVisible();

  await signIn(page);
  await page.goto('/admin/messages');
  await page.getByRole('link', { name: new RegExp(`E2E Visitor ${stamp}`) }).click();
  await page.getByRole('radio', { name: 'Done' }).click();
  await expect(page.locator('.toast-ok')).toBeVisible();
  await page.getByLabel('Internal notes').fill('Called back, sent prices.');
  await page.getByRole('button', { name: 'Save notes' }).click();
  await expect(page.getByLabel('Internal notes')).toHaveValue('Called back, sent prices.');
  await page.getByRole('button', { name: 'Delete' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click();
  await expect(page.getByRole('link', { name: new RegExp(`E2E Visitor ${stamp}`) })).toHaveCount(0);
});

// ------------------------------------------------------------------ access

test('the dashboard needs a session, and a wrong password is refused', async ({ page }) => {
  await page.goto('/admin/sections');
  await expect(page).toHaveURL(/\/admin\/login\?next=/);
  await page.getByLabel('Email').fill(EMAIL);
  await page.getByLabel('Password').fill('not-the-password');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.locator('.form-error')).toBeVisible();
  const api = await page.request.get('/api/admin/dashboard');
  expect(api.status()).toBe(401);
});

// ------------------------------------------------------------------ sections

test('a section edit stays a draft until published, then shows on the website', async ({ page, browser }) => {
  await signIn(page);
  await page.goto('/admin/sections/hero');
  const field = bi(page, 'Title, first half', 'English');
  const original = await field.inputValue();
  const changed = `Qoot ${stamp}`;
  await field.fill(changed);
  await page.getByRole('button', { name: 'Save draft' }).click();
  await expect(page.getByText('Draft saved')).toBeVisible();

  const visitor = await browser.newPage();
  await visitor.goto('/en/');
  await expect(visitor.locator('.hero-title')).not.toContainText(changed);

  await page.getByRole('button', { name: 'Publish', exact: true }).click();
  await expect(page.getByText('Published. The website shows it now.')).toBeVisible();
  await visitor.reload();
  await expect(visitor.locator('.hero-title')).toContainText(changed);

  await field.fill(original);
  await page.getByRole('button', { name: 'Save and publish' }).click();
  await expect(page.getByText('Published. The website shows it now.')).toBeVisible();
  await visitor.reload();
  await expect(visitor.locator('.hero-title')).not.toContainText(changed);
  await visitor.close();
});

test('a hidden section disappears from the website and its menu link with it', async ({ page, browser }) => {
  await signIn(page);
  await page.goto('/admin/sections');
  const toggle = page.getByRole('switch', { name: /Shown on the website: Questions/ });
  try {
    await toggle.uncheck();
    await expect(page.getByText('Section hidden from the website.')).toBeVisible();
    const visitor = await browser.newPage();
    await visitor.goto('/en/');
    await expect(visitor.locator('#faq')).toHaveCount(0);
    await expect(visitor.locator('.site-nav a[href="#faq"]')).toHaveCount(0);
    await toggle.check();
    await expect(page.getByText('Section shown on the website.')).toBeVisible();
    await visitor.reload();
    await expect(visitor.locator('#faq')).toHaveCount(1);
    await visitor.close();
  } finally {
    // Never leave the website with a section hidden by a failed run.
    const { token } = (await (await page.request.get('/api/auth/antiforgery')).json()) as { token: string };
    await page.request.put('/api/admin/sections/faq/visibility', { data: { visible: true }, headers: { 'X-XSRF-TOKEN': token } });
  }
});

// ------------------------------------------------------------------ news

test('an article goes from draft to published to unpublished to deleted', async ({ page, browser }) => {
  const slug = `e2e-${stamp}`;
  await signIn(page);
  await page.goto('/admin/news/new');
  // One language at a time: the switch above the headline changes it.
  await page.locator('.lang-switch').getByRole('button', { name: /Arabic/ }).click();
  await page.getByLabel('The headline (Arabic)').fill(`خبر تجريبي ${stamp}`);
  await page.getByLabel('Article text (Arabic)').fill('نص الخبر **مهم**.');
  await page.locator('.lang-switch').getByRole('button', { name: /English/ }).click();
  await page.getByLabel('The headline (English)').fill(`Test story ${stamp}`);
  await page.getByLabel('Short summary (English)').fill('A short summary.');
  await page.getByText('More options').click();
  await page.getByLabel('Web address').fill(slug);
  await page.getByRole('button', { name: 'Save draft' }).click();
  await expect(page).toHaveURL(/\/admin\/news\/\d+$/);

  const visitor = await browser.newPage();
  const draft = await visitor.goto(`/en/news/${slug}`);
  expect(draft?.status()).toBe(404);

  await page.getByRole('button', { name: 'Publish', exact: true }).click();
  await expect(page.getByText('Published. It is on the website now.')).toBeVisible();
  await visitor.goto(`/news/${slug}`);
  await expect(visitor.locator('h1')).toContainText(`خبر تجريبي ${stamp}`);
  await expect(visitor.locator('.article-body strong')).toHaveText('مهم');
  await visitor.goto('/en/news/');
  await expect(visitor.getByText(`Test story ${stamp}`)).toBeVisible();

  await page.getByRole('button', { name: 'Unpublish' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Unpublish' }).click();
  await expect(page.getByText('Back to draft')).toBeVisible();
  const gone = await visitor.goto(`/en/news/${slug}`);
  expect(gone?.status()).toBe(404);

  await page.getByRole('button', { name: 'Delete' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click();
  await expect(page).toHaveURL(/\/admin\/news$/);
  await visitor.close();
});

// ------------------------------------------------------------------ media

test('a picture can be uploaded, described in both languages, and deleted', async ({ page }) => {
  await signIn(page);
  await page.goto('/admin/media');
  // a valid 1×1 PNG
  const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==', 'base64');
  await page.locator('input[type="file"]').first().setInputFiles({ name: `e2e-${stamp}.png`, mimeType: 'image/png', buffer: png });
  await expect(page.getByText('1 picture uploaded.')).toBeVisible();
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel('Arabic', { exact: true }).fill('صورة تجريبية');
  await dialog.getByLabel('English', { exact: true }).fill('Test picture');
  await dialog.getByRole('button', { name: 'Save' }).click();
  await expect(page.getByText('Descriptions saved.')).toBeVisible();
  await dialog.getByRole('button', { name: 'Delete' }).click();
  await page.getByRole('dialog', { name: /Delete/ }).getByRole('button', { name: 'Delete' }).click();
  await expect(page.getByText('Picture deleted.')).toBeVisible();

  // A disguised file is refused by the server.
  await page.locator('input[type="file"]').first().setInputFiles({ name: 'evil.png', mimeType: 'image/png', buffer: Buffer.from('<script>alert(1)</script>') });
  await expect(page.locator('.toast-error')).toBeVisible();
});

// ------------------------------------------------------------------ roles

test('an editor can edit content but not settings, people or the log', async ({ page }) => {
  const email = `editor-${stamp}@example.com`;
  const password = `Editor-Pass-${stamp}-2026`;
  await signIn(page);
  await page.goto('/admin/users');
  await page.getByRole('button', { name: 'Add a person' }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel('Email').fill(email);
  await dialog.getByLabel('Name').fill('E2E Editor');
  await dialog.getByLabel('Temporary password').fill(password);
  await dialog.getByRole('button', { name: 'Save' }).click();
  await expect(page.getByText('Account created.')).toBeVisible();
  await signOut(page);

  await signIn(page, email, password);
  await page.goto('/admin/settings');
  await expect(page.getByText('Only administrators can change site settings.')).toBeVisible();
  await page.goto('/admin/users');
  await expect(page.getByText("Your role can't change this.")).toBeVisible();
  expect((await page.request.get('/api/admin/users')).status()).toBe(403);
  expect((await page.request.get('/api/admin/audit')).status()).toBe(403);
  expect((await page.request.get('/api/admin/sections')).status()).toBe(200);
  await signOut(page);

  await signIn(page);
  await page.goto('/admin/users');
  const row = page.locator('tr', { hasText: email });
  await row.locator('summary').click();
  await row.getByRole('button', { name: 'Delete' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click();
  await expect(page.getByText('Account deleted.')).toBeVisible();
});
