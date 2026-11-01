// Role: Settings for the customer app unit tests (Vitest): the pure try-on math and physics.
// Run with: npm test
import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: { include: ['src/**/*.spec.ts'], environment: 'node' }
});
