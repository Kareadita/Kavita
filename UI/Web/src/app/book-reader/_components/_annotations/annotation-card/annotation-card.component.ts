import {
  ChangeDetectionStrategy,
  Component,
  computed,
  EventEmitter,
  inject,
  input,
  model,
  Output,
  Signal
} from '@angular/core';
import {Annotation} from "../../../_models/annotations/annotation";
import {UtcToLocaleDatePipe} from "../../../../_pipes/utc-to-locale-date.pipe";
import {QuillViewComponent} from "ngx-quill";
import {DatePipe, NgStyle} from "@angular/common";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ConfirmService} from "../../../../shared/confirm.service";
import {AnnotationService} from "../../../../_services/annotation.service";
import {EpubReaderMenuService} from "../../../../_services/epub-reader-menu.service";
import {DefaultValuePipe} from "../../../../_pipes/default-value.pipe";
import {SlotColorPipe} from "../../../../_pipes/slot-color.pipe";
import {ColorscapeService} from "../../../../_services/colorscape.service";
import {Router} from "@angular/router";

@Component({
  selector: 'app-annotation-card',
  imports: [
    UtcToLocaleDatePipe,
    QuillViewComponent,
    DatePipe,
    TranslocoDirective,
    DefaultValuePipe,
    NgStyle
  ],
  templateUrl: './annotation-card.component.html',
  styleUrl: './annotation-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AnnotationCardComponent {

  protected readonly colorscapeService = inject(ColorscapeService);
  private readonly confirmService = inject(ConfirmService);
  private readonly annotationService = inject(AnnotationService);
  private readonly epubMenuService = inject(EpubReaderMenuService);
  private readonly router = inject(Router);
  private readonly highlightSlotPipe = new SlotColorPipe();

  annotation = model.required<Annotation>();
  allowEdit = input<boolean>(true);
  showPageLink = input<boolean>(true);
  isInReader = input<boolean>(true);
  @Output() delete = new EventEmitter();
  @Output() navigate = new EventEmitter<Annotation>();

  titleColor: Signal<string>;

  constructor() {

    // TODO: Validate if I want this -- aka update content on a detail page when receiving update from backend
    // this.messageHub.messages$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(message => {
    //   if (message.payload !== EVENTS.AnnotationUpdate) return;
    //   const updatedAnnotation = message.payload as AnnotationUpdateEvent;
    //   if (this.annotation()?.id !== updatedAnnotation.annotation.id) return;
    //
    //   console.log('Refreshing annotation from backend: ', updatedAnnotation.annotation);
    //   this.annotation.set(updatedAnnotation.annotation);
    // });


    this.titleColor = computed(() => {
      const annotation = this.annotation();
      const slots = this.annotationService.slots();
      if (!annotation || annotation.selectedSlotIndex < 0 || annotation.selectedSlotIndex >= slots.length) return '';

      return this.highlightSlotPipe.transform(slots[annotation.selectedSlotIndex].color);
    });
  }

  loadAnnotation() {
    // Check if the url is within the reader or not
    // If within the reader, we can use a event to allow drawer to load
    // If outside the reader, we need to use a load reader with a special handler
    if (this.isInReader()) {
      this.navigate.emit(this.annotation());
      return;
    }
    const url = this.router.url;
    console.log(url); // TODO: Validate
    this.router.navigateByUrl(url);
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
