// Role: Provides a reusable white widget with a gold header and visual close
// control used by the dashboard cards.

import { Component, EventEmitter, Input, Output } from '@angular/core';

@Component({
  selector: 'nb-widget-card', standalone: true,
  template: `<article class="widget" [class.hidden]="!visible"><header><b>{{ title }}</b><button (click)="closed.emit()">×</button></header><div class="widget-body"><ng-content /></div></article>`,
  styles: [`.widget { background:white; border:1px solid var(--gold); border-radius:7px; overflow:hidden; } .widget.hidden { display:none; } header { height:40px; display:flex; align-items:center; justify-content:space-between; padding:0 14px; color:var(--gold); background:var(--widget); } header button { border:0; background:transparent; color:var(--gold); font-size:20px; } .widget-body { padding:16px; }`]
})
export class WidgetCardComponent { @Input() title = ''; @Input() visible = true; @Output() closed = new EventEmitter<void>(); }
