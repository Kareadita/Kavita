import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
import { NavService } from './nav.service';

@Injectable({
  providedIn: 'root'
})
export class ImageService {

  baseUrl = environment.apiUrl;
  public placeholderImage = 'assets/images/image-placeholder-min.png';
  public errorImage = 'assets/images/error-placeholder2-min.png';
  public resetCoverImage = 'assets/images/image-reset-cover-min.png';

  constructor(private navSerivce: NavService) {
    this.navSerivce.darkMode$.subscribe(res => {
      if (res) {
        this.placeholderImage = 'assets/images/image-placeholder.dark-min.png';
        this.errorImage = 'assets/images/error-placeholder2.dark-min.png';
      } else {
        this.placeholderImage = 'assets/images/image-placeholder-min.png';
        this.errorImage = 'assets/images/error-placeholder2-min.png';
      }
    });
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
    return this.baseUrl + 'image/chapter-cover?chapterId=' + chapterId + '&pageNum=' + pageNum;
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
    const r = Math.random() * 100 + 1;
    if (url.indexOf('&random') >= 0) {
      return url + 1;
    }
    return url + '&random=' + r;
  }
}
