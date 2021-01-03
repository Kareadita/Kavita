import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs/operators';
import { Series } from 'src/app/_models/series';
import { AccountService } from 'src/app/_services/account.service';
import { SeriesService } from 'src/app/_services/series.service';
import { CardItemAction } from '../card-item/card-item.component';

@Component({
  selector: 'app-series-card',
  templateUrl: './series-card.component.html',
  styleUrls: ['./series-card.component.scss']
})
export class SeriesCardComponent implements OnInit {
  @Input() data: Series | undefined;
  @Input() libraryId = 0;
  @Output() clicked = new EventEmitter<Series>();

  isAdmin = false;
  actions: CardItemAction[] = [];

  constructor(private accountService: AccountService, private router: Router,
              private seriesService: SeriesService, private toastr: ToastrService) {
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
      // this.actions.push({title: 'Scan Library', callback: (data: Library) => {
      //   this.libraryService.scan(this.libraryId).subscribe((res: any) => {
      //     this.toastr.success('Scan started for ' + data.name);
      //   });
      // }});
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
