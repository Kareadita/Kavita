import {Component, inject, Input} from '@angular/core';
import {FormsModule} from "@angular/forms";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {SafeHtmlPipe} from "../../../_pipes/safe-html.pipe";

@Component({
  selector: 'app-out-of-date-modal',
  imports: [
    FormsModule,
    TranslocoDirective,
    SafeHtmlPipe
  ],
  templateUrl: './out-of-date-modal.component.html',
  styleUrl: './out-of-date-modal.component.scss'
})
export class OutOfDateModalComponent {

  private readonly ngbModal = inject(NgbActiveModal);

  @Input({required: true}) versionsOutOfDate: number = 0;

  close() {
    this.ngbModal.close();
  }
}
