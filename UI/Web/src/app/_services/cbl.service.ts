import {inject, Injectable} from '@angular/core';
import {HttpClient, HttpParams} from '@angular/common/http';
import {environment} from '../../environments/environment';
import {CblRepoBrowseResult} from '../_models/reading-list/cbl/cbl-repo-browse-result';
import {CblRepoItem} from '../_models/reading-list/cbl/cbl-repo-item';
import {CblImportSummary} from '../_models/reading-list/cbl/cbl-import-summary';
import {CblSavedFile} from '../_models/reading-list/cbl/cbl-saved-file';
import {CblImportDecisions} from '../_models/reading-list/cbl/cbl-import-decisions';
import {ReadingListProvider} from '../_models/reading-list';
import {RemapRule} from '../_models/reading-list/cbl/remap-rule';
import {Chapter} from '../_models/chapter';
import {NgxFileDropEntry} from 'ngx-file-drop';
import {TextResonse} from "../_types/text-response";

@Injectable({
  providedIn: 'root',
})
export class CblService {
  private readonly httpClient = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  browseRepo(path: string = '') {
    let params = new HttpParams();
    if (path !== '') {
      params = params.append('path', path);
    }
    return this.httpClient.get<CblRepoBrowseResult>(this.baseUrl + 'cbl/browse', {params: params});
  }

  importFromRepo(items: CblRepoItem[]) {
    return this.httpClient.post<CblSavedFile[]>(this.baseUrl + 'cbl/repo-import', {items});
  }

  importFromUrl(url: string) {
    return this.httpClient.post<CblSavedFile>(this.baseUrl + 'cbl/upload-cbl-file', {url});
  }

  importFromFile(file: File, fileEntry: NgxFileDropEntry) {
    const formData = new FormData();
    formData.append('cblFile', file, fileEntry.relativePath);
    return this.httpClient.post<CblSavedFile>(this.baseUrl + 'cbl/file-import', formData);
  }

  reValidate(fileName: string) {
    return this.httpClient.post<CblImportSummary>(this.baseUrl + 'cbl/re-validate', {fileName});
  }

  finalizeImport(fileName: string, decisions: CblImportDecisions, provider: ReadingListProvider,
    repoMeta?: { repoPath: string; downloadUrl: string; sha: string }) {
    return this.httpClient.post<CblImportSummary>(this.baseUrl + 'cbl/finalize-import', {
      fileName,
      decisions,
      provider,
      ...repoMeta
    });
  }

  getRemapRules() {
    return this.httpClient.get<RemapRule[]>(this.baseUrl + 'cbl/remap-rules');
  }

  createRemapRule(cblSeriesName: string, seriesId: number, issueDetail?: {
    cblVolume?: string; cblNumber?: string; volumeId?: number; chapterId?: number;
  }) {
    return this.httpClient.post<RemapRule>(this.baseUrl + 'cbl/remap-rules', {
      cblSeriesName, seriesId, ...issueDetail
    });
  }

  syncList(readingListId: number) {
    return this.httpClient.post(this.baseUrl + 'cbl/sync?readingListId=' + readingListId, {}, TextResonse);
  }

  updateRemapRule(id: number, update: { seriesId?: number; cblSeriesName?: string; volumeId?: number; chapterId?: number; cblVolume?: string; cblNumber?: string }) {
    return this.httpClient.post<RemapRule>(this.baseUrl + 'cbl/remap-rules/' + id, update);
  }

  deleteRemapRule(id: number) {
    return this.httpClient.delete<void>(this.baseUrl + 'cbl/remap-rules/' + id);
  }

  getAllRemapRules() {
    return this.httpClient.get<RemapRule[]>(this.baseUrl + 'cbl/remap-rules/all');
  }

  promoteRule(id: number) {
    return this.httpClient.post<RemapRule>(this.baseUrl + 'cbl/remap-rules/' + id + '/promote', {});
  }

  demoteRule(id: number) {
    return this.httpClient.post<RemapRule>(this.baseUrl + 'cbl/remap-rules/' + id + '/demote', {});
  }

  buildChapterStub(rule: RemapRule): Chapter {
    return {
      volumeId: 0,
      range: rule.chapterRange,
      titleName: rule.chapterTitleName !== rule.chapterRange ? rule.chapterTitleName : '',
      isSpecial: rule.chapterIsSpecial,
    } as Chapter;
  }

}
