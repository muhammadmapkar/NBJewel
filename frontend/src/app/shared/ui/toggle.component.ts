// Role: Renders a visual-only gold toggle with no form or business behavior.

import { Component, Input } from '@angular/core';

@Component({ selector: 'nb-toggle', standalone: true, template: `<button class="toggle" [class.on]="on" (click)="on = !on" [attr.aria-pressed]="on"><span></span></button>`, styles: [`.toggle { width:34px; height:20px; padding:2px; border:0; border-radius:20px; background:#e7e3d7; } .toggle.on { background:var(--gold); } .toggle span { display:block; width:16px; height:16px; border-radius:50%; background:white; transition:transform .15s; } .toggle.on span { transform:translateX(14px); }`] })
export class ToggleComponent { @Input() on = false; }
