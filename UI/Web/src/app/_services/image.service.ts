import { Injectable, OnDestroy } from '@angular/core';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { environment } from 'src/environments/environment';
import { ThemeService } from '../theme.service';
import { RecentlyAddedItem } from '../_models/recently-added-item';
import { AccountService } from './account.service';
import { NavService } from './nav.service';

@Injectable({
  providedIn: 'root'
})
export class ImageService implements OnDestroy {

  baseUrl = environment.apiUrl;
  apiKey: string = '';
  public placeholderImage = 'assets/images/image-placeholder-min.png';
  public errorImage = 'assets/images/error-placeholder2-min.png';
  public resetCoverImage = 'assets/images/image-reset-cover-min.png';

  private onDestroy: Subject<void> = new Subject();

  constructor(private navSerivce: NavService, private accountService: AccountService, private themeService: ThemeService) {
    // TODO: Figure out how to handle dark mode here (or to allow customization of images)
    this.themeService.currentTheme$.subscribe(res => {
      if (res.name.toLowerCase() === 'dark') {
        this.placeholderImage = 'assets/images/image-placeholder.dark-min.png';
        this.errorImage = 'assets/images/error-placeholder2.dark-min.png';
      } else {
        this.placeholderImage = 'assets/images/image-placeholder-min.png';
        this.errorImage = 'assets/images/error-placeholder2-min.png';
      }
    });

    this.accountService.currentUser$.pipe(takeUntil(this.onDestroy)).subscribe(user => {
      if (user) {
        this.apiKey = user.apiKey;
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

  getVolumeCoverImage(volumeId: number) {
    return this.baseUrl + 'image/volume-cover?volumeId=' + volumeId;
  }

  getSeriesCoverImage(seriesId: number) {
    return this.baseUrl + 'image/series-cover?seriesId=' + seriesId;
  }

  getCollectionCoverImage(collectionTagId: number) {
    return this.baseUrl + 'image/collection-cover?collectionTagId=' + collectionTagId;
  }

  getChapterCoverImage(chapterId: number) {
    return this.baseUrl + 'image/chapter-cover?chapterId=' + chapterId;
  }

  getBookmarkedImage(chapterId: number, pageNum: number) {
    return this.baseUrl + 'image/bookmark?chapterId=' + chapterId + '&pageNum=' + pageNum + '&apiKey=' + encodeURIComponent(this.apiKey);
  }

  updateErroredImage(event: any) {
    event.target.src = this.placeholderImage;
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
