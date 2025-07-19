import {ChangeDetectionStrategy, Component, computed, EventEmitter, inject, model, Output, Signal} from '@angular/core';
import {Annotation} from "../../../_models/annotation";
import {UtcToLocaleDatePipe} from "../../../../_pipes/utc-to-locale-date.pipe";
import {QuillViewComponent} from "ngx-quill";
import {DatePipe, JsonPipe} from "@angular/common";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ConfirmService} from "../../../../shared/confirm.service";
import {AnnotationService} from "../../../../_services/annotation.service";
import {EpubReaderMenuService} from "../../../../_services/epub-reader-menu.service";
import {HighlightColorPipe} from "../../../../_pipes/highlight-color.pipe";
import {DefaultValuePipe} from "../../../../_pipes/default-value.pipe";

@Component({
  selector: 'app-annotation-card',
  imports: [
    UtcToLocaleDatePipe,
    QuillViewComponent,
    DatePipe,
    TranslocoDirective,
    DefaultValuePipe,
    JsonPipe
  ],
  templateUrl: './annotation-card.component.html',
  styleUrl: './annotation-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AnnotationCardComponent {
  private readonly confirmService = inject(ConfirmService);
  private readonly annotationService = inject(AnnotationService);
  private readonly epubMenuService = inject(EpubReaderMenuService);
  private readonly highlightColorPipe = new HighlightColorPipe();

  annotation = model.required<Annotation>();
  @Output() delete = new EventEmitter();

  titleClass: Signal<string>;

  constructor() {
    this.titleClass = computed(() => {
      const annotation = this.annotation();
      if (!annotation) return '';
      return `${this.highlightColorPipe.transform(annotation.highlightColor)}-title`;
    })
  }

  editAnnotation() {
    this.epubMenuService.openViewAnnotationDrawer(this.annotation(), true, (updatedAnnotation: Annotation) => {
      this.annotation.set(updatedAnnotation);
    });
  }

  viewAnnotation() {
    this.epubMenuService.openViewAnnotationDrawer(this.annotation(), false, (updatedAnnotation: Annotation) => {
      this.annotation.set(updatedAnnotation);
    });
  }

  async deleteAnnotation() {
    if (!await this.confirmService.confirm(translate('toasts.confirm-delete-annotation'))) return;
    const annotation = this.annotation();
    if (!annotation) return;

    this.annotationService.delete(annotation.id).subscribe(_ => {
      this.delete.emit();
    });

  }
}
