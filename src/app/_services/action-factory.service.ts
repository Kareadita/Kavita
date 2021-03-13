import { Injectable } from '@angular/core';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs/operators';
import { CardItemAction } from '../shared/card-item/card-item.component';
import { ConfirmService } from '../shared/confirm.service';
import { EditSeriesModalComponent } from '../_modals/edit-series-modal/edit-series-modal.component';
import { Chapter } from '../_models/chapter';
import { Series } from '../_models/series';
import { User } from '../_models/user';
import { Volume } from '../_models/volume';
import { AccountService } from './account.service';
import { LibraryService } from './library.service';
import { SeriesService } from './series.service';

export enum Action {
  MarkAsRead = 0,
  MarkAsUnread = 1,
  ScanLibrary = 2,
  Delete = 3,
  Edit = 4,
  Info = 5
}

export interface ActionItem<T> {
  title: string;
  action: Action;
  callback: (action: Action, data: T) => void;

}

@Injectable({
  providedIn: 'root'
})
export class ActionFactoryService {

  seriesActions: Array<ActionItem<Series>> = [
    {
      action: Action.MarkAsRead,
      title: 'Mark as Read',
      callback: this.dummyCallback
    },
    {
      action: Action.MarkAsUnread,
      title: 'Mark as Unread',
      callback: this.dummyCallback
    }
  ];

  volumeActions: Array<ActionItem<Volume>> = [
    {
      action: Action.MarkAsRead,
      title: 'Mark as Read',
      callback: this.dummyCallback
    },
    {
      action: Action.MarkAsUnread,
      title: 'Mark as Unread',
      callback: this.dummyCallback
    }
  ];

  chapterActions: Array<ActionItem<Chapter>> = [
    {
      action: Action.MarkAsRead,
      title: 'Mark as Read',
      callback: this.dummyCallback
    },
    {
      action: Action.MarkAsUnread,
      title: 'Mark as Unread',
      callback: this.dummyCallback
    }
  ];

  isAdmin = false;

  constructor(private accountService: AccountService, private seriesService: SeriesService, private toastr: ToastrService) {
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) {
        this.isAdmin = this.accountService.hasAdminRole(user);
      }

      if (this.isAdmin) {
        this.seriesActions.push({
          action: Action.ScanLibrary,
          title: 'Scan Library',
          callback: this.dummyCallback
        });

        this.seriesActions.push({
          action: Action.Delete,
          title: 'Delete',
          callback: this.dummyCallback
        });

        this.seriesActions.push({
          action: Action.Edit,
          title: 'Edit',
          callback: this.dummyCallback
        });

        this.volumeActions.push({
          action: Action.Info,
          title: 'Info',
          callback: this.dummyCallback
        });

        this.chapterActions.push({
          action: Action.Info,
          title: 'Info',
          callback: this.dummyCallback
        });
      }
    });
  }


  getSeriesActions(callback: (action: Action, series: Series) => void) {
    this.seriesActions.forEach(action => action.callback = callback);
    return this.seriesActions;
  }

  getVolumeActions(callback: (action: Action, volume: Volume) => void) {
    this.volumeActions.forEach(action => action.callback = callback);
    return this.volumeActions;
  }

  getChapterActions(callback: (action: Action, chapter: Chapter) => void) {
    this.chapterActions.forEach(action => action.callback = callback);
    return this.chapterActions;
  }

  dummyCallback(action: Action, data: any) {}
}
