import {computed, inject, Injectable, signal} from '@angular/core';
import {environment} from "../../environments/environment";
import {HttpClient} from "@angular/common/http";
import {Annotation} from '../book-reader/_models/annotations/annotation';
import {TextResonse} from "../_types/text-response";
import {map, of, tap} from "rxjs";
import {switchMap} from "rxjs/operators";
import {AccountService} from "./account.service";
import {User} from "../_models/user";
import {MessageHubService} from "./message-hub.service";
import {RgbaColor} from "../book-reader/_models/annotations/highlight-slot";
import {Router} from "@angular/router";

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
  private readonly messageHub = inject(MessageHubService);
  private readonly router = inject(Router);
  private readonly baseUrl = environment.apiUrl;

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
        console.log('emitting edit event');
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

  delete(id: number) {
    const filtered = this.annotations().filter(a => a.id === id);
    if (filtered.length === 0) return of();
    const annotationToDelete = filtered[0];

    return this.httpClient.delete(this.baseUrl + `annotation?annotationId=${id}`, TextResonse).pipe(tap(_ => {
      const annotations = this._annotations();
      this._annotations.set(annotations.filter(a => a.id !== id));

      this._events.set({
        pageNumber: annotationToDelete.pageNumber,
        type: 'delete',
        annotation: annotationToDelete
      });
    }));
  }

  /**
   * Routes to the book reader with the annotation in view
   * @param item
   */
  navigateToAnnotation(item: Annotation) {
    this.router.navigate(['/library', item.libraryId, 'series', item.seriesId, 'book', item.chapterId], { queryParams: { annotation: item.id } });
  }
}
