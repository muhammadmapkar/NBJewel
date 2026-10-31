// Role: Settings for the frontend unit tests (Vitest). Only pure TypeScript specs under src run
// here, for example the try-on overlay math. Run with: npm test
import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: { include: ['src/**/*.spec.ts'], environment: 'node' }
});
