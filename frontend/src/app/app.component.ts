// Role: Provides the root host element for the shared routed application shell.

import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({ selector: 'nb-root', standalone: true, imports: [RouterOutlet], template: '<router-outlet />' })
export class AppComponent {}
