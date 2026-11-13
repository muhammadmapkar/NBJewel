// Role: Shows one of the inline SVG icons from icons.ts at the current text colour.

import { Component, computed, inject, input } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';
import { ICONS, IconName } from './icons';

@Component({
  selector: 'ct-icon', standalone: true,
  template: `<span class="icon" [innerHTML]="svg()"></span>`,
  styles: [`:host { display: inline-flex; width: 22px; height: 22px; } .icon, .icon ::ng-deep svg { display: block; width: 100%; height: 100%; }`]
})
export class IconComponent {
  private readonly sanitizer = inject(DomSanitizer);
  readonly name = input.required<IconName>();
  // The SVGs are fixed strings in this app (never user data), so trusting them is safe.
  readonly svg = computed(() => this.sanitizer.bypassSecurityTrustHtml(ICONS[this.name()]));
}
