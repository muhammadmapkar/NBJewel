// Role: Proves authenticated draft branch data cannot survive a transition back to public storefront mode.
import '@angular/compiler';
import { createEnvironmentInjector, EnvironmentInjector, ɵINJECTOR_SCOPE, provideZonelessChangeDetection, signal } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { expect, it } from 'vitest';
import { CatalogService } from '../catalog/catalog.service';
import { CustomerSession } from './customer-session';
import { StoreDirectoryService } from './store-directory.service';
it('partitions directory caches by business and by private preview versus live mode', async () => {
  const catalog = { shopSlug: signal('nb-jewel-demo'), preview: signal(false) };
  const injector = createEnvironmentInjector([provideZonelessChangeDetection(), { provide: ɵINJECTOR_SCOPE, useValue: 'root' }, StoreDirectoryService, {provide:CatalogService,useValue:catalog},{provide:CustomerSession,useValue:{base:'https://api.example.test'}},provideHttpClient(),provideHttpClientTesting()],null as unknown as EnvironmentInjector);
  const http=injector.get(HttpTestingController); const directory=injector.get(StoreDirectoryService);
  const row=(id:string)=>({location:{id,name:'Assumed branch',city:'Assumed city',latitude:25,longitude:55,source:'erp',isSample:true,erpEnabled:false,inventorySyncedAt:null,imagesSyncedAt:null,productIds:[]},productCount:0,liveSynced:false});
  try {
    const live=directory.load(); http.expectOne('https://api.example.test/api/shops/nb-jewel-demo/stores').flush([row('1')]); await live;
    catalog.preview.set(true); const draft=directory.load(); const request=http.expectOne('https://api.example.test/api/businesses/nb-jewel-demo/portal/stores'); expect(request.request.withCredentials).toBe(true); request.flush([row('2')]); await draft;
    catalog.preview.set(false); const again=directory.load(); expect(directory.stores()).toEqual([]); http.expectOne('https://api.example.test/api/shops/nb-jewel-demo/stores').flush([row('1')]); await again;
    expect(directory.stores().map(x=>x.location.id)).toEqual(['1']);
  } finally { http.verify(); injector.destroy(); }
});
