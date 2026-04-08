import {ChangeDetectionStrategy, Component, computed, inject, input, output} from '@angular/core';
import {ReadingListItem} from 'src/app/_models/reading-list';
import {ImageService} from 'src/app/_services/image.service';
import {NgbProgressbar} from '@ng-bootstrap/ng-bootstrap';
import {ImageComponent} from '../../../shared/image/image.component';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ItemRemoveEvent} from "../draggable-ordered-list/draggable-ordered-list.component";
import {RouterLink} from "@angular/router";
import {AccountService} from "../../../_services/account.service";
import {BlurToggleDirective} from "../../../_directives/blur-toggle.directive";
import {LooseLeafOrDefaultNumber} from "../../../_models/chapter";
import {DateYearRangePipe, NULL_DATE} from "../../../_pipes/date-year-range.pipe";
import {DefaultValuePipe} from "../../../_pipes/default-value.pipe";

@Component({
  selector: 'app-reading-list-item',
  templateUrl: './reading-list-item.component.html',
  styleUrls: ['./reading-list-item.component.scss'],
  imports: [ImageComponent, NgbProgressbar, TranslocoDirective, RouterLink, BlurToggleDirective, DateYearRangePipe, DefaultValuePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReadingListItemComponent {

  protected readonly imageService = inject(ImageService);
  private readonly accountService = inject(AccountService);

  item = input.required<ReadingListItem>();
  position = input(0);
  editMode = input(false);

  read = output<ReadingListItem>();
  remove = output<ItemRemoveEvent>();

  chapterTitle = computed(() => this.item().chapter?.titleName || this.item().title);
  chapterNumber = computed(() => this.item().chapter?.range || this.item().chapterNumber);
  renderChapterNumber = computed(() => {
    const chNum = this.chapterNumber();
    const volNum = this.item().volumeNumber;

    if (chNum === LooseLeafOrDefaultNumber + '') {
      return translate('common.volume-num-shorthand', {num: volNum});
    }
    return translate('common.issue-num-shorthand', {num: chNum})
  });
  releaseDate = computed(() => this.item().chapter?.releaseDate || this.item().releaseDate);
  summary = computed(() => this.item().chapter?.summary || this.item().summary);
  pages = computed(() => this.item().chapter?.pages ?? this.item().pagesTotal);
  writerName = computed(() => this.item().chapter?.writerName);
  pencillerName = computed(() => this.item().chapter?.pencillerName);

  isUnread = computed(() => this.item().pagesRead === 0 && this.pages() > 0);
  isInProgress = computed(() => this.item().pagesRead > 0 && this.item().pagesRead < this.pages());
  progressPercent = computed(() => {
    const total = this.pages();
    if (total === 0) return 0;
    return Math.round((this.item().pagesRead / total) * 100);
  });

  blurEnabled = computed(() => !!this.accountService.userPreferences()?.blurUnreadSummaries);
  shouldBlur = computed(() => this.blurEnabled() && this.isUnread());

  chapterDetailUrl = computed(() => {
    const item = this.item();
    return ['/library', item.libraryId, 'series', item.seriesId, 'chapter', item.chapterId];
  });

  seriesUrl = computed(() => {
    const item = this.item();
    return ['/library', item.libraryId, 'series', item.seriesId];
  });

  readChapter(item: ReadingListItem) {
    this.read.emit(item);
  }

  removeItem(item: ReadingListItem) {
    this.remove.emit({
      item: item,
      position: item.order
    });
  }

  protected readonly NULL_DATE = NULL_DATE;
}
