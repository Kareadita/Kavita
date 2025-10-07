import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
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
import {DatePipe, NgClass, NgStyle} from "@angular/common";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ConfirmService} from "../../../../shared/confirm.service";
import {AnnotationService} from "../../../../_services/annotation.service";
import {EpubReaderMenuService} from "../../../../_services/epub-reader-menu.service";
import {DefaultValuePipe} from "../../../../_pipes/default-value.pipe";
import {SlotColorPipe} from "../../../../_pipes/slot-color.pipe";
import {ColorscapeService} from "../../../../_services/colorscape.service";
import {ActivatedRoute, Router, RouterLink} from "@angular/router";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {AccountService} from "../../../../_services/account.service";
import {EVENTS, MessageHubService} from "../../../../_services/message-hub.service";
import {AnnotationUpdateEvent} from "../../../../_models/events/annotation-update-event";
import {AnnotationLikesComponent} from "../annotation-likes/annotation-likes.component";

@Component({
  selector: 'app-annotation-card',
  imports: [
    UtcToLocaleDatePipe,
    QuillViewComponent,
    DatePipe,
    TranslocoDirective,
    DefaultValuePipe,
    NgStyle,
    RouterLink,
    NgClass,
    NgbTooltip,
    AnnotationLikesComponent
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
  private readonly route = inject(ActivatedRoute);
  private readonly highlightSlotPipe = new SlotColorPipe();
  protected readonly accountService = inject(AccountService);
  private readonly messageHub = inject(MessageHubService);
  private readonly destroyRef = inject(DestroyRef);

  annotation = model.required<Annotation>();
  allowEdit = input<boolean>(true);
  showPageLink = input<boolean>(true);
  /**
   * If sizes should be forced. Turned off in drawer to account for manual resize
   */
  forceSize = input<boolean>(true);
  /**
   * Redirects to the reader with annotation in view
   */
  showInReaderLink = input<boolean>(false);
  /**
   * Disable a selection checkbox. Fires selection when called
   */
  showSelectionBox = input<boolean>(false);
  /**
   * Displays series and library name
   */
  showLocationInformation = input<boolean>(false);
  /**
   * Disable a like button
   */
  showLikes = input<boolean>(true);
  openInIncognitoMode = input<boolean>(false);
  isInReader = input<boolean>(true);
  /**
   * If enabled, listens to annotation updates
   */
  listedToUpdates = input<boolean>(false);
  /**
   * If the card is rendered inside the book reader. Used for styling the confirm button
   */
  inBookReader = input<boolean>(false);

  selected = input<boolean>(false);
  @Output() delete = new EventEmitter();
  @Output() navigate = new EventEmitter<Annotation>();
  /**
   * Fire when the checkbox is pressed, with the last known state (inverse of checked state)
   */
  @Output() selection = new EventEmitter<boolean>();

  titleColor: Signal<string>;
  hasClicked = model<boolean>(false);

  constructor() {

    effect(() => {
      const enabled = this.listedToUpdates();
      const event = this.messageHub.messageSignal();
      if (!enabled || event?.event !== EVENTS.AnnotationUpdate) return;

      const newAnnotation = (event.payload as AnnotationUpdateEvent).annotation;
      if (this.annotation().id != newAnnotation.id) return;

      this.annotation.set(newAnnotation);
    });

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

    // If outside the reader, we need to use a load reader with a special handler
    const queryParams = { ...this.route.snapshot.queryParams };
    queryParams['annotation'] = this.annotation().id + '';

    // Navigate to same route with updated query params
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams,
      replaceUrl: false
    });
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
    if (!await this.confirmService.confirm(translate('toasts.confirm-delete-annotation'), {
      ...this.confirmService.defaultConfirm,
      bookReader: this.inBookReader(),
    })) return;
    const annotation = this.annotation();
    if (!annotation) return;

    this.annotationService.delete(annotation.id).subscribe(_ => {
      this.delete.emit();
    });
  }
}
