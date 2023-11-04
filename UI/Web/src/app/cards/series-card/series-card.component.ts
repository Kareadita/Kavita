import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  EventEmitter, inject,
  Input,
  OnChanges,
  OnInit,
  Output
} from '@angular/core';
import {Router} from '@angular/router';
import {NgbModal, NgbOffcanvas} from '@ng-bootstrap/ng-bootstrap';
import {ToastrService} from 'ngx-toastr';
import {Series} from 'src/app/_models/series';
import {ImageService} from 'src/app/_services/image.service';
import {Action, ActionFactoryService, ActionItem} from 'src/app/_services/action-factory.service';
import {SeriesService} from 'src/app/_services/series.service';
import {ActionService} from 'src/app/_services/action.service';
import {EditSeriesModalComponent} from '../_modals/edit-series-modal/edit-series-modal.component';
import {RelationKind} from 'src/app/_models/series-detail/relation-kind';
import {CommonModule} from "@angular/common";
import {CardItemComponent} from "../card-item/card-item.component";
import {RelationshipPipe} from "../../_pipes/relationship.pipe";
import {Device} from "../../_models/device/device";
import {translate, TranslocoService} from "@ngneat/transloco";
import {SeriesPreviewDrawerComponent} from "../../_single-module/series-preview-drawer/series-preview-drawer.component";

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
  imports: [CommonModule, CardItemComponent, RelationshipPipe],
  templateUrl: './series-card.component.html',
  styleUrls: ['./series-card.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeriesCardComponent implements OnInit, OnChanges {
  @Input({required: true}) data!: Series;
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

  actions: ActionItem<Series>[] = [];
  imageUrl: string = '';

  private readonly offcanvasService = inject(NgbOffcanvas);

  constructor(private router: Router, private cdRef: ChangeDetectorRef,
              private seriesService: SeriesService, private toastr: ToastrService,
              private modalService: NgbModal, private imageService: ImageService,
              private actionFactoryService: ActionFactoryService,
              private actionService: ActionService) {}


  ngOnInit(): void {
    if (this.data) {
      this.imageUrl = this.imageService.getSeriesCoverImage(this.data.id);
      this.cdRef.markForCheck();
    }
  }

  ngOnChanges(changes: any) {
    if (this.data) {
      this.actions = [...this.actionFactoryService.getSeriesActions((action: ActionItem<Series>, series: Series) => this.handleSeriesActionCallback(action, series))];
      if (this.isOnDeck) {
        const othersIndex = this.actions.findIndex(obj => obj.title === 'others');
        const othersAction = deepClone(this.actions[othersIndex]) as ActionItem<Series>;
        if (othersAction.children.findIndex(o => o.action === Action.RemoveFromOnDeck) < 0) {
          othersAction.children.push({
            action: Action.RemoveFromOnDeck,
            title: 'remove-from-on-deck',
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
        this.refreshMetadata(series);
        break;
      case(Action.Delete):
        this.deleteSeries(series);
        break;
      case(Action.Edit):
        this.openEditModal(series);
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
      default:
        break;
    }
  }

  openEditModal(data: Series) {
    const modalRef = this.modalService.open(EditSeriesModalComponent, {  size: 'lg' });
    modalRef.componentInstance.series = data;
    modalRef.closed.subscribe((closeResult: {success: boolean, series: Series, coverImageUpdate: boolean}) => {
      if (closeResult.success) {
        this.seriesService.getSeries(data.id).subscribe(series => {
          this.data = series;
          this.cdRef.markForCheck();
          this.reload.emit(series.id);
          this.dataChanged.emit(series);
        });
      }
    });
  }

  async refreshMetadata(series: Series) {
    await this.actionService.refreshMetdata(series);
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
      if (this.data) {
        this.data.pagesRead = 0;
        this.cdRef.markForCheck();
      }

      this.dataChanged.emit(series);
    });
  }

  markAsRead(series: Series) {
    this.actionService.markSeriesAsRead(series, () => {
      if (this.data) {
        this.data.pagesRead = series.pages;
        this.cdRef.markForCheck();
      }
      this.dataChanged.emit(series);
    });
  }

  handleClick() {
    if (this.previewOnClick) {
      const ref = this.offcanvasService.open(SeriesPreviewDrawerComponent, {position: 'end', panelClass: 'navbar-offset'});
      ref.componentInstance.isExternalSeries = false;
      ref.componentInstance.seriesId = this.data.id;
      ref.componentInstance.libraryId = this.data.libraryId;
      ref.componentInstance.name = this.data.name;
      return;
    }
    this.clicked.emit(this.data);
    this.router.navigate(['library', this.libraryId, 'series', this.data?.id]);
  }

}
