import {ChangeDetectionStrategy, Component, EventEmitter, inject, model, Output} from '@angular/core';
import {Annotation} from "../../../_models/annotation";
import {UtcToLocaleDatePipe} from "../../../../_pipes/utc-to-locale-date.pipe";
import {QuillViewComponent} from "ngx-quill";
import {DatePipe} from "@angular/common";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ConfirmService} from "../../../../shared/confirm.service";
import {AnnotationService} from "../../../../_services/annotation.service";
import {EpubReaderMenuService} from "../../../../_services/epub-reader-menu.service";

@Component({
  selector: 'app-annotation-card',
  imports: [
    UtcToLocaleDatePipe,
    QuillViewComponent,
    DatePipe,
    TranslocoDirective
  ],
  templateUrl: './annotation-card.component.html',
  styleUrl: './annotation-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AnnotationCardComponent {
  private readonly confirmService = inject(ConfirmService);
  private readonly annotationService = inject(AnnotationService);
  private readonly epubMenuService = inject(EpubReaderMenuService);

  annotation = model.required<Annotation>();
  @Output() delete = new EventEmitter();

  editAnnotation() {
    this.epubMenuService.openViewAnnotationDrawer(this.annotation(), true, (updatedAnnotation: Annotation) => {
      this.annotation.set(updatedAnnotation);
    });
  }

  async deleteAnnotation() {
    if (!await this.confirmService.confirm(translate('toasts.confirm-delete-annotation'))) return;

    this.annotationService.delete(this.annotation().id).subscribe(_ => {
      this.delete.emit();
    });

  }
}
