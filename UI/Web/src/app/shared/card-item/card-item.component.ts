import { Component, EventEmitter, Input, OnDestroy, OnInit, Output } from '@angular/core';
import { Observable, Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { Chapter } from 'src/app/_models/chapter';
import { CollectionTag } from 'src/app/_models/collection-tag';
import { MangaFormat } from 'src/app/_models/manga-format';
import { Series } from 'src/app/_models/series';
import { Volume } from 'src/app/_models/volume';
import { Action, ActionItem } from 'src/app/_services/action-factory.service';
import { ImageService } from 'src/app/_services/image.service';
import { LibraryService } from 'src/app/_services/library.service';
import { Download } from '../_models/download';
import { DownloadService } from '../_services/download.service';
import { UtilityService } from '../_services/utility.service';

@Component({
  selector: 'app-card-item',
  templateUrl: './card-item.component.html',
  styleUrls: ['./card-item.component.scss']
})
export class CardItemComponent implements OnInit, OnDestroy {

  @Input() imageUrl = '';
  @Input() title = '';
  @Input() actions: ActionItem<any>[] = [];
  @Input() read = 0; // Pages read
  @Input() total = 0; // Total Pages
  @Input() supressLibraryLink = false;
  @Input() entity!: Series | Volume | Chapter | CollectionTag; // This is the entity we are representing. It will be returned if an action is executed.
  @Output() clicked = new EventEmitter<string>();

  libraryName: string | undefined = undefined; // Library name item belongs to
  libraryId: number | undefined = undefined; 
  supressArchiveWarning: boolean = false; // This will supress the cannot read archive warning when total pages is 0
  format: MangaFormat = MangaFormat.UNKNOWN;

  showDownloadNotification: boolean = false;
  download$!: Observable<Download>;

  get MangaFormat(): typeof MangaFormat {
    return MangaFormat;
  }

  private readonly onDestroy = new Subject<void>();

  constructor(public imageSerivce: ImageService, private libraryService: LibraryService, public utilityService: UtilityService, private downloadService: DownloadService) {}

  ngOnInit(): void {
    if (this.entity.hasOwnProperty('promoted') && this.entity.hasOwnProperty('title')) {
      this.supressArchiveWarning = true;
    }

    if (this.supressLibraryLink === false) {
      this.libraryService.getLibraryNames().pipe(takeUntil(this.onDestroy)).subscribe(names => {
        if (this.entity !== undefined && this.entity.hasOwnProperty('libraryId')) {
          this.libraryId = (this.entity as Series).libraryId;
          this.libraryName = names[this.libraryId];
        }
      });
    }
    this.format = (this.entity as Series).format;
  }

  ngOnDestroy() {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  handleClick() {
    this.clicked.emit(this.title);
  }

  isNullOrEmpty(val: string) {
    return val === null || val === undefined || val === '';
  }

  preventClick(event: any) {
    event.stopPropagation();
    event.preventDefault();
  }

  performAction(action: ActionItem<any>) {
    if (typeof action.callback === 'function') {
      action.callback(action.action, this.entity);
    }

    if (action.action == Action.Download) {
      this.showDownloadNotification = true;
      if (this.entity.hasOwnProperty('chapters')) {
        const volume = (this.entity as Volume);
        this.download$ = this.downloadService.downloadVolume(volume, '');
      }
      
    }
  }

  isPromoted() {
    const tag = this.entity as CollectionTag;
    return tag.hasOwnProperty('promoted') && tag.promoted;
  }
}
