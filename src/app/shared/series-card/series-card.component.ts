import { Component, EventEmitter, Input, OnChanges, OnInit, Output } from '@angular/core';
import { Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs/operators';
import { Series } from 'src/app/_models/series';
import { AccountService } from 'src/app/_services/account.service';
import { LibraryService } from 'src/app/_services/library.service';
import { SeriesService } from 'src/app/_services/series.service';
import { CardItemAction } from '../card-item/card-item.component';

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

  // TODO: Think about making this a stateless component
  constructor(private accountService: AccountService, private router: Router,
              private seriesService: SeriesService, private toastr: ToastrService,
              private libraryService: LibraryService) {
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
    this.actions = [
      {
        title: 'Mark as Read',
        callback: () => this.markAsRead
      },
      {
        title: 'Mark as Unread',
        callback: () => this.markAsUnread
      }
    ];

    if (this.isAdmin) {
      this.actions.push({title: 'Scan Library', callback: (data: Series) => {
        this.libraryService.scan(this.libraryId).subscribe((res: any) => {
          this.toastr.success('Scan started for ' + data.name);
        });
      }});

      this.actions.push({title: 'Delete', callback: (data: Series) => {
        if (!confirm('Are you sure you want to delete this series? It will not modify files on disk.')) {
          return;
        }
        this.seriesService.delete(data.id).subscribe((res: boolean) => {
          if (res) {
            this.toastr.success('Series deleted');
            this.reload.emit(true);
          }
        });
      }});

      this.actions.push({title: 'Get Info', callback: (data: Series) => {
        // TODO: Open Info modal
      }});
    }
  }

  markAsUnread(series: any) {

  }

  markAsRead(series: any) {

  }

  handleClick() {
    this.clicked.emit(this.data);
    this.router.navigate(['library', this.libraryId, 'series', this.data?.id]);
  }

}
