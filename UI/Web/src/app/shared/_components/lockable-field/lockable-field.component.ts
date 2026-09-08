import {Component, model} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
  imports: [
    TranslocoDirective
  ],
  selector: 'app-lockable-field',
  styleUrl: './lockable-field.component.scss',
  templateUrl: './lockable-field.component.html',
})
export class LockableFieldComponent {
  locked = model.required<boolean>();
}
