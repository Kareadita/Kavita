import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  EventEmitter, HostListener,
  inject,
  Input, OnInit,
  Output
} from '@angular/core';
import {CardActionablesComponent} from "../../_single-module/card-actionables/card-actionables.component";
import {DecimalPipe} from "@angular/common";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {DownloadIndicatorComponent} from "../download-indicator/download-indicator.component";
import {EntityTitleComponent} from "../entity-title/entity-title.component";
import {ImageComponent} from "../../shared/image/image.component";
import {NgbProgressbar, NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {Router, RouterLink} from "@angular/router";
import {Select2Module} from "ng-select2-component";
import {TranslocoDirective} from "@jsverse/transloco";
import {ImageService} from "../../_services/image.service";
import {BulkSelectionService} from "../bulk-selection.service";
import {DownloadEvent, DownloadService} from "../../shared/_services/download.service";
import {EVENTS, MessageHubService} from "../../_services/message-hub.service";
import {AccountService} from "../../_services/account.service";
import {ScrollService} from "../../_services/scroll.service";
import {Action, ActionFactoryService, ActionItem} from "../../_services/action-factory.service";
import {ReaderService} from "../../_services/reader.service";
import {Observable} from "rxjs";
import {User} from "../../_models/user";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {filter, map} from "rxjs/operators";
import {UserProgressUpdateEvent} from "../../_models/events/user-progress-update-event";
import {Volume} from "../../_models/volume";
import {UtilityService} from "../../shared/_services/utility.service";
import {LibraryType} from "../../_models/library/library";
import {RelationshipPipe} from "../../_pipes/relationship.pipe";
import {Device} from "../../_models/device/device";
import {ActionService} from "../../_services/action.service";

@Component({
  selector: 'app-volume-card',
  standalone: true,
  imports: [
    CardActionablesComponent,
    DecimalPipe,
    DefaultValuePipe,
    DownloadIndicatorComponent,
    EntityTitleComponent,
    ImageComponent,
    NgbProgressbar,
    NgbTooltip,
    RouterLink,
    Select2Module,
    TranslocoDirective,
    RelationshipPipe
  ],
  templateUrl: './volume-card.component.html',
  styleUrl: './volume-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class VolumeCardComponent implements OnInit {

  private readonly destroyRef = inject(DestroyRef);
  public readonly imageService = inject(ImageService);
  public readonly bulkSelectionService = inject(BulkSelectionService);
  private readonly downloadService = inject(DownloadService);
  private readonly actionService = inject(ActionService);
  private readonly messageHub = inject(MessageHubService);
  private readonly accountService = inject(AccountService);
  private readonly scrollService = inject(ScrollService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly router = inject(Router);
  private readonly readerService = inject(ReaderService);
  protected readonly utilityService = inject(UtilityService);

  @Input({required: true}) libraryId: number = 0;
  @Input({required: true}) libraryType!: LibraryType;
  @Input({required: true}) seriesId: number = 0;
  @Input({required: true}) volume!: Volume;
  /**
   * Any actions to perform on the card
   */
  @Input() actions: ActionItem<Volume>[] = [];
  /**
   * If the entity is selected or not.
   */
  @Input() selected: boolean = false;
  /**
   * If the entity should show selection code
   */
  @Input() allowSelection: boolean = false;
  /**
   * This will suppress the "cannot read archive warning" when total pages is 0
   */
  @Input() suppressArchiveWarning: boolean = false;
  /**
   * When the card is selected.
   */
  @Output() selection = new EventEmitter<boolean>();

  /**
   * This is the download we get from download service.
   */
  download$: Observable<DownloadEvent | null> | null = null;
  /**
   * Handles touch events for selection on mobile devices
   */
  prevTouchTime: number = 0;
  /**
   * Handles touch events for selection on mobile devices to ensure you aren't touch scrolling
   */
  prevOffset: number = 0;
  selectionInProgress: boolean = false;

  private user: User | undefined;

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

  ngOnInit() {
    this.filterSendTo();

    this.accountService.currentUser$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(user => {
      this.user = user;
    });

    this.download$ = this.downloadService.activeDownloads$.pipe(takeUntilDestroyed(this.destroyRef), map((events) => {
      return this.downloadService.mapToEntityType(events, this.volume);
    }));


    this.messageHub.messages$.pipe(filter(event => event.event === EVENTS.UserProgressUpdate),
      map(evt => evt.payload as UserProgressUpdateEvent), takeUntilDestroyed(this.destroyRef))
      .subscribe(updateEvent => {
      if (this.user === undefined || this.user.username !== updateEvent.username) return;
      if (updateEvent.volumeId !== this.volume.id) return;

        let sum = 0;
        const chapters = this.volume.chapters.filter(c => c.volumeId === updateEvent.volumeId);
        chapters.forEach(chapter => {
          chapter.pagesRead = updateEvent.pagesRead;
          sum += chapter.pagesRead;
        });
        this.volume.pagesRead = sum;
        this.cdRef.detectChanges();
    });

  }

  handleSelection(event?: any) {
    if (event) {
      event.stopPropagation();
    }
    this.selection.emit(this.selected);
    this.cdRef.detectChanges();
  }


  filterSendTo() {
    if (!this.actions || this.actions.length === 0) return;
    // TODO: See if we can handle send to for volumes
    //this.actions = this.actionFactoryService.filterSendToAction(this.actions, this.volume);
  }

  performAction(action: ActionItem<Volume>) {
    if (action.action == Action.Download) {
      this.downloadService.download('volume', this.volume);
      return; // Don't propagate the download from a card
    }

    if (action.action == Action.SendTo) {
      const device = (action._extra!.data as Device);
      this.actionService.sendToDevice(this.volume.chapters.map(c => c.id), device);
      return;
    }

    if (typeof action.callback === 'function') {
      action.callback(action, this.volume);
    }
  }

  handleClick(event: any) {
    if (this.bulkSelectionService.hasSelections()) {
      this.handleSelection(event);
      return;
    }
    this.router.navigate(['library', this.libraryId, 'series', this.seriesId, 'volume', this.volume.id]);
  }

  read(event: any) {
    event.stopPropagation();
    event.preventDefault();
    console.log('reading volume');
    this.readerService.readVolume(this.libraryId, this.seriesId, this.volume, false);
  }

  protected readonly LibraryType = LibraryType;
}
