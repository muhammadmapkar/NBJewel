// Role: Keeps five customer destinations within thumb reach below 768px while preserving the camera's own controls.
import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { CatalogService } from '../catalog/catalog.service';
import { IconComponent } from './icon.component';
import { StoreService } from '../catalog/store.service';
@Component({ selector: 'ct-mobile-nav', standalone: true, imports: [RouterLink, RouterLinkActive, IconComponent], template: `
@if (!router.url.startsWith('/try/')) { <nav aria-label="Mobile navigation">
<a routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }"><ct-icon name="home" />Home</a>
<a routerLink="/stores" routerLinkActive="active"><ct-icon name="location" />Stores</a>
<a [routerLink]="catalog.products()[0] ? ['/try', catalog.products()[0].id] : ['/shop']" [queryParams]="catalog.linkParams()"><ct-icon name="camera" />Try-On</a>
<a routerLink="/bag" [queryParams]="catalog.linkParams()" routerLinkActive="active"><ct-icon name="bag" />Bag ({{ store.bagCount() }})</a>
<a routerLink="/profile" [queryParams]="catalog.linkParams()" routerLinkActive="active"><ct-icon name="profile" />Profile</a></nav> }`, styles: [`
nav { display:none; } @media(max-width:767px) { nav { display:grid; grid-template-columns:repeat(5,1fr); position:fixed; bottom:0; left:0; right:0; z-index:30; padding:6px 4px calc(6px + env(safe-area-inset-bottom)); background:var(--ivory); border-top:1px solid var(--line); box-shadow:0 -4px 20px #2b1a1408; } a { display:grid; gap:2px; place-items:center; min-height:52px; color:var(--muted); font-size:11px; text-decoration:none; border-radius:12px; } span { font-size:23px; line-height:24px; } .active { color:var(--maroon); background:var(--cream); font-weight:700; } }
`] })
export class MobileNavComponent { readonly router = inject(Router); readonly catalog = inject(CatalogService); readonly store = inject(StoreService); }
