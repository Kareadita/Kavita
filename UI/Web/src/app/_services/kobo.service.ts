import {HttpClient} from '@angular/common/http';
import {inject, Injectable} from '@angular/core';
import {environment} from '../../environments/environment';
import {KoboSyncPoint} from '../_models/preferences/kobo-sync-point';
import {ChapterRestoreInfo} from '../_models/chapter-restore-info';

@Injectable({
  providedIn: 'root'
})
export class KoboService {

  private readonly httpClient = inject(HttpClient);

  baseUrl = environment.apiUrl;

  getSyncPoints() {
    return this.httpClient.get<KoboSyncPoint[]>(this.baseUrl + 'users/kobo-sync-points');
  }

  deleteSyncPoint(syncPointId: number) {
    return this.httpClient.delete(this.baseUrl + 'users/kobo-sync-point?syncPointId=' + syncPointId);
  }

  undoRemoveBooks(syncPointId: number, undoBookIds: number[]) {
    return this.httpClient.post(this.baseUrl + 'users/kobo-sync-point/undo-remove-books', {syncPointId, undoBookIds});
  }

  getChapterRestoreInfo(syncPointId: number) {
    return this.httpClient.get<ChapterRestoreInfo[]>(this.baseUrl + 'chapter/restore-info?syncPointId=' + syncPointId);
  }
}
