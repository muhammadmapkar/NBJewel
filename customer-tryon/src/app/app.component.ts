// Role: The root of the customer app: just the routed page. Each page draws its own header.

import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({ selector: 'ct-root', standalone: true, imports: [RouterOutlet], template: '<router-outlet />' })
export class AppComponent {}
