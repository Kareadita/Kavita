import {Component, DestroyRef, inject, Input} from '@angular/core';
import {FormsModule} from "@angular/forms";
import {AsyncPipe, NgForOf, NgIf} from "@angular/common";
import {NgbActiveModal, NgbHighlight, NgbModal, NgbTypeahead} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {ServerService} from "../../../_services/server.service";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {map} from "rxjs/operators";
import {ChangelogComponent} from "../changelog/changelog.component";
import {SafeHtmlPipe} from "../../../_pipes/safe-html.pipe";

@Component({
  selector: 'app-out-of-date-modal',
  standalone: true,
  imports: [
    FormsModule,
    NgForOf,
    NgIf,
    NgbHighlight,
    NgbTypeahead,
    TranslocoDirective,
    AsyncPipe,
    ChangelogComponent,
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
