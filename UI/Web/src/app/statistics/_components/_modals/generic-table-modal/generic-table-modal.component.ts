import {Component, ContentChild, inject, Input, TemplateRef} from '@angular/core';
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
  styleUrl: './generic-table-modal.component.scss'
})
export class GenericTableModalComponent {

  public readonly modalService = inject(NgbActiveModal);

  @Input({required: true}) title: string = '';
  @Input() bodyTemplate!: TemplateRef<any>;

  ngOnInit() {
    console.log('bodyTemplate: ', this.bodyTemplate)
  }

}
