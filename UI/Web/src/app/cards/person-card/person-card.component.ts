import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  contentChild,
  HostListener,
  inject, input,
  output, signal,
  TemplateRef
} from '@angular/core';
import {ImageService} from "../../_services/image.service";
import {BulkSelectionService} from "../bulk-selection.service";
import {ScrollService} from "../../_services/scroll.service";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {CardActionablesComponent} from "../../_single-module/card-actionables/card-actionables.component";
import {NgTemplateOutlet} from "@angular/common";
import {BrowsePerson} from "../../_models/metadata/browse/browse-person";
import {Person} from "../../_models/metadata/person";
import {FormsModule} from "@angular/forms";
import {ImageComponent} from "../../shared/image/image.component";
import {TranslocoDirective} from "@jsverse/transloco";
import {ActionItem} from "../../_models/actionables/action-item";


@Component({
    selector: 'app-person-card',
    imports: [
        NgbTooltip,
        CardActionablesComponent,
        NgTemplateOutlet,
        FormsModule,
        ImageComponent,
        TranslocoDirective
    ],
    templateUrl: './person-card.component.html',
    styleUrl: './person-card.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class PersonCardComponent<T extends Person> {

  public readonly imageService = inject(ImageService);
  public readonly bulkSelectionService = inject(BulkSelectionService);
  private readonly scrollService = inject(ScrollService);
  private readonly cdRef = inject(ChangeDetectorRef);

  /**
   * Card item url. Will internally handle error and missing covers
   */
  imageUrl = input('');
  /**
   * Name of the card
   */
  title = input('');
  /**
   * If the entity is selected or not.
   */
  selected = input(false);
  /**
   * Any actions to perform on the card
   */
  actions = input<ActionItem<T>[]>([]);
  /**
   * This is the entity we are representing. It will be returned if an action is executed.
   */
  entity = input.required<T>();
  /**
   * If the entity should show selection code
   */
  allowSelection = input(false);
  /**
   * The number of updates/items within the card. If less than 2, will not be shown.
   */
  count = input(0);
  /**
   * Event emitted when item is clicked
   */
  readonly clicked = output<string>();
  /**
   * When the card is selected.
   */
  readonly selection = output<boolean>();
  subtitleTemplate = contentChild<TemplateRef<any>>('subtitle');

  /**
   * Handles touch events for selection on mobile devices
   */
  prevTouchTime = signal(0);
  /**
   * Handles touch events for selection on mobile devices to ensure you aren't touch scrolling
   */
  prevOffset = signal(0);
  selectionInProgress = signal(false);

  @HostListener('touchmove', ['$event'])
  onTouchMove(event: TouchEvent) {
    if (!this.allowSelection()) return;

    this.selectionInProgress.set(false);
  }

  @HostListener('touchstart', ['$event'])
  onTouchStart(event: TouchEvent) {
    if (!this.allowSelection()) return;

    this.prevTouchTime.set(event.timeStamp);
    this.prevOffset.set(this.scrollService.scrollPosition);
    this.selectionInProgress.set(true);
  }

  @HostListener('touchend', ['$event'])
  onTouchEnd(event: TouchEvent) {
    if (!this.allowSelection) return;
    const delta = event.timeStamp - this.prevTouchTime();
    const verticalOffset = this.scrollService.scrollPosition;

    if (delta >= 300 && delta <= 1000 && (verticalOffset === this.prevOffset()) && this.selectionInProgress()) {
      this.handleSelection();
      event.stopPropagation();
      event.preventDefault();
    }
    this.prevTouchTime.set(0);
    this.selectionInProgress.set(false);
  }


  handleClick(event?: any) {
    if (this.bulkSelectionService.hasSelections()) {
      this.handleSelection();
      return;
    }

    this.clicked.emit(this.title());
  }


  handleSelection(event?: any) {
    if (event) {
      event.stopPropagation();
    }
    this.selection.emit(this.selected());
  }

}
