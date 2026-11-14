import assert from 'node:assert/strict';
import { randomBytes } from 'node:crypto';

const { chromium } = await import(process.env.PLAYWRIGHT_MODULE ?? 'playwright');
const frontend = process.env.FLEETPROOF_TEST_WEB ?? 'http://127.0.0.1:5179';
const backend = process.env.FLEETPROOF_TEST_API ?? 'http://127.0.0.1:5080/api/v1';
for (const url of [frontend, backend])
  assert.ok(['127.0.0.1', 'localhost'].includes(new URL(url).hostname), 'Run only against local test services');
const browser = await chromium.launch({
  ...(process.env.CHROME_PATH ? { executablePath: process.env.CHROME_PATH } : {}),
});
try {
  const page = await browser.newPage();
  page.setDefaultTimeout(10000);
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  page.on('response', response => {
    if (response.url().startsWith(backend + '/') && !response.ok())
      errors.push(`${response.status()} ${response.url()}`);
  });
  const name = 'vue_' + randomBytes(5).toString('hex');
  const password = randomBytes(18).toString('hex');
  const submit = async (path, status, action) => {
    const pending = page.waitForResponse(response => response.url() === backend + path && response.request().method() === 'POST');
    await action();
    const response = await pending;
    assert.equal(response.status(), status, path);
    return response.json();
  };

  await page.goto(frontend + '/register');
  await page.locator('#new-username').fill(name);
  await page.locator('#new-password').fill(password);
  await submit('/authentication/sign-up', 201, () => page.locator('button[type=submit]').click());
  await page.waitForURL('**/login');
  await page.locator('#username').fill(name);
  await page.locator('#password').fill(password);
  await submit('/authentication/sign-in', 200, () => page.locator('button[type=submit]').click());
  await page.waitForURL('**/app/dashboard');

  await page.goto(frontend + '/app/subscriptions');
  const card = plan => page.locator(`[data-plan=${plan}]`);
  const choose = plan => card(plan).getByRole('button', { name: 'Elegir plan' }).click();
  const verifyPrices = async amounts => {
    for (const [plan, amount] of Object.entries(amounts)) {
      const price = await card(plan).locator('.plan-price strong').textContent();
      assert.equal(price.replace(/\D/g, ''), String(amount));
    }
  };
  assert.equal(await page.locator('[data-plan]').count(), 3);
  await verifyPrices({weekly:49, monthly:149, annual:1390});
  await page.getByRole('radio', { name: 'Particular' }).check();
  await verifyPrices({weekly:9, monthly:25, annual:190});
  await choose('annual');
  await page.getByText('Taypi · Simulación', { exact: true }).waitFor();
  await page.getByRole('img', { name: 'QR de demostración, no válido para pagos' }).waitFor();
  await page.getByText('Particular · Plan Anual · S/ 190', { exact: true }).waitFor();
  await page.getByRole('dialog').getByRole('button', { name: 'Cancelar', exact: true }).click();
  const token = await page.evaluate(() => localStorage.getItem('fleetproof-token'));
  const current = await page.request.get(backend + '/subscriptions', { headers: { Authorization: `Bearer ${token}` } });
  assert.equal(current.status(), 200);
  assert.deepEqual(await current.json(), []);
  await choose('annual');
  const plan = await submit('/subscriptions', 201, () => page.getByRole('button', { name: 'Confirmar pago' }).click());
  assert.equal(plan.status, 'active');
  assert.equal(plan.plan, 'annual');
  assert.equal(plan.audience, 'personal');
  assert.equal(plan.paymentStatus, 'completed');
  await page.getByText('Plan activado.').waitFor();
  await page.reload();
  await card('annual').getByText('Activo', { exact: true }).waitFor();
  assert.ok(await page.getByRole('radio', { name: 'Particular' }).isChecked());

  await page.goto(frontend + '/app/vehicles');
  await page.getByRole('button', { name: 'Agregar vehículo' }).click();
  await page.locator('#plate').fill('VUE-123');
  await page.locator('#type').fill('Auto');
  await page.locator('#site').fill('Lima');
  await page.locator('#owner').fill('Cuenta de prueba');
  const vehicle = await submit('/vehicles', 201, () => page.getByRole('button', { name: 'Guardar', exact: true }).click());
  await page.getByRole('cell', { name: 'VUE-123', exact: true }).waitFor();

  await page.getByRole('tab', { name: 'Flotas' }).click();
  await page.getByLabel('Nombre de flota').fill('Flota AV2');
  const fleet = await submit('/fleets', 201, () => page.getByRole('button', { name: /crear flota|registrar flota/i }).click());
  await page.getByRole('combobox', { name: 'Flotas', exact: true }).click();
  await page.getByRole('option', { name: fleet.name }).click();
  await page.getByRole('combobox', { name: 'Placa', exact: true }).click();
  await page.getByRole('option', { name: vehicle.plate }).click();
  await page.getByLabel('Responsable').fill('Operador QA');
  const assignment = await submit('/assignments', 201, () => page.getByRole('button', { name: 'Asignar vehículo' }).click());
  assert.equal(assignment.fleetId, fleet.id);
  assert.equal(assignment.vehicleId, vehicle.id);
  await page.getByRole('row').filter({ hasText: /Flota AV2.*VUE-123.*Operador QA/ }).waitFor();

  await page.goto(frontend + '/app/vehicles?tab=fleets');
  await page.getByRole('row').filter({ hasText: /Flota AV2.*VUE-123.*Operador QA/ }).waitFor();
  if (process.env.FLEETPROOF_TEST_SCREENSHOT)
    await page.screenshot({ path: process.env.FLEETPROOF_TEST_SCREENSHOT, fullPage: true });
  assert.deepEqual(errors, []);
  console.log('OK: Vue registration, login, QR demo, active plan, vehicle, fleet and assignment after reload.');
} finally {
  await browser.close();
}
