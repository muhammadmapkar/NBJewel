// Role: Builds a hosted Angular app and emits the standard Vercel static/routing output using explicit nonsecret settings.
import { fileURLToPath } from 'node:url';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';
import { prepareBuild, writeOutput } from './hosting-config.mjs';

try {
  const root = fileURLToPath(new URL('../', import.meta.url)); const app = process.argv[2];
  const settings = await prepareBuild(app, root, process.env); const directory = join(root, settings.directory);
  const result = spawnSync(process.execPath, [join(directory, 'node_modules/@angular/cli/bin/ng.js'), 'build', '--configuration', 'deploy'], { cwd: directory, stdio: 'inherit', env: process.env });
  if (result.error) throw new Error('Could not start the Angular build.');
  if (result.status !== 0) process.exit(result.status ?? 1);
  await writeOutput(app, root, settings);
  console.log(`Hosted ${app} build prepared at ${settings.directory}/.vercel/output, base ${settings.baseHref}.`);
} catch (error) { console.error(error.message); process.exitCode = 1; }
