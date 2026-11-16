// Role: Verifies portal preview configuration follows the ERP document base, keeping hosted /erp requests outside the public storefront fallback.
import '@angular/compiler';
import { DOCUMENT } from '@angular/common';
import { createEnvironmentInjector, EnvironmentInjector, provideZonelessChangeDetection, ɵINJECTOR_SCOPE } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { expect, it } from 'vitest';
import { PortalAdminService } from './portal-admin.service';
it('loads preview configuration from the /erp base and keeps the public storefront origin', () => {
  const injector = createEnvironmentInjector([provideZonelessChangeDetection(), { provide: ɵINJECTOR_SCOPE, useValue: 'root' }, PortalAdminService, provideHttpClient(), provideHttpClientTesting(), { provide: DOCUMENT, useValue: { baseURI: 'https://shop.example.test/erp/' } }], null as unknown as EnvironmentInjector);
  const http = injector.get(HttpTestingController); let result = '';
  try {
    const service = injector.get(PortalAdminService); service.configuration().subscribe(value => result = value);
    http.expectOne('https://shop.example.test/erp/portal-config.json').flush({ storefrontBaseUrl: 'https://shop.example.test' });
    expect(result).toBe('https://shop.example.test'); expect(service.previewUrl('nb-jewel-demo')).toBe('https://shop.example.test/shops/nb-jewel-demo/preview'); http.verify();
  } finally { injector.destroy(); }
});
