import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  EventEmitter,
  HostListener,
  inject,
  Input,
  OnChanges,
  OnInit,
  Output
} from '@angular/core';
import {Router, RouterLink} from '@angular/router';
import {NgbModal, NgbOffcanvas, NgbProgressbar, NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {ToastrService} from 'ngx-toastr';
import {Series} from 'src/app/_models/series';
import {ImageService} from 'src/app/_services/image.service';
import {Action, ActionFactoryService, ActionItem} from 'src/app/_services/action-factory.service';
import {SeriesService} from 'src/app/_services/series.service';
import {ActionService} from 'src/app/_services/action.service';
import {EditSeriesModalComponent} from '../_modals/edit-series-modal/edit-series-modal.component';
import {RelationKind} from 'src/app/_models/series-detail/relation-kind';
import {DecimalPipe} from "@angular/common";
import {CardItemComponent} from "../card-item/card-item.component";
import {RelationshipPipe} from "../../_pipes/relationship.pipe";
import {Device} from "../../_models/device/device";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {SeriesPreviewDrawerComponent} from "../../_single-module/series-preview-drawer/series-preview-drawer.component";
import {CardActionablesComponent} from "../../_single-module/card-actionables/card-actionables.component";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {DownloadIndicatorComponent} from "../download-indicator/download-indicator.component";
import {EntityTitleComponent} from "../entity-title/entity-title.component";
import {FormsModule} from "@angular/forms";
import {ImageComponent} from "../../shared/image/image.component";
import {DownloadEvent, DownloadService} from "../../shared/_services/download.service";
import {Observable} from "rxjs";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {map} from "rxjs/operators";
import {AccountService} from "../../_services/account.service";
import {BulkSelectionService} from "../bulk-selection.service";
import {User} from "../../_models/user";
import {ScrollService} from "../../_services/scroll.service";
import {ReaderService} from "../../_services/reader.service";
import {SeriesFormatComponent} from "../../shared/series-format/series-format.component";
import {DefaultModalOptions} from "../../_models/default-modal-options";

function deepClone(obj: any): any {
  if (obj === null || typeof obj !== 'object') {
    return obj;
  }

  if (obj instanceof Array) {
    return obj.map(item => deepClone(item));
  }

  const clonedObj: any = {};

  for (const key in obj) {
    if (Object.prototype.hasOwnProperty.call(obj, key)) {
      if (typeof obj[key] === 'object' && obj[key] !== null) {
        clonedObj[key] = deepClone(obj[key]);
      } else {
        clonedObj[key] = obj[key];
      }
    }
  }

  return clonedObj;
}

@Component({
  selector: 'app-series-card',
  standalone: true,
  imports: [RelationshipPipe, CardActionablesComponent, DefaultValuePipe, DownloadIndicatorComponent,
    FormsModule, ImageComponent, NgbProgressbar, NgbTooltip, RouterLink, TranslocoDirective,
    SeriesFormatComponent, DecimalPipe],
  templateUrl: './series-card.component.html',
  styleUrls: ['./series-card.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeriesCardComponent implements OnInit, OnChanges {

  private readonly offcanvasService = inject(NgbOffcanvas);
  private readonly router = inject(Router);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly seriesService = inject(SeriesService);
  private readonly toastr = inject(ToastrService);
  private readonly modalService = inject(NgbModal);
  protected readonly imageService = inject(ImageService);
  private readonly actionFactoryService = inject(ActionFactoryService);
  private readonly actionService = inject(ActionService);
  private readonly accountService = inject(AccountService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly bulkSelectionService = inject(BulkSelectionService);
  private readonly downloadService = inject(DownloadService);
  private readonly scrollService = inject(ScrollService);
  private readonly readerService = inject(ReaderService);

  @Input({required: true}) series!: Series;
  @Input() libraryId = 0;
  @Input() suppressLibraryLink = false;
  /**
   * If the entity is selected or not.
   */
  @Input() selected: boolean = false;
  /**
   * If the entity should show selection code
   */
  @Input() allowSelection: boolean = false;
  /**
   * If the Series has a relationship to display
   */
  @Input() relation: RelationKind | undefined = undefined;
  /**
   * When a series card is shown on deck, a special actionable is added to the list
   */
  @Input() isOnDeck: boolean = false;
  /**
   * Opens a drawer with a preview of the metadata for this series
   */
  @Input() previewOnClick: boolean = false;

  @Output() clicked = new EventEmitter<Series>();
  /**
   * Emits when a reload needs to occur and the id of the entity
   */
  @Output() reload = new EventEmitter<number>();
  @Output() dataChanged = new EventEmitter<Series>();
  /**
   * When the card is selected.
   */
  @Output() selection = new EventEmitter<boolean>();

  count: number = 0;
  actions: ActionItem<Series>[] = [];
  imageUrl: string = '';
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


  ngOnInit(): void {
    if (this.series) {
      this.imageUrl = this.imageService.getSeriesCoverImage(this.series.id);
      this.cdRef.markForCheck();
    }
  }

  ngOnChanges(changes: any) {
    if (this.series) {
      this.accountService.currentUser$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(user => {
        this.user = user;
      });

      this.download$ = this.downloadService.activeDownloads$.pipe(takeUntilDestroyed(this.destroyRef), map((events) => {
        return this.downloadService.mapToEntityType(events, this.series);
      }));

      this.actions = [...this.actionFactoryService.getSeriesActions((action: ActionItem<Series>, series: Series) => this.handleSeriesActionCallback(action, series))];
      if (this.isOnDeck) {
        const othersIndex = this.actions.findIndex(obj => obj.title === 'others');
        const othersAction = deepClone(this.actions[othersIndex]) as ActionItem<Series>;
        if (othersAction.children.findIndex(o => o.action === Action.RemoveFromOnDeck) < 0) {
          othersAction.children.push({
            action: Action.RemoveFromOnDeck,
            title: 'remove-from-on-deck',
            description: '',
            callback: (action: ActionItem<Series>, series: Series) => this.handleSeriesActionCallback(action, series),
            class: 'danger',
            requiresAdmin: false,
            children: [],
          });
          this.actions[othersIndex] = othersAction;
        }
      }
      this.cdRef.markForCheck();
    }
  }

  handleSeriesActionCallback(action: ActionItem<Series>, series: Series) {
    switch (action.action) {
      case(Action.MarkAsRead):
        this.markAsRead(series);
        break;
      case(Action.MarkAsUnread):
        this.markAsUnread(series);
        break;
      case(Action.Scan):
        this.scanLibrary(series);
        break;
      case(Action.RefreshMetadata):
        this.refreshMetadata(series, true);
        break;
      case(Action.GenerateColorScape):
        this.refreshMetadata(series, false);
        break;
      case(Action.Delete):
        this.deleteSeries(series);
        break;
      case(Action.Edit):
        this.openEditModal(series);
        break;
      case Action.Match:
        this.actionService.matchSeries(this.series, (refreshNeeded) => {
          if (refreshNeeded) {
            this.reload.emit(series.id);
          }
        });
        break;
      case(Action.AddToReadingList):
        this.actionService.addSeriesToReadingList(series);
        break;
      case Action.AddToWantToReadList:
        this.actionService.addMultipleSeriesToWantToReadList([series.id]);
        break;
      case Action.RemoveFromWantToReadList:
        this.actionService.removeMultipleSeriesFromWantToReadList([series.id]);
        if (this.router.url.startsWith('/want-to-read')) {
          this.reload.emit(series.id);
        }
        break;
      case(Action.AddToCollection):
        this.actionService.addMultipleSeriesToCollectionTag([series]);
        break;
      case (Action.AnalyzeFiles):
        this.actionService.analyzeFilesForSeries(series);
        break;
      case Action.SendTo:
        const device = (action._extra!.data as Device);
        this.actionService.sendSeriesToDevice(series.id, device);
        break;
      case Action.RemoveFromOnDeck:
        this.seriesService.removeFromOnDeck(series.id).subscribe(() => this.reload.emit(series.id));
        break;
      case Action.Download:
        this.downloadService.download('series', this.series);
        break;
      default:
        break;
    }
  }

  openEditModal(data: Series) {
    const modalRef = this.modalService.open(EditSeriesModalComponent, DefaultModalOptions);
    modalRef.componentInstance.series = data;
    modalRef.closed.subscribe((closeResult: {success: boolean, series: Series, coverImageUpdate: boolean}) => {
      if (closeResult.success) {
        this.seriesService.getSeries(data.id).subscribe(series => {
          this.series = series;
          this.cdRef.markForCheck();
          this.reload.emit(series.id);
          this.dataChanged.emit(series);
        });
      }
    });
  }

  async refreshMetadata(series: Series, forceUpdate = false) {
    await this.actionService.refreshSeriesMetadata(series, undefined, forceUpdate, forceUpdate);
  }

  async scanLibrary(series: Series) {
    this.seriesService.scan(series.libraryId, series.id).subscribe((res: any) => {
      this.toastr.success(translate('toasts.scan-queued', {name: series.name}));
    });
  }

  async deleteSeries(series: Series) {
    await this.actionService.deleteSeries(series, (result: boolean) => {
      if (result) {
        this.reload.emit(series.id);
      }
    });
  }

  markAsUnread(series: Series) {
    this.actionService.markSeriesAsUnread(series, () => {
      if (this.series) {
        this.series.pagesRead = 0;
        this.cdRef.markForCheck();
      }

      this.dataChanged.emit(series);
    });
  }

  markAsRead(series: Series) {
    this.actionService.markSeriesAsRead(series, () => {
      if (this.series) {
        this.series.pagesRead = series.pages;
        this.cdRef.markForCheck();
      }
      this.dataChanged.emit(series);
    });
  }

  handleClick() {
    if (this.previewOnClick) {
      const ref = this.offcanvasService.open(SeriesPreviewDrawerComponent, {position: 'end', panelClass: ''});
      ref.componentInstance.isExternalSeries = false;
      ref.componentInstance.seriesId = this.series.id;
      ref.componentInstance.libraryId = this.series.libraryId;
      ref.componentInstance.name = this.series.name;
      return;
    }
    this.clicked.emit(this.series);
    this.router.navigate(['library', this.libraryId, 'series', this.series?.id]);
  }

  handleSelection(event?: any) {
    if (event) {
      event.stopPropagation();
    }
    this.selection.emit(this.selected);
    this.cdRef.detectChanges();
  }

  read(event: any) {

    event.stopPropagation();
    if (this.bulkSelectionService.hasSelections()) return;

    // Get Continue Reading point and open directly
    this.readerService.getCurrentChapter(this.series.id).subscribe(chapter => {
      this.readerService.readChapter(this.libraryId, this.series.id, chapter, false);
    });
  }

}
