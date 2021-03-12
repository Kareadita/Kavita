import { Component, EventEmitter, Input, OnChanges, OnInit, Output } from '@angular/core';
import { Router } from '@angular/router';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs/operators';
import { EditSeriesModalComponent } from 'src/app/_modals/edit-series-modal/edit-series-modal.component';
import { Series } from 'src/app/_models/series';
import { AccountService } from 'src/app/_services/account.service';
import { LibraryService } from 'src/app/_services/library.service';
import { ReaderService } from 'src/app/_services/reader.service';
import { SeriesService } from 'src/app/_services/series.service';
import { CardItemAction } from '../card-item/card-item.component';
import { ConfirmService } from '../confirm.service';

@Component({
  selector: 'app-series-card',
  templateUrl: './series-card.component.html',
  styleUrls: ['./series-card.component.scss']
})
export class SeriesCardComponent implements OnInit, OnChanges {
  @Input() data: Series | undefined;
  @Input() libraryId = 0;
  @Output() clicked = new EventEmitter<Series>();
  @Output() reload = new EventEmitter<boolean>();

  isAdmin = false;
  actions: CardItemAction[] = [];

  constructor(private accountService: AccountService, private router: Router,
              private seriesService: SeriesService, private toastr: ToastrService,
              private libraryService: LibraryService, private modalService: NgbModal,
              private confirmService: ConfirmService, public readerService: ReaderService) {
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
      this.generateActions();
    }
  }

  generateActions() {
    // TODO: Move this into a Factory with an observable/callback so we can handle after it's done
    this.actions = [
      {
        title: 'Mark as Read',
        callback: (data: any) => this.markAsRead(data)
      },
      {
        title: 'Mark as Unread',
        callback: (data: any) => this.markAsUnread(data)
      }
    ];

    if (this.isAdmin) {
      this.actions.push({title: 'Scan Library', callback: (data: Series) => {
        this.libraryService.scan(this.libraryId).subscribe((res: any) => {
          this.toastr.success('Scan started for ' + data.name);
        });
      }});

      this.actions.push({title: 'Delete', callback: async (data: Series) => {
        if (!await this.confirmService.confirm('Are you sure you want to delete this series? It will not modify files on disk.')) {
          return;
        }

        this.seriesService.delete(data.id).subscribe((res: boolean) => {
          if (res) {
            this.toastr.success('Series deleted');
            this.reload.emit(true);
          }
        });
      }});

      this.actions.push({title: 'Edit', callback: (data: Series) => {
        const modalRef = this.modalService.open(EditSeriesModalComponent, {  size: 'lg' });
        modalRef.componentInstance.series = data;
        modalRef.closed.subscribe((closeResult: {success: boolean, series: Series}) => {
          window.scrollTo(0, 0);
          if (closeResult.success) {
            this.seriesService.getSeries(data.id).subscribe(series => {
              this.data = series;
            });
          }
        });
      }});
    }
  }

  markAsUnread(series: Series) {
    this.seriesService.markUnread(series.id).subscribe(res => {
      this.toastr.success(series.name + ' is now unread');
      series.pagesRead = 0;
    });
  }

  markAsRead(series: Series) {
    this.seriesService.markRead(series.id).subscribe(res => {
      this.toastr.success(series.name + ' is now read');
      series.pagesRead = series.pages;
    });
  }

  handleClick() {
    this.clicked.emit(this.data);
    this.router.navigate(['library', this.libraryId, 'series', this.data?.id]);
  }

}
