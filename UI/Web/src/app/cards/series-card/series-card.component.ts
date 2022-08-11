import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Input, OnChanges, OnDestroy, OnInit, Output } from '@angular/core';
import { Router } from '@angular/router';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs/operators';
import { Series } from 'src/app/_models/series';
import { AccountService } from 'src/app/_services/account.service';
import { ImageService } from 'src/app/_services/image.service';
import { ActionFactoryService, Action, ActionItem } from 'src/app/_services/action-factory.service';
import { SeriesService } from 'src/app/_services/series.service';
import { ActionService } from 'src/app/_services/action.service';
import { EditSeriesModalComponent } from '../_modals/edit-series-modal/edit-series-modal.component';
import { Subject } from 'rxjs';
import { RelationKind } from 'src/app/_models/series-detail/relation-kind';

@Component({
  selector: 'app-series-card',
  templateUrl: './series-card.component.html',
  styleUrls: ['./series-card.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeriesCardComponent implements OnInit, OnChanges, OnDestroy {
  @Input() data!: Series;
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

  @Output() clicked = new EventEmitter<Series>();
  @Output() reload = new EventEmitter<boolean>();
  @Output() dataChanged = new EventEmitter<Series>();
  /**
   * When the card is selected.
   */
   @Output() selection = new EventEmitter<boolean>();

  actions: ActionItem<Series>[] = [];
  imageUrl: string = '';
  onDestroy: Subject<void> = new Subject<void>();

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
      this.actions = this.actionFactoryService.getSeriesActions((action: Action, series: Series) => this.handleSeriesActionCallback(action, series));
      this.cdRef.markForCheck();
    }
  }

  ngOnDestroy() {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  handleSeriesActionCallback(action: Action, series: Series) {
    switch (action) {
      case(Action.MarkAsRead):
        this.markAsRead(series);
        break;
      case(Action.MarkAsUnread):
        this.markAsUnread(series);
        break;
      case(Action.ScanLibrary):
        this.scanLibrary(series);
        break;
      case(Action.RefreshMetadata):
        this.refreshMetdata(series);
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
        break;
      case(Action.AddToCollection):
        this.actionService.addMultipleSeriesToCollectionTag([series]);
        break;
      case (Action.AnalyzeFiles):
        this.actionService.analyzeFilesForSeries(series);
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
          this.reload.emit(true);
          this.dataChanged.emit(series);
        });
      }
    });
  }

  async refreshMetdata(series: Series) {
    this.actionService.refreshMetdata(series);
  }

  async scanLibrary(series: Series) {
    this.seriesService.scan(series.libraryId, series.id).subscribe((res: any) => {
      this.toastr.success('Scan queued for ' + series.name);
    });
  }

  async deleteSeries(series: Series) {
    this.actionService.deleteSeries(series, (result: boolean) => {
      if (result) {
        this.reload.emit(true);
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
    this.clicked.emit(this.data);
    this.router.navigate(['library', this.libraryId, 'series', this.data?.id]);
  }

}
