// Role: Checks assumed hosted origin boundaries and generated deployment files against literal expected public routes/configuration.
import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, mkdir, readFile, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { settingsFor, prepareBuild, outputConfig, writeOutput } from '../hosting-config.mjs';

const env = { NBJEWEL_PUBLIC_ORIGIN: 'https://shop.example.test', NBJEWEL_API_UPSTREAM: 'https://api.example.test', NBJEWEL_ERP_UPSTREAM: 'https://erp.example.test' };
test('gateway sends API and ERP requests to their own upstream without losing the public API prefix', () => {
  const config = outputConfig('storefront', settingsFor('storefront', env));
  const resolve = path => { const route = config.routes.find(r => r.src && new RegExp(r.src).test(path)); return route?.dest ? path.replace(new RegExp(route.src), route.dest) : null; };
  assert.equal(resolve('/api/shops/nb-jewel-demo/products?storeId=1'), 'https://api.example.test/api/shops/nb-jewel-demo/products?storeId=1');
  assert.equal(resolve('/erp/catalog/7?tab=images'), 'https://erp.example.test/erp/catalog/7?tab=images');
  assert.equal(resolve('/erp/portal-config.json'), 'https://erp.example.test/erp/portal-config.json');
  assert.equal(resolve('/jewelry-images/sample.png'), 'https://api.example.test/jewelry-images/sample.png');
  assert.equal(config.routes.find(r => r.src === '^/erp$')?.headers?.Location, '/erp/sign-in');
  assert.equal(config.routes.find(r => r.src === '^/api/(.*)$')?.headers?.['Cache-Control'], 'no-store');
  assert.equal(config.routes.findIndex(r => r.handle === 'filesystem') > config.routes.findIndex(r => r.src === '^/erp/(.*)$'), true);
  assert.equal(config.routes.at(-1).dest, '/index.html');
});
test('ERP static files live beneath /erp while only navigations fall back to its own index', async () => {
  const root = await mkdtemp(join(tmpdir(), 'nbjewel-hosting-output-'));
  await mkdir(join(root, 'frontend/dist/nbjewel-frontend/browser'), { recursive: true });
  await writeFile(join(root, 'frontend/dist/nbjewel-frontend/browser/index.html'), '<base href="/erp/">');
  await writeFile(join(root, 'frontend/dist/nbjewel-frontend/browser/main.js'), 'real app fixture');
  const settings = settingsFor('erp', env); await writeOutput('erp', root, settings);
  assert.equal(await readFile(join(root, 'frontend/.vercel/output/static/erp/main.js'), 'utf8'), 'real app fixture');
  const config = JSON.parse(await readFile(join(root, 'frontend/.vercel/output/config.json'), 'utf8'));
  assert.equal(config.version, 3);
  const fallbacks = config.routes.filter(r => r.dest);
  assert.equal(fallbacks.at(-1).dest, '/erp/index.html');
  assert.equal(config.routes.find(r => r.src === '^/erp/.*\\.[^/]+$')?.status, 404);
});
test('hosted settings reject absent, malformed, cross-origin browser API and self-proxy configurations', () => {
  assert.throws(() => settingsFor('erp', {}));
  for (const origin of ['http://shop.example.test', 'https://user:pass@shop.example.test', 'https://shop.example.test/path', 'https://shop.example.test?token=secret', 'https://shop.example.test/#fragment', 'ftp://shop.example.test', 'https://shop.example.test/\n']) {
    assert.throws(() => settingsFor('erp', { NBJEWEL_PUBLIC_ORIGIN: origin }));
  }
  assert.throws(() => settingsFor('erp', { ...env, NBJEWEL_API_URL: 'https://api.example.test' }));
  assert.throws(() => settingsFor('storefront', { NBJEWEL_PUBLIC_ORIGIN: env.NBJEWEL_PUBLIC_ORIGIN }));
  assert.throws(() => settingsFor('storefront', { ...env, NBJEWEL_API_UPSTREAM: env.NBJEWEL_PUBLIC_ORIGIN }));
  assert.throws(() => settingsFor('storefront', { ...env, NBJEWEL_ERP_UPSTREAM: env.NBJEWEL_PUBLIC_ORIGIN }));
});
test('loopback origins support isolated combined-host browser verification and the existing API URL alias is accepted', () => {
  assert.equal(settingsFor('erp', { NBJEWEL_API_URL: 'https://shop.example.test/' }).publicOrigin, 'https://shop.example.test');
  assert.equal(settingsFor('erp', { NBJEWEL_PUBLIC_ORIGIN: 'http://127.0.0.1:4301' }).publicOrigin, 'http://127.0.0.1:4301');
  assert.throws(() => settingsFor('erp', { NBJEWEL_PUBLIC_ORIGIN: 'http://127.0.0.1.evil.test' }));
});
test('generated hosted files expose only the public origin and leave local runtime files unchanged', async () => {
  const root = await mkdtemp(join(tmpdir(), 'nbjewel-hosting-config-'));
  for (const app of ['frontend', 'customer-tryon']) {
    await mkdir(join(root, app, 'public'), { recursive: true });
    await writeFile(join(root, app, 'public', app === 'frontend' ? 'portal-config.json' : 'storefront-config.json'), '{"local":"retained"}');
  }
  const inputs = { ...env, GEMINI_API_KEY: 'must-never-be-written', NBJEWEL_IDENTITY_CONNECTION: 'must-never-be-written' };
  const erp = await prepareBuild('erp', root, inputs); const shop = await prepareBuild('storefront', root, inputs);
  assert.equal(erp.baseHref, '/erp/'); assert.equal(shop.baseHref, '/');
  assert.deepEqual(JSON.parse(await readFile(join(root, 'frontend/.hosting/public/portal-config.json'), 'utf8')), { storefrontBaseUrl: 'https://shop.example.test' });
  assert.deepEqual(JSON.parse(await readFile(join(root, 'customer-tryon/.hosting/public/storefront-config.json'), 'utf8')), { apiBaseUrl: 'https://shop.example.test', portalApiBaseUrl: 'https://shop.example.test' });
  const generated = await readFile(join(root, 'frontend/src/environments/environment.deploy.ts'), 'utf8');
  assert.match(generated, /apiBaseUrl: "https:\/\/shop.example.test"/); assert.doesNotMatch(generated, /must-never-be-written|api\.example|erp\.example/);
  assert.equal(await readFile(join(root, 'frontend/public/portal-config.json'), 'utf8'), '{"local":"retained"}');
  assert.equal(await readFile(join(root, 'customer-tryon/public/storefront-config.json'), 'utf8'), '{"local":"retained"}');
});
test('gateway forwards only named GET health probes before the storefront fallback without caching', () => {
  const config = outputConfig('storefront', settingsFor('storefront', env));
  const resolve = (path, method = 'GET') => { const route = config.routes.find(r => r.src && (!r.methods || r.methods.includes(method)) && new RegExp(r.src).test(path)); return route?.dest ? path.replace(new RegExp(route.src), route.dest) : null; };
  assert.equal(resolve('/health/live'), 'https://api.example.test/health/live');
  assert.equal(resolve('/health/ready/core'), 'https://api.example.test/health/ready/core');
  assert.equal(resolve('/health/ready'), '/index.html');
  assert.equal(resolve('/health/arbitrary'), '/index.html');
  assert.equal(resolve('/health/ready/core', 'POST'), '/index.html');
  assert.equal(config.routes.find(r => r.src === '^/health/ready/core$')?.headers?.['Cache-Control'], 'no-store');
});
