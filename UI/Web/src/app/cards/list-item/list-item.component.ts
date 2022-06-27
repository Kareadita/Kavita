import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import { finalize, Observable, of, take, takeWhile } from 'rxjs';
import { Download } from 'src/app/shared/_models/download';
import { DownloadService } from 'src/app/shared/_services/download.service';
import { UtilityService } from 'src/app/shared/_services/utility.service';
import { Chapter } from 'src/app/_models/chapter';
import { LibraryType } from 'src/app/_models/library';
import { Series } from 'src/app/_models/series';
import { RelationKind } from 'src/app/_models/series-detail/relation-kind';
import { Volume } from 'src/app/_models/volume';
import { Action, ActionItem } from 'src/app/_services/action-factory.service';

@Component({
  selector: 'app-list-item',
  templateUrl: './list-item.component.html',
  styleUrls: ['./list-item.component.scss']
})
export class ListItemComponent implements OnInit {

  /**
   * Volume or Chapter to render
   */
  @Input() entity!: Volume | Chapter;
  /**
   * Image to show
   */
  @Input() imageUrl: string = '';
  /**
   * Actions to show
   */
  @Input() actions: ActionItem<any>[] = []; // Volume | Chapter
  /**
   * Library type to help with formatting title
   */
  @Input() libraryType: LibraryType = LibraryType.Manga;
  /**
   * Name of the Series to show under the title
   */
  @Input() seriesName: string = '';

  /**
   * Size of the Image Height. Defaults to 230px.
   */
  @Input() imageHeight: string = '230px';
  /**
   * Size of the Image Width Defaults to 158px.
   */
  @Input() imageWidth: string = '158px';
  @Input() seriesLink: string = '';

  @Input() pagesRead: number = 0;
  @Input() totalPages: number = 0;

  @Input() relation: RelationKind | undefined = undefined;

  /**
  * When generating the title, should this prepend 'Volume number' before the Chapter wording
  */
  @Input() includeVolume: boolean = false;
  /**
   * Show's the title if avaible on entity
   */
  @Input() showTitle: boolean = true;
  /**
   * Blur the summary for the list item
   */
  @Input() blur: boolean = false;

  @Output() read: EventEmitter<void> = new EventEmitter<void>();

  actionInProgress: boolean = false;
  summary$: Observable<string> = of('');
  summary: string = '';
  isChapter: boolean = false;
  

  download$: Observable<Download> | null = null;
  downloadInProgress: boolean = false;

  get Title() {
    if (this.isChapter) return (this.entity as Chapter).titleName;
    return '';
  }


  constructor(private utilityService: UtilityService, private downloadService: DownloadService, private toastr: ToastrService) { }

  ngOnInit(): void {

    this.isChapter = this.utilityService.isChapter(this.entity);
    if (this.isChapter) {
      this.summary = this.utilityService.asChapter(this.entity).summary || '';
    } else {
      this.summary = this.utilityService.asVolume(this.entity).chapters[0].summary || '';
    }
  }

  performAction(action: ActionItem<any>) {
    if (action.action == Action.Download) {
      if (this.downloadInProgress === true) {
        this.toastr.info('Download is already in progress. Please wait.');
        return;
      }
      
      if (this.utilityService.isVolume(this.entity)) {
        const volume = this.utilityService.asVolume(this.entity);
        this.downloadService.downloadVolumeSize(volume.id).pipe(take(1)).subscribe(async (size) => {
          const wantToDownload = await this.downloadService.confirmSize(size, 'volume');
          if (!wantToDownload) { return; }
          this.downloadInProgress = true;
          this.download$ = this.downloadService.downloadVolume(volume).pipe(
            takeWhile(val => {
              return val.state != 'DONE';
            }),
            finalize(() => {
              this.download$ = null;
              this.downloadInProgress = false;
            }));
        });
      } else if (this.utilityService.isChapter(this.entity)) {
        const chapter = this.utilityService.asChapter(this.entity);
        this.downloadService.downloadChapterSize(chapter.id).pipe(take(1)).subscribe(async (size) => {
          const wantToDownload = await this.downloadService.confirmSize(size, 'chapter');
          if (!wantToDownload) { return; }
          this.downloadInProgress = true;
          this.download$ = this.downloadService.downloadChapter(chapter).pipe(
            takeWhile(val => {
              return val.state != 'DONE';
            }),
            finalize(() => {
              this.download$ = null;
              this.downloadInProgress = false;
            }));
        });
      }
      return; // Don't propagate the download from a card
    }

    if (typeof action.callback === 'function') {
      action.callback(action.action, this.entity);
    }
  }
}
