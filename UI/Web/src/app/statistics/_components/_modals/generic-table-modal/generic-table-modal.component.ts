import {ChangeDetectionStrategy, Component, inject, input, TemplateRef} from '@angular/core';
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {NgTemplateOutlet} from "@angular/common";
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
  selector: 'app-generic-table-modal',
  standalone: true,
  imports: [
    NgTemplateOutlet,
    TranslocoDirective
  ],
  templateUrl: './generic-table-modal.component.html',
  styleUrl: './generic-table-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class GenericTableModalComponent {
  // TODO: Maybe delete this?
  public readonly modalService = inject(NgbActiveModal);

  title = input.required<string>();
  bodyTemplate = input.required<TemplateRef<any>>();

}
