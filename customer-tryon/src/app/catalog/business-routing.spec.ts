// Role: Proves legacy API-only storefront links map to the demo portal with their valid product, store and try-on context.
import '@angular/compiler';
import { DefaultUrlSerializer } from '@angular/router';
import { expect, it } from 'vitest';
import { legacyShopUrl } from './business-routing';
const serializer = new DefaultUrlSerializer();
it('keeps ERP product, store and parts context under the demo slug', () => { expect(serializer.serialize(legacyShopUrl('/try/7?source=erp&store=1&parts=necklace'))).toBe('/shops/nb-jewel-demo/try/7?store=1&parts=necklace'); });
it('drops obsolete demo store IDs while preserving product ID and parts', () => { expect(serializer.serialize(legacyShopUrl('/p/7?source=demo&store=old&parts=earrings'))).toBe('/shops/nb-jewel-demo/p/7?parts=earrings'); });
it('maps home and source-free legacy URLs to the API-only demo portal', () => { expect(serializer.serialize(legacyShopUrl('/'))).toBe('/shops/nb-jewel-demo'); expect(serializer.serialize(legacyShopUrl('/bag?store=2'))).toBe('/shops/nb-jewel-demo/bag?store=2'); });
