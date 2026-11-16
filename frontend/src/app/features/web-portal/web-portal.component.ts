// Role: Edits the assumed fixed business portal using the existing ERP shell/widget, preserving local edits after revision conflicts.
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { firstValueFrom, forkJoin } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { WidgetCardComponent } from '../../shared/ui/widget-card.component';
import { PORTAL_FLAGS, PortalAdminService, PortalChoices, PortalDraft, PortalSettings } from './portal-admin.service';
@Component({ selector:'nb-web-portal', standalone:true, imports:[FormsModule, WidgetCardComponent], template:`
<h1>Web Portal</h1><p>Manage the saved draft for {{ auth.identity()?.business?.name }}.</p>
@if (notice()) { <p role="status">{{ notice() }}</p> }
@if (settings; as s) {
<nb-widget-card title="Portal draft"><form (ngSubmit)="save()"><fieldset [disabled]="busy()">
<label>Business name<input name="name" [(ngModel)]="s.name" maxlength="120" (ngModelChange)="dirty=true" /></label>
<label>Color preset<select name="color" [(ngModel)]="s.colorPreset" (ngModelChange)="dirty=true"><option value="gold">Gold</option><option value="silver">Silver</option><option value="rose">Rose</option></select></label>
@for (field of texts; track field.key) { <label>{{field.label}}<textarea [name]="field.key" [(ngModel)]="s[field.key]" (ngModelChange)="dirty=true" [maxLength]="field.max"></textarea></label> }
@for (flag of flags; track flag.key) { <label class="check"><input type="checkbox" [name]="flag.key" [(ngModel)]="s[flag.key]" (ngModelChange)="dirty=true" />{{flag.label}}</label> }
<label>Logo PNG/JPG<input type="file" accept="image/png,image/jpeg" (change)="upload($event,'logoAssetId')" /></label>
<label>Banner PNG/JPG<input type="file" accept="image/png,image/jpeg" (change)="upload($event,'bannerAssetId')" /></label>
<h2>Visible branches</h2>@for (branch of choices.branches; track branch.id) { <label class="check"><input type="checkbox" [checked]="s.visibleBranchIds.includes(branch.id)" (change)="toggleId('visibleBranchIds',branch.id)" />{{branch.name}}</label> }
<h2>Featured designs (maximum 12)</h2>@for (product of choices.products; track product.id) { <label class="check"><input type="checkbox" [checked]="s.featuredItemIds.includes(product.id)" (change)="toggleId('featuredItemIds',product.id)" />{{product.name}}</label> }
<h2>Visible collections</h2><p>An empty selection shows all collections.</p>@for (collection of choices.collections; track collection) { <label class="check"><input type="checkbox" [checked]="s.visibleCollections.includes(collection)" (change)="toggleCollection(collection)" />{{collection}}</label> }
</fieldset><p role="status">{{ validation() }}</p><button type="submit" [disabled]="busy()">Save draft</button></form></nb-widget-card>
@if (conflict()) { <nb-widget-card title="Draft changed"><p>Another editor saved changes. Reload the latest draft or keep your changed fields for review.</p><button (click)="load()">Reload latest</button><button (click)="keepEdits()">Keep my edits</button></nb-widget-card> }
<nb-widget-card title="Preview and publication"><p>{{ online ? 'Online' : 'Offline' }}. Draft revision {{revision}}. @if(dirty) { Save the draft before previewing or publishing. }</p>
<button (click)="preview()" [disabled]="busy() || dirty">Preview saved draft</button><button (click)="frame(1024)" [disabled]="busy() || dirty">Desktop preview</button><button (click)="frame(390)" [disabled]="busy() || dirty">Mobile preview</button>
<button (click)="publish()" [disabled]="!canPublish()">Publish</button><button (click)="offline()" [disabled]="busy() || !hasPublishPermission()">Take offline</button>
@if (frameUrl; as url) { <div style="overflow:auto"><iframe title="Saved draft preview" [src]="url" [style.width.px]="frameWidth" style="height:640px;max-width:100%;border:0"></iframe></div> }
</nb-widget-card>
}`, styles:[`fieldset { border:0; padding:0; display:grid; grid-template-columns:repeat(auto-fit,minmax(240px,1fr)); gap:14px; } label { display:grid; gap:6px; } input,select,textarea { border:1px solid var(--gold); border-radius:5px; padding:8px; } .check { display:flex; align-items:center; } button { margin:8px 8px 8px 0; padding:9px 14px; border:1px solid var(--gold); border-radius:5px; background:var(--widget); color:var(--gold); } h2 { grid-column:1/-1; } nb-widget-card { display:block; margin:16px 0; }`] })
export class WebPortalComponent {
  readonly auth = inject(AuthService); private readonly api = inject(PortalAdminService); private readonly sanitizer = inject(DomSanitizer, { optional:true });
  readonly flags = PORTAL_FLAGS; readonly busy = signal(false); readonly notice = signal(''); readonly conflict = signal(false);
  readonly texts: { key:'headline'|'introduction'|'story'|'phone'|'whatsApp'|'email'|'openingHours'; label:string; max:number }[] = [{key:'headline',label:'Headline',max:200},{key:'introduction',label:'Introduction',max:2000},{key:'story',label:'Shop story',max:5000},{key:'phone',label:'Phone',max:40},{key:'whatsApp',label:'WhatsApp number',max:40},{key:'email',label:'Email',max:254},{key:'openingHours',label:'Opening hours',max:500}];
  settings: PortalSettings | null = null; private baseline: PortalSettings | null = null; revision=0; online=false; dirty=false;
  choices: PortalChoices = { branches:[], products:[], collections:[] }; frameUrl: SafeResourceUrl | null = null; frameWidth=1024;
  private get slug(): string | null { return this.auth.identity()?.business?.slug ?? null; }
  constructor() { this.load(); }
  private apply(draft: PortalDraft): void { this.revision=draft.revision; this.online=draft.online; this.settings=structuredClone(draft.settings); this.baseline=structuredClone(draft.settings); this.dirty=false; this.conflict.set(false); }
  load(): void {
    const slug=this.slug; if(!slug || !this.auth.identity()?.permissions.includes('portal.manage')) { this.notice.set('Portal management is unavailable for this account.'); return; }
    this.busy.set(true); forkJoin({ draft:this.api.draft(slug), choices:this.api.choices(slug), config:this.api.configuration() }).subscribe({ next: result=>{ this.apply(result.draft); this.choices=result.choices; this.busy.set(false); }, error:error=>{ this.failed(error); this.busy.set(false); } });
  }
  hasPublishPermission(): boolean { return this.auth.identity()?.permissions.includes('portal.publish') === true; }
  validation(): string { return this.settings?.enquiries && !/^\+[1-9][0-9]{7,14}$/.test(this.settings.whatsApp) ? 'Enter a WhatsApp number in E.164 format when enquiries are enabled.' : ''; }
  canPublish(): boolean { return !!this.settings && this.hasPublishPermission() && !this.busy() && !this.dirty && !this.validation(); }
  private failed(error: { status?:number; error?:{message?:string} }): void { this.conflict.set(error?.status===409); this.notice.set(error?.status===409 ? 'The draft changed. Reload it or keep your changed fields before saving again.' : error?.error?.message ?? 'Portal request failed. Please try again.'); }
  async save(): Promise<boolean> { const slug=this.slug; if(!slug || !this.settings || this.busy()) return false; this.busy.set(true); try { this.apply(await firstValueFrom(this.api.save(slug,this.revision,this.settings))); this.notice.set('Draft saved.'); return true; } catch(error) { this.failed(error as {status?:number}); return false; } finally { this.busy.set(false); } }
  async publish(): Promise<boolean> { const slug=this.slug; if(!slug || !this.canPublish()) return false; this.busy.set(true); try { this.apply(await firstValueFrom(this.api.publish(slug,this.revision))); this.notice.set('Portal published.'); return true; } catch(error) { this.failed(error as {status?:number}); return false; } finally { this.busy.set(false); } }
  async offline(): Promise<boolean> { const slug=this.slug; if(!slug || !this.hasPublishPermission() || this.busy()) return false; this.busy.set(true); try { const draft=await firstValueFrom(this.api.offline(slug,this.revision)); this.online=draft.online; this.notice.set('Portal is offline.'); return true; } catch(error) { this.failed(error as {status?:number}); return false; } finally { this.busy.set(false); } }
  async keepEdits(): Promise<void> { const slug=this.slug; if(!slug || !this.settings || !this.baseline) return; const local=structuredClone(this.settings); const baseline=this.baseline; this.busy.set(true); try { const latest=await firstValueFrom(this.api.draft(slug)); const merged={...latest.settings} as unknown as Record<string,unknown>; for(const key of Object.keys(baseline) as (keyof PortalSettings)[]) if(JSON.stringify(local[key])!==JSON.stringify(baseline[key])) merged[key]=local[key]; this.apply(latest); this.settings=merged as unknown as PortalSettings; this.dirty=true; this.notice.set('Latest revision loaded. Your changed fields are kept; review before saving.'); } catch(error) { this.failed(error as {status?:number}); } finally { this.busy.set(false); } }
  preview(): void { if(this.slug && !this.dirty && !this.busy()) window.open(this.api.previewUrl(this.slug),'_blank','noopener,noreferrer'); }
  frame(width:number): void { if(this.slug && !this.dirty && this.sanitizer) { this.frameWidth=width; this.frameUrl=this.sanitizer.bypassSecurityTrustResourceUrl(this.api.previewUrl(this.slug) + '?revision=' + this.revision); } }
  toggleId(key:'visibleBranchIds'|'featuredItemIds',id:number): void { if(!this.settings) return; const values=this.settings[key]; if(!values.includes(id) && key==='featuredItemIds' && values.length>=12) { this.notice.set('Choose up to 12 featured designs.'); return; } this.settings[key]=values.includes(id)?values.filter(value=>value!==id):[...values,id]; this.dirty=true; }
  toggleCollection(name:string): void { if(this.settings) { this.settings.visibleCollections=this.settings.visibleCollections.includes(name)?this.settings.visibleCollections.filter(value=>value!==name):[...this.settings.visibleCollections,name]; this.dirty=true; } }
  async upload(event:Event,key:'logoAssetId'|'bannerAssetId'): Promise<void> { const file=(event.target as HTMLInputElement).files?.[0]; const slug=this.slug; if(!file || !slug || !this.settings) return; if(file.size>5*1024*1024 || !['image/png','image/jpeg'].includes(file.type)) { this.notice.set('Choose a PNG or JPG image of at most 5 MB.'); return; } this.busy.set(true); try { this.settings[key]=(await firstValueFrom(this.api.upload(slug,file))).id; this.dirty=true; this.notice.set('Image uploaded. Save the draft before previewing it.'); } catch(error) { this.failed(error as {status?:number}); } finally { this.busy.set(false); } }
}
