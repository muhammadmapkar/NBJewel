// Role: Configures customer storefront unit tests with CommonJS syntax matching this package.
// Run with: npm test
module.exports = {
  test: { include: ['src/**/*.spec.ts'], environment: 'node' }
};
