import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  EventEmitter,
  HostListener,
  inject,
  Input,
  OnInit,
  Output
} from '@angular/core';
import {ImageService} from "../../_services/image.service";
import {BulkSelectionService} from "../bulk-selection.service";
import {DownloadEvent, DownloadService} from "../../shared/_services/download.service";
import {EVENTS, MessageHubService} from "../../_services/message-hub.service";
import {AccountService} from "../../_services/account.service";
import {ScrollService} from "../../_services/scroll.service";
import {ActionItem} from "../../_services/action-factory.service";
import {Chapter} from "../../_models/chapter";
import {Observable} from "rxjs";
import {User} from "../../_models/user/user";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {NgbProgressbar, NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {DecimalPipe} from "@angular/common";
import {ImageComponent} from "../../shared/image/image.component";
import {DownloadIndicatorComponent} from "../download-indicator/download-indicator.component";
import {FormsModule} from "@angular/forms";
import {EntityTitleComponent} from "../entity-title/entity-title.component";
import {CardActionablesComponent} from "../../_single-module/card-actionables/card-actionables.component";
import {Router, RouterLink} from "@angular/router";
import {TranslocoDirective} from "@jsverse/transloco";
import {filter, map} from "rxjs/operators";
import {UserProgressUpdateEvent} from "../../_models/events/user-progress-update-event";
import {ReaderService} from "../../_services/reader.service";
import {LibraryType} from "../../_models/library/library";
import {MangaFormat} from "../../_models/manga-format";

@Component({
    selector: 'app-chapter-card',
    imports: [
        NgbTooltip,
        NgbProgressbar,
        DecimalPipe,
        ImageComponent,
        DownloadIndicatorComponent,
        FormsModule,
        EntityTitleComponent,
        CardActionablesComponent,
        RouterLink,
        TranslocoDirective
    ],
    templateUrl: './chapter-card.component.html',
    styleUrl: './chapter-card.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChapterCardComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  public readonly imageService = inject(ImageService);
  public readonly bulkSelectionService = inject(BulkSelectionService);
  private readonly downloadService = inject(DownloadService);
  private readonly messageHub = inject(MessageHubService);
  private readonly accountService = inject(AccountService);
  private readonly scrollService = inject(ScrollService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly router = inject(Router);
  private readonly readerService = inject(ReaderService);

  protected readonly LibraryType = LibraryType;
  protected readonly MangaFormat = MangaFormat;

  @Input({required: true}) libraryId: number = 0;
  @Input({required: true}) seriesId: number = 0;
  @Input({required: true}) chapter!: Chapter;
  @Input({required: true}) libraryType!: LibraryType;
  /**
   * Any actions to perform on the card
   */
  @Input() actions: ActionItem<Chapter>[] = [];
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
    this.accountService.currentUser$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(user => {
      this.user = user;
    });

    this.download$ = this.downloadService.activeDownloads$.pipe(takeUntilDestroyed(this.destroyRef), map((events) => {
      return this.downloadService.mapToEntityType(events, this.chapter);
    }));


    this.messageHub.messages$.pipe(filter(event => event.event === EVENTS.UserProgressUpdate),
      map(evt => evt.payload as UserProgressUpdateEvent), takeUntilDestroyed(this.destroyRef)).subscribe(updateEvent => {
      if (this.user === undefined || this.user.username !== updateEvent.username) return;
      if (updateEvent.chapterId !== this.chapter.id) return;

      this.chapter.pagesRead = updateEvent.pagesRead;
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

  handleClick(event: any) {
    if (this.bulkSelectionService.hasSelections()) {
      this.handleSelection(event);
      return;
    }

    this.router.navigate(['library', this.libraryId, 'series', this.seriesId, 'chapter', this.chapter.id]);
  }

  read(event: any) {
    event.stopPropagation();
    this.readerService.readChapter(this.libraryId, this.seriesId, this.chapter, false);
  }
}
