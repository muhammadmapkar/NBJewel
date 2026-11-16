// Role: Preserves the old explicit ERP configuration-generation command using validated single-origin hosted settings.
import { fileURLToPath } from 'node:url';
import { prepareBuild } from '../../scripts/hosting-config.mjs';
await prepareBuild('erp', fileURLToPath(new URL('../../', import.meta.url)), process.env);
