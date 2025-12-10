import {computed, inject, Injectable, signal} from '@angular/core';
import {environment} from "../../environments/environment";
import {HttpClient, HttpParams} from "@angular/common/http";
import {Annotation} from '../book-reader/_models/annotations/annotation';
import {TextResonse} from "../_types/text-response";
import {asyncScheduler, map, of, tap} from "rxjs";
import {switchMap, throttleTime} from "rxjs/operators";
import {AccountService} from "./account.service";
import {User} from "../_models/user/user";
import {MessageHubService} from "./message-hub.service";
import {RgbaColor} from "../book-reader/_models/annotations/highlight-slot";
import {Router} from "@angular/router";
import {SAVER, Saver} from "../_providers/saver.provider";
import {download} from "../shared/_models/download";
import {DEBOUNCE_TIME} from "../shared/_services/download.service";
import {FilterV2} from "../_models/metadata/v2/filter-v2";
import {AnnotationsFilterField, AnnotationsSortField} from "../_models/metadata/v2/annotations-filter";
import {UtilityService} from "../shared/_services/utility.service";
import {PaginatedResult} from "../_models/pagination";

/**
 * Represents any modification (create/delete/edit) that occurs to annotations
 */
export interface AnnotationEvent {
  pageNumber: number;
  type: 'create' | 'delete' | 'edit';
  annotation: Annotation;

}

@Injectable({
  providedIn: 'root'
})
export class AnnotationService {

  private readonly httpClient = inject(HttpClient);
  private readonly accountService = inject(AccountService);
  private readonly utilityService = inject(UtilityService);
  private readonly messageHub = inject(MessageHubService);
  private readonly router = inject(Router);
  private readonly baseUrl = environment.apiUrl;
  private readonly save = inject<Saver>(SAVER);

  private _annotations = signal<Annotation[]>([]);
  /**
   * Annotations for a given book
   */
  public readonly annotations = this._annotations.asReadonly();

  private _events = signal<AnnotationEvent | null>(null);
  public readonly events = this._events.asReadonly();

  private readonly user = signal<User | null>(null);
  public readonly slots = computed(() => {
    const currentUser = this.user();

    return currentUser?.preferences?.bookReaderHighlightSlots ?? [];
  });

  constructor() {
    this.accountService.currentUser$.subscribe(user => {
      this.user.set(user!);
    });
  }

  updateSlotColor(index: number, color: RgbaColor) {
    const user = this.accountService.currentUserSignal();
    if (!user) return of([]);

    const preferences = user.preferences;
    preferences.bookReaderHighlightSlots[index].color = color;

    return this.accountService.updatePreferences(preferences).pipe(
      map((p) => p.bookReaderHighlightSlots)
    );
  }

  getAllAnnotations(chapterId: number) {
    return this.httpClient.get<Array<Annotation>>(this.baseUrl + 'annotation/all?chapterId=' + chapterId).pipe(map(annotations => {
      this._annotations.set(annotations);
      return annotations;
    }));
  }

  getAllAnnotationsFiltered(filter: FilterV2<AnnotationsFilterField, AnnotationsSortField>, pageNum?: number, itemsPerPage?: number) {
    const params = this.utilityService.addPaginationIfExists(new HttpParams(), pageNum, itemsPerPage);

    return this.httpClient.post<PaginatedResult<Annotation>[]>(this.baseUrl + 'annotation/all-filtered', filter, {observe: 'response', params}).pipe(
      map((res: any) => {
        return this.utilityService.createPaginatedResult<Annotation>(res);
      }),
    );
  }

  getAnnotationsForSeries(seriesId: number) {
    return this.httpClient.get<Array<Annotation>>(this.baseUrl + 'annotation/all-for-series?seriesId=' + seriesId);
  }


  createAnnotation(data: Annotation) {
    return this.httpClient.post<Annotation>(this.baseUrl + 'annotation/create', data).pipe(
      tap(newAnnotation => {
        this._events.set({
          pageNumber: newAnnotation.pageNumber,
          type: 'create',
          annotation: newAnnotation
        });
      }),
      switchMap(newAnnotation => this.getAllAnnotations(newAnnotation.chapterId))
    );
  }

  updateAnnotation(data: Annotation) {
    return this.httpClient.post<Annotation>(this.baseUrl + 'annotation/update', data).pipe(
      switchMap(newAnnotation => this.getAllAnnotations(data.chapterId)),
      tap(_ => {
        this._events.set({
          pageNumber: data.pageNumber,
          type: 'edit',
          annotation: data
        });
      }),
    );
  }

  getAnnotation(annotationId: number) {
    return this.httpClient.get<Annotation>(this.baseUrl + `annotation/${annotationId}`);
  }

  /**
   * Deletes an annotation without it needing to be loading in the signal.
   * Used in the ViewEditAnnotationDrawer. Event is still fired.
   * @param annotation
   */
  deleteAnnotation(annotation: Annotation) {
    const id = annotation.id;

    return this.httpClient.delete(this.baseUrl + `annotation?annotationId=${id}`, TextResonse).pipe(tap(_ => {
      const annotations = this._annotations();
      this._annotations.set(annotations.filter(a => a.id !== id));

      this._events.set({
        pageNumber: annotation.pageNumber,
        type: 'delete',
        annotation: annotation
      });
    }));
  }

  delete(id: number) {
    const filtered = this.annotations().filter(a => a.id === id);
    if (filtered.length === 0) return of();
    const annotationToDelete = filtered[0];

    return this.deleteAnnotation(annotationToDelete);
  }

  /**
   * While this method will update the services annotations list. No events will be sent out.
   * Deletion on the callers' side should be handled in the rxjs chain.
   * @param ids
   */
  bulkDelete(ids: number[]) {
    return this.httpClient.post(this.baseUrl + "annotation/bulk-delete", ids).pipe(
      tap(() => {
        this._annotations.update(x => x.filter(a => !ids.includes(a.id)));
      }),
    );
  }

  /**
   * Routes to the book reader with the annotation in view
   * @param item
   */
  navigateToAnnotation(item: Annotation) {
    this.router.navigate(['/library', item.libraryId, 'series', item.seriesId, 'book', item.chapterId], { queryParams: { annotation: item.id } });
  }

  exportFilter(filter: FilterV2<AnnotationsFilterField, AnnotationsSortField>, pageNum?: number, itemsPerPage?: number) {
    const params = this.utilityService.addPaginationIfExists(new HttpParams(), pageNum, itemsPerPage);

    return this.httpClient.post(this.baseUrl + 'annotation/export-filter', filter, {
      observe: 'events',
      responseType: 'blob',
      reportProgress: true,
      params}).
    pipe(
      throttleTime(DEBOUNCE_TIME, asyncScheduler, { leading: true, trailing: true }),
      download((blob, filename) => {
        this.save(blob, decodeURIComponent(filename));
      })
    );
  }

  exportAnnotations(ids?: number[]) {
    return this.httpClient.post(this.baseUrl + 'annotation/export', ids, {observe: 'events', responseType: 'blob', reportProgress: true}).pipe(
      throttleTime(DEBOUNCE_TIME, asyncScheduler, { leading: true, trailing: true }),
      download((blob, filename) => {
        this.save(blob, decodeURIComponent(filename));
      })
    );
  }

  /**
   * Does not emit an event
   * @param ids
   */
  likeAnnotations(ids: number[]) {
    const userId = this.accountService.currentUserSignal()?.id;
    if (!userId) return of();

    return this.httpClient.post(this.baseUrl + 'annotation/like', ids);
  }

  /**
   * Does not emit an event
   * @param ids
   */
  unLikeAnnotations(ids: number[]) {
    const userId = this.accountService.currentUserSignal()?.id;
    if (!userId) return of();

    return this.httpClient.post(this.baseUrl + 'annotation/unlike', ids);
  }
}
