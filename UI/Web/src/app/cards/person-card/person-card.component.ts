import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, ContentChild,
  DestroyRef, EventEmitter,
  HostListener,
  inject,
  Input, Output, TemplateRef
} from '@angular/core';
import {Action, ActionFactoryService, ActionItem} from "../../_services/action-factory.service";
import {ImageService} from "../../_services/image.service";
import {BulkSelectionService} from "../bulk-selection.service";
import {LibraryService} from "../../_services/library.service";
import {DownloadService} from "../../shared/_services/download.service";
import {UtilityService} from "../../shared/_services/utility.service";
import {MessageHubService} from "../../_services/message-hub.service";
import {AccountService} from "../../_services/account.service";
import {ScrollService} from "../../_services/scroll.service";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {CardActionablesComponent} from "../../_single-module/card-actionables/card-actionables.component";
import {NgTemplateOutlet} from "@angular/common";
import {BrowsePerson} from "../../_models/person/browse-person";
import {Person} from "../../_models/metadata/person";
import {FormsModule} from "@angular/forms";
import {ImageComponent} from "../../shared/image/image.component";
import {TranslocoDirective} from "@jsverse/transloco";


@Component({
  selector: 'app-person-card',
  standalone: true,
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
export class PersonCardComponent {

  private readonly destroyRef = inject(DestroyRef);
  public readonly imageService = inject(ImageService);
  public readonly bulkSelectionService = inject(BulkSelectionService);
  private readonly messageHub = inject(MessageHubService);
  private readonly scrollService = inject(ScrollService);
  private readonly cdRef = inject(ChangeDetectorRef);

  /**
   * Card item url. Will internally handle error and missing covers
   */
  @Input() imageUrl = '';
  /**
   * Name of the card
   */
  @Input() title = '';
  /**
   * If the entity is selected or not.
   */
  @Input() selected: boolean = false;
  /**
   * Any actions to perform on the card
   */
  @Input() actions: ActionItem<any>[] = [];
  /**
   * This is the entity we are representing. It will be returned if an action is executed.
   */
  @Input({required: true}) entity!: BrowsePerson | Person;
  /**
   * If the entity should show selection code
   */
  @Input() allowSelection: boolean = false;
  /**
   * The number of updates/items within the card. If less than 2, will not be shown.
   */
  @Input() count: number = 0;
  /**
   * Event emitted when item is clicked
   */
  @Output() clicked = new EventEmitter<string>();
  /**
   * When the card is selected.
   */
  @Output() selection = new EventEmitter<boolean>();
  @ContentChild('subtitle') subtitleTemplate!: TemplateRef<any>;

  tooltipTitle: string = this.title;
  /**
   * Handles touch events for selection on mobile devices
   */
  prevTouchTime: number = 0;
  /**
   * Handles touch events for selection on mobile devices to ensure you aren't touch scrolling
   */
  prevOffset: number = 0;
  selectionInProgress: boolean = false;

  @HostListener('touchmove', ['$event'])
  onTouchMove(event: TouchEvent) {
    if (!this.allowSelection) return;

    this.selectionInProgress = false;
    this.cdRef.markForCheck();
  }

  @HostListener('touchstart', ['$event'])
  onTouchStart(event: TouchEvent) {
    if (!this.allowSelection) return;

    this.prevTouchTime = event.timeStamp;
    this.prevOffset = this.scrollService.scrollPosition;
    this.selectionInProgress = true;
  }

  @HostListener('touchend', ['$event'])
  onTouchEnd(event: TouchEvent) {
    if (!this.allowSelection) return;
    const delta = event.timeStamp - this.prevTouchTime;
    const verticalOffset = this.scrollService.scrollPosition;

    if (delta >= 300 && delta <= 1000 && (verticalOffset === this.prevOffset) && this.selectionInProgress) {
      this.handleSelection();
      event.stopPropagation();
      event.preventDefault();
    }
    this.prevTouchTime = 0;
    this.selectionInProgress = false;
  }


  handleClick(event?: any) {
    if (this.bulkSelectionService.hasSelections()) {
      this.handleSelection();
      return;
    }
    this.clicked.emit(this.title);
  }

  performAction(action: ActionItem<any>) {
    if (typeof action.callback === 'function') {
      action.callback(action, this.entity);
    }
  }

  handleSelection(event?: any) {
    if (event) {
      event.stopPropagation();
    }
    this.selection.emit(this.selected);
    this.cdRef.detectChanges();
  }

}
