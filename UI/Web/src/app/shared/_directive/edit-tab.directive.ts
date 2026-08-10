import {Directive, inject, input, TemplateRef} from '@angular/core';
import {Tabs} from "../../_models/tabs";

/**
 * Directive for the EditModalComponent
 */
@Directive({
  selector: '[appEditTab]',
  standalone: true
})
export class EditTabDirective {
  id = input.required<Tabs>({alias: 'appEditTab'});

  template = inject(TemplateRef<any>);

}
