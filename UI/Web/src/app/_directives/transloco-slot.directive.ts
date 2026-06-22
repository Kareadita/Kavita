import {Directive, input, TemplateRef} from '@angular/core';

@Directive({
  selector: 'ng-template[translocoSlot]',
  standalone: true,
})
export class TranslocoSlotDirective {
  readonly translocoSlot = input.required<string>();
  constructor(readonly tpl: TemplateRef<unknown>) {}
}
