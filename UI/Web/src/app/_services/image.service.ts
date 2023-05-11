import { Injectable, OnDestroy } from '@angular/core';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { environment } from 'src/environments/environment';
import { ThemeService } from './theme.service';
import { RecentlyAddedItem } from '../_models/recently-added-item';
import { AccountService } from './account.service';

@Injectable({
  providedIn: 'root'
})
export class ImageService implements OnDestroy {

  baseUrl = environment.apiUrl;
  apiKey: string = '';
  encodedKey: string = '';
  public placeholderImage = 'assets/images/image-placeholder.dark-min.png';
  public errorImage = 'assets/images/error-placeholder2.dark-min.png';
  public resetCoverImage = 'assets/images/image-reset-cover-min.png';
  public errorWebLinkImage = 'assets/images/image-placeholder.dark-min.png';

  private onDestroy: Subject<void> = new Subject();

  constructor(private accountService: AccountService, private themeService: ThemeService) {
    this.themeService.currentTheme$.pipe(takeUntil(this.onDestroy)).subscribe(theme => {
      if (this.themeService.isDarkTheme()) {
        this.placeholderImage = 'assets/images/image-placeholder.dark-min.png';
        this.errorImage = 'assets/images/error-placeholder2.dark-min.png';
        this.errorWebLinkImage = 'assets/images/image-placeholder-min.png';
      } else {
        this.placeholderImage = 'assets/images/image-placeholder-min.png';
        this.errorImage = 'assets/images/error-placeholder2-min.png';
        this.errorWebLinkImage = 'assets/images/image-placeholder-min.png';
      }
    });

    this.accountService.currentUser$.pipe(takeUntil(this.onDestroy)).subscribe(user => {
      if (user) {
        this.apiKey = user.apiKey;
        this.encodedKey = encodeURIComponent(this.apiKey);
      }
    });
  }

  ngOnDestroy(): void {
      this.onDestroy.next();
      this.onDestroy.complete();
  }

  getRecentlyAddedItem(item: RecentlyAddedItem) {
    if (item.chapterId === 0) {
      return this.getVolumeCoverImage(item.volumeId);
    }
    return this.getChapterCoverImage(item.chapterId);
  }

  /**
   * Returns the entity type from a cover image url. Undefied if not applicable
   * @param url 
   * @returns 
   */
  getEntityTypeFromUrl(url: string) {
    if (url.indexOf('?') < 0) return undefined;
    const part = url.split('?')[1];
    const equalIndex = part.indexOf('=');
    return part.substring(0, equalIndex).replace('Id', '');
  }

  getLibraryCoverImage(libraryId: number) {
    return `${this.baseUrl}image/library-cover?libraryId=${libraryId}&apiKey=${this.encodedKey}`;
  }

  getVolumeCoverImage(volumeId: number) {
    return `${this.baseUrl}image/volume-cover?volumeId=${volumeId}&apiKey=${this.encodedKey}`;
  }

  getSeriesCoverImage(seriesId: number) {
    return `${this.baseUrl}image/series-cover?seriesId=${seriesId}&apiKey=${this.encodedKey}`;
  }

  getCollectionCoverImage(collectionTagId: number) {
    return `${this.baseUrl}image/collection-cover?collectionTagId=${collectionTagId}&apiKey=${this.encodedKey}`;
  }

  getReadingListCoverImage(readingListId: number) {
    return `${this.baseUrl}image/readinglist-cover?readingListId=${readingListId}&apiKey=${this.encodedKey}`;
  }

  getChapterCoverImage(chapterId: number) {
    return `${this.baseUrl}image/chapter-cover?chapterId=${chapterId}&apiKey=${this.encodedKey}`;
  }

  getBookmarkedImage(chapterId: number, pageNum: number) {
    return `${this.baseUrl}image/bookmark?chapterId=${chapterId}&apiKey=${this.encodedKey}&pageNum=${pageNum}`;
  }

  getWebLinkImage(url: string) {
    return `${this.baseUrl}image/web-link?url=${encodeURIComponent(url)}&apiKey=${this.encodedKey}`;
  }

  getCoverUploadImage(filename: string) {
    return `${this.baseUrl}image/cover-upload?filename=${encodeURIComponent(filename)}&apiKey=${this.encodedKey}`;
  }

  updateErroredImage(event: any) {
    event.target.src = this.placeholderImage;
  }

  updateErroredWebLinkImage(event: any) {
    event.target.src = this.errorWebLinkImage;
  }

  /**
   * Used to refresh an existing loaded image (lazysizes). If random already attached, will append another number onto it.
   * @param url Existing request url from ImageService only
   * @returns Url with a random parameter attached
   */
  randomize(url: string) {
    const r = Math.round(Math.random() * 100 + 1);
    if (url.indexOf('&random') >= 0) {
      return url + 1;
    }
    return url + '&random=' + r;
  }
}
