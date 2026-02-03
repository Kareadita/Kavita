import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  DestroyRef,
  EventEmitter,
  inject,
  input,
  Input,
  OnChanges,
  OnInit,
  Output,
  signal,
  SimpleChanges
} from '@angular/core';
import {Router} from "@angular/router";
import {ImageService} from "../../_services/image.service";
import {BulkSelectionService} from "../bulk-selection.service";
import {DownloadEvent, DownloadService} from "../../shared/_services/download.service";
import {EVENTS, MessageHubService} from "../../_services/message-hub.service";
import {AccountService} from "../../_services/account.service";
import {ScrollService} from "../../_services/scroll.service";
import {ActionItem} from "../../_services/action-factory.service";
import {ReaderService} from "../../_services/reader.service";
import {Observable} from "rxjs";
import {User} from "../../_models/user/user";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {filter, map} from "rxjs/operators";
import {UserProgressUpdateEvent} from "../../_models/events/user-progress-update-event";
import {Volume} from "../../_models/volume";
import {UtilityService} from "../../shared/_services/utility.service";
import {LibraryType} from "../../_models/library/library";
import {ActionService} from "../../_services/action.service";
import {FormsModule} from "@angular/forms";
import {EntityCardComponent} from "../entity-card/entity-card.component";
import {CardConfigFactory} from "../../_services/card-config-factory.service";
import {CardEntity, CardEntityFactory} from "../../_models/card/card-entity";
import {CardConfiguration} from "../../_models/card/card-configuration";

@Component({
  selector: 'app-volume-card',
  imports: [
    FormsModule,
    EntityCardComponent,
  ],
  templateUrl: './volume-card.component.html',
  styleUrl: './volume-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class VolumeCardComponent implements OnInit, OnChanges {

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
  private readonly configFactory = inject(CardConfigFactory);

  index = input.required<number>();
  maxIndex = input.required<number>();

  // ============================================================
  // EXISTING PUBLIC API (maintained for backwards compatibility)
  // ============================================================

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

  private volumeSignal = signal<Volume | null>(null);

  cardEntity = computed<CardEntity>(() => {
    const volume = this.volumeSignal();
    if (!volume) {
      // Return a placeholder - shouldn't render in practice
      return CardEntityFactory.volume({} as Volume, 0, 0);
    }
    return CardEntityFactory.volume(volume, this.seriesId, this.libraryId);
  });

  config = computed<CardConfiguration<Volume>>(() => {
    const baseConfig = this.configFactory.forVolume(
      this.seriesId,
      this.libraryId,
      this.libraryType,
      this.handleAction.bind(this),
      {
        allowSelection: this.allowSelection,
        clickFunc: this.handleClick.bind(this)
      }
    );

    return baseConfig;
  });




  ngOnInit() {
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

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['entity']) {
      this.volumeSignal.set(this.volume);
    }
  }

  handleAction(action: ActionItem<Volume>, volume: Volume) {

  }


  handleSelection(event?: any) {
    // if (event) {
    //   event.stopPropagation();
    // }
    // this.selection.emit(this.selected);
    // this.cdRef.detectChanges();
  }

  handleClick(event: any) {
    if (this.bulkSelectionService.hasSelections()) {
      this.handleSelection(event);
      return;
    }
    this.router.navigate(['library', this.libraryId, 'series', this.seriesId, 'volume', this.volume.id]);
  }

  read(event: any) {
    // event.stopPropagation();
    // event.preventDefault();
    // this.readerService.readVolume(this.libraryId, this.seriesId, this.volume, false);
  }

  protected readonly LibraryType = LibraryType;
}
