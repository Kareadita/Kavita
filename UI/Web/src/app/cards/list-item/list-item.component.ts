import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, DestroyRef,
  EventEmitter,
  inject,
  Input,
  OnInit,
  Output
} from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import { map, Observable } from 'rxjs';
import { Download } from 'src/app/shared/_models/download';
import { DownloadEvent, DownloadService } from 'src/app/shared/_services/download.service';
import {Breakpoint, UtilityService} from 'src/app/shared/_services/utility.service';
import { Chapter } from 'src/app/_models/chapter';
import { LibraryType } from 'src/app/_models/library';
import { RelationKind } from 'src/app/_models/series-detail/relation-kind';
import { Volume } from 'src/app/_models/volume';
import { Action, ActionItem } from 'src/app/_services/action-factory.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {ReadMoreComponent} from "../../shared/read-more/read-more.component";
import {CommonModule} from "@angular/common";
import {ImageComponent} from "../../shared/image/image.component";
import {DownloadIndicatorComponent} from "../download-indicator/download-indicator.component";
import {EntityInfoCardsComponent} from "../entity-info-cards/entity-info-cards.component";
import {NgbProgressbar, NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {translate, TranslocoDirective} from "@ngneat/transloco";
import {CardActionablesComponent} from "../../_single-module/card-actionables/card-actionables.component";

@Component({
  selector: 'app-list-item',
  standalone: true,
  imports: [CommonModule, ReadMoreComponent, ImageComponent, DownloadIndicatorComponent, EntityInfoCardsComponent, CardActionablesComponent, NgbProgressbar, NgbTooltip, TranslocoDirective],
  templateUrl: './list-item.component.html',
  styleUrls: ['./list-item.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ListItemComponent implements OnInit {

  /**
   * Volume or Chapter to render
   */
  @Input({required: true}) entity!: Volume | Chapter;
  @Input({required: true}) libraryId!: number;
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
   * Show's the title if available on entity
   */
  @Input() showTitle: boolean = true;
  /**
   * Blur the summary for the list item
   */
  @Input() blur: boolean = false;

  @Output() read: EventEmitter<void> = new EventEmitter<void>();
  private readonly destroyRef = inject(DestroyRef);

  actionInProgress: boolean = false;
  summary: string = '';
  isChapter: boolean = false;


  download$: Observable<DownloadEvent | null> | null = null;
  downloadInProgress: boolean = false;

  get Title() {
    if (this.isChapter) return (this.entity as Chapter).titleName;
    return '';
  }

  get ShowExtended() {
    return this.utilityService.getActiveBreakpoint() === Breakpoint.Desktop;
  }

  protected readonly Breakpoint = Breakpoint;


  constructor(public utilityService: UtilityService, private downloadService: DownloadService,
    private toastr: ToastrService, private readonly cdRef: ChangeDetectorRef) { }

  ngOnInit(): void {
    this.isChapter = this.utilityService.isChapter(this.entity);
    if (this.isChapter) {
      this.summary = this.utilityService.asChapter(this.entity).summary || '';
    } else {
      this.summary = this.utilityService.asVolume(this.entity).chapters[0].summary || '';
    }

    this.cdRef.markForCheck();


    this.download$ = this.downloadService.activeDownloads$.pipe(takeUntilDestroyed(this.destroyRef), map((events) => {
      if(this.utilityService.isVolume(this.entity)) return events.find(e => e.entityType === 'volume' && e.subTitle === this.downloadService.downloadSubtitle('volume', (this.entity as Volume))) || null;
      if(this.utilityService.isChapter(this.entity)) return events.find(e => e.entityType === 'chapter' && e.subTitle === this.downloadService.downloadSubtitle('chapter', (this.entity as Chapter))) || null;
      return null;
    }));
  }

  performAction(action: ActionItem<any>) {
    if (action.action == Action.Download) {
      if (this.downloadInProgress) {
        this.toastr.info(translate('toasts.download-in-progress'));
        return;
      }

      const statusUpdate = (d: Download | undefined) => {
        if (d) return;
        this.downloadInProgress = false;
      };

      if (this.utilityService.isVolume(this.entity)) {
        const volume = this.utilityService.asVolume(this.entity);
        this.downloadService.download('volume', volume, statusUpdate);
      } else if (this.utilityService.isChapter(this.entity)) {
        const chapter = this.utilityService.asChapter(this.entity);
        this.downloadService.download('chapter', chapter, statusUpdate);
      }
      return; // Don't propagate the download from a card
    }

    if (typeof action.callback === 'function') {
      action.callback(action, this.entity);
    }
  }
}
