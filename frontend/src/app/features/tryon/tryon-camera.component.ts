// Role: The camera try-on page (/tryon/:id). Phase 2 placeholder: it loads the chosen item and
// shows it, with a way back to the grid. The live camera overlay is added in phase 3.

import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { switchMap } from 'rxjs';
import { JewelryItem } from './jewelry.models';
import { JewelryService } from './jewelry.service';

@Component({
  selector: 'nb-tryon-camera', standalone: true, imports: [RouterLink],
  template: `
    <header class="brand-bar"><a class="back" routerLink="/tryon" aria-label="Back to jewelry">‹</a><span class="title">{{ item()?.name ?? 'Try-On' }}</span></header>
    <main class="page">
      @if (missing()) {
        <div class="card message">This piece is not available any more. <a class="link" routerLink="/tryon">Back to jewelry</a></div>
      } @else if (item(); as current) {
        <div class="card message"><img [src]="jewelry.imageSrc(current)" [alt]="current.name" /><p>Camera try-on is coming in the next step.</p></div>
      }
    </main>
  `,
  styles: [`
    :host { display:block; min-height:100vh; background:var(--page); }
    .brand-bar { height:58px; display:flex; align-items:center; gap:14px; padding:0 16px; color:white; background:var(--sidebar); }
    .back { color:white; text-decoration:none; font-size:30px; line-height:1; }
    .title { font-weight:700; }
    .page { max-width:960px; margin:0 auto; padding:16px; }
    .card { background:white; padding:16px; border-radius:8px; }
    .message { padding:40px 16px; text-align:center; color:var(--muted); }
    .message img { max-width:60%; max-height:200px; }
    .link { color:var(--gold); font-weight:700; }
  `]
})
export class TryonCameraComponent {
  readonly jewelry = inject(JewelryService);
  private readonly route = inject(ActivatedRoute);
  readonly item = signal<JewelryItem | null>(null);
  readonly missing = signal(false);

  constructor() {
    this.route.paramMap.pipe(switchMap(params => this.jewelry.getActive(params.get('id') ?? '')), takeUntilDestroyed()).subscribe({
      next: item => { this.item.set(item ?? null); this.missing.set(!item); },
      error: () => this.missing.set(true)
    });
  }
}
