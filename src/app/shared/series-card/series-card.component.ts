import { Component, EventEmitter, Input, OnChanges, OnInit, Output } from '@angular/core';
import { Router } from '@angular/router';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs/operators';
import { EditSeriesModalComponent } from 'src/app/_modals/edit-series-modal/edit-series-modal.component';
import { Series } from 'src/app/_models/series';
import { AccountService } from 'src/app/_services/account.service';
import { ImageService } from 'src/app/_services/image.service';
import { LibraryService } from 'src/app/_services/library.service';
import { ActionFactoryService, Action, ActionItem } from 'src/app/_services/action-factory.service';
import { SeriesService } from 'src/app/_services/series.service';
import { ConfirmService } from '../confirm.service';

@Component({
  selector: 'app-series-card',
  templateUrl: './series-card.component.html',
  styleUrls: ['./series-card.component.scss']
})
export class SeriesCardComponent implements OnInit, OnChanges {
  @Input() data: Series | undefined;
  @Input() libraryId = 0;
  @Input() suppressLibraryLink = false;
  @Output() clicked = new EventEmitter<Series>();
  @Output() reload = new EventEmitter<boolean>();
  @Output() dataChanged = new EventEmitter<Series>();

  isAdmin = false;
  actions: ActionItem<Series>[] = [];

  constructor(private accountService: AccountService, private router: Router,
              private seriesService: SeriesService, private toastr: ToastrService,
              private libraryService: LibraryService, private modalService: NgbModal,
              private confirmService: ConfirmService, public imageService: ImageService,
              private actionFactoryService: ActionFactoryService) {
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) {
        this.isAdmin = this.accountService.hasAdminRole(user);
      }
    });
  }


  ngOnInit(): void {
  }

  ngOnChanges(changes: any) {
    if (this.data) {
      this.actions = this.actionFactoryService.getSeriesActions((action: Action, series: Series) => this.handleSeriesActionCallback(action, series));
    }
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
      default:
        break;
    }
  }

  openEditModal(data: Series) {
    const modalRef = this.modalService.open(EditSeriesModalComponent, {  size: 'lg', scrollable: true });
    modalRef.componentInstance.series = data;
    modalRef.closed.subscribe((closeResult: {success: boolean, series: Series}) => {
      window.scrollTo(0, 0);
      if (closeResult.success) {
        this.seriesService.getSeries(data.id).subscribe(series => {
          this.data = series;
          this.reload.emit(true);
          this.dataChanged.emit(series);
        });
      }
    });
  }

  refreshMetdata(series: Series) {
    this.seriesService.refreshMetadata(series).subscribe((res: any) => {
      this.toastr.success('Refresh started for ' + series.name);
    });
  }

  scanLibrary(series: Series) {
    this.libraryService.scan(series.libraryId).subscribe((res: any) => {
      this.toastr.success('Scan started for ' + series.name);
    });
  }

  async deleteSeries(series: Series) {
    if (!await this.confirmService.confirm('Are you sure you want to delete this series? It will not modify files on disk.')) {
      return;
    }

    this.seriesService.delete(series.id).subscribe((res: boolean) => {
      if (res) {
        this.toastr.success('Series deleted');
        this.reload.emit(true);
      }
    });
  }

  markAsUnread(series: Series) {
    this.seriesService.markUnread(series.id).subscribe(res => {
      this.toastr.success(series.name + ' is now unread');
      series.pagesRead = 0;
      if (this.data) {
        this.data.pagesRead = 0;
      }
      
      this.dataChanged.emit(series);
    });
  }

  markAsRead(series: Series) {
    this.seriesService.markRead(series.id).subscribe(res => {
      this.toastr.success(series.name + ' is now read');
      series.pagesRead = series.pages;
      if (this.data) {
        this.data.pagesRead = series.pages;
      }
      this.dataChanged.emit(series);
    });
  }

  handleClick() {
    this.clicked.emit(this.data);
    this.router.navigate(['library', this.libraryId, 'series', this.data?.id]);
  }

}
