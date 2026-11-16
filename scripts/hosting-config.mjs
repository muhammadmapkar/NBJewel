// Role: Validates assumed single-origin hosting settings and writes public Angular/Vercel artifacts without reading secrets or changing local defaults.
import { cp, mkdir, readFile, rename, stat, writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import { randomUUID } from 'node:crypto';

const apps = { erp: { directory: 'frontend', output: 'nbjewel-frontend', baseHref: '/erp/' }, storefront: { directory: 'customer-tryon', output: 'customer-tryon', baseHref: '/' } };
function origin(value, name) {
  try {
    if (typeof value !== 'string' || !value || /\s/.test(value)) throw new Error();
    const url = new URL(value);
    const loopback = url.hostname === 'localhost' || url.hostname === '[::1]' || /^127\.\d+\.\d+\.\d+$/.test(url.hostname);
    if ((url.protocol !== 'https:' && !(url.protocol === 'http:' && loopback)) || url.username || url.password || url.pathname !== '/' || url.search || url.hash) throw new Error();
    return url.origin;
  } catch { throw new Error(`${name} must be an HTTPS origin without a path, credentials, query or fragment (HTTP loopback is allowed for local verification).`); }
}
export function settingsFor(app, env) {
  if (!Object.hasOwn(apps, app)) throw new Error('Choose erp or storefront.');
  const publicOrigin = origin(env.NBJEWEL_PUBLIC_ORIGIN ?? env.NBJEWEL_API_URL, 'NBJEWEL_PUBLIC_ORIGIN');
  if (env.NBJEWEL_API_URL !== undefined && origin(env.NBJEWEL_API_URL, 'NBJEWEL_API_URL') !== publicOrigin) throw new Error('NBJEWEL_API_URL must match the public origin for single-origin hosting.');
  const settings = { ...apps[app], publicOrigin };
  if (app === 'storefront') {
    settings.apiUpstream = origin(env.NBJEWEL_API_UPSTREAM, 'NBJEWEL_API_UPSTREAM');
    settings.erpUpstream = origin(env.NBJEWEL_ERP_UPSTREAM, 'NBJEWEL_ERP_UPSTREAM');
    if (settings.apiUpstream === publicOrigin || settings.erpUpstream === publicOrigin) throw new Error('Upstream origins must differ from the public origin to avoid proxy loops.');
  }
  return settings;
}
export function outputConfig(app, settings) {
  const noStore = { 'Cache-Control': 'no-store' };
  const routes = app === 'storefront' ? [
    { src: '^/erp$', status: 307, headers: { Location: '/erp/sign-in', ...noStore } },
    { src: '^/erp/(.*)$', dest: `${settings.erpUpstream}/erp/$1`, headers: noStore },
    { src: '^/api/(.*)$', dest: `${settings.apiUpstream}/api/$1`, headers: noStore },
    { src: '^/jewelry-images/(.*)$', dest: `${settings.apiUpstream}/jewelry-images/$1`, headers: noStore },
    { src: '^/storefront-config\\.json$', headers: noStore, continue: true }
  ] : [
    { src: '^/(?:erp)?$', status: 307, headers: { Location: '/erp/sign-in', ...noStore } },
    { src: '^/erp/(?:portal-config\\.json|index\\.html)$', headers: noStore, continue: true }
  ];
  routes.push({ handle: 'filesystem' });
  // A missing script/image/config is a missing file, never an HTML response with a successful status.
  routes.push({ src: app === 'erp' ? '^/erp/.*\\.[^/]+$' : '^/.*\\.[^/]+$', status: 404 });
  routes.push({ src: app === 'erp' ? '^/erp/.*$' : '^/.*$', dest: app === 'erp' ? '/erp/index.html' : '/index.html', headers: noStore });
  return { version: 3, routes };
}
export async function prepareBuild(app, root, env) {
  const settings = settingsFor(app, env); const directory = join(root, settings.directory);
  const generated = join(directory, '.hosting/public'); await mkdir(generated, { recursive: true });
  const json = app === 'erp' ? { storefrontBaseUrl: settings.publicOrigin } : { apiBaseUrl: settings.publicOrigin, portalApiBaseUrl: settings.publicOrigin };
  await writeFile(join(generated, app === 'erp' ? 'portal-config.json' : 'storefront-config.json'), JSON.stringify(json, null, 2) + '\n');
  if (app === 'erp') {
    const environments = join(directory, 'src/environments'); await mkdir(environments, { recursive: true });
    await writeFile(join(environments, 'environment.deploy.ts'), `// Role: Generated nonsecret hosted API address. Local development uses environment.ts.\nexport const environment = { apiBaseUrl: ${JSON.stringify(settings.publicOrigin)} };\n`);
  }
  return settings;
}
export async function writeOutput(app, root, settings) {
  const directory = join(root, settings.directory); const browser = join(directory, 'dist', settings.output, 'browser');
  const index = await readFile(join(browser, 'index.html'), 'utf8');
  if (!index.includes(`<base href="${settings.baseHref}">`)) throw new Error('The compiled app has the wrong hosting base path.');
  const vercel = join(directory, '.vercel'); const next = join(vercel, 'output-next-' + randomUUID()); await mkdir(next, { recursive: true });
  await cp(browser, join(next, 'static', ...(app === 'erp' ? ['erp'] : [])), { recursive: true });
  await writeFile(join(next, 'config.json'), JSON.stringify(outputConfig(app, settings), null, 2) + '\n');
  const output = join(vercel, 'output');
  if (await stat(output).then(() => true, error => { if (error.code === 'ENOENT') return false; throw error; })) {
    // Retain previous generated artifacts instead of deleting files during this owner-controlled workflow.
    await rename(output, join(vercel, 'previous-output-' + randomUUID()));
  }
  await rename(next, output);
}
