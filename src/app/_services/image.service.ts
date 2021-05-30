import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';

@Injectable({
  providedIn: 'root'
})
export class ImageService {

  baseUrl = environment.apiUrl;
  public placeholderImage = 'assets/images/image-placeholder-min.png';
  public errorImage = 'assets/images/error-placeholder2-min.png';

  constructor() { }

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
}
