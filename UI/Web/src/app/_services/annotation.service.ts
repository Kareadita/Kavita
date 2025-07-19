import {inject, Injectable, signal} from '@angular/core';
import {environment} from "../../environments/environment";
import {HttpClient} from "@angular/common/http";
import {Annotation} from '../book-reader/_models/annotation';
import {CreateAnnotationRequest} from "../book-reader/_models/create-annotation-request";
import {TextResonse} from "../_types/text-response";
import {map, of, tap} from "rxjs";
import {switchMap} from "rxjs/operators";
import {toObservable} from "@angular/core/rxjs-interop";

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
  private readonly baseUrl = environment.apiUrl;

  private _annotations = signal<Annotation[]>([]);
  /**
   * Annotations for a given book
   */
  public readonly annotations = this._annotations.asReadonly();
  public readonly annotations$ = toObservable(this.annotations);

  private _events = signal<AnnotationEvent | null>(null);
  public readonly events = this._events.asReadonly();
  public readonly events$ = toObservable(this.events);

  getAllAnnotations(chapterId: number) {
    return this.httpClient.get<Array<Annotation>>(this.baseUrl + 'annotation/all?chapterId=' + chapterId).pipe(map(annotations => {
      console.log('Annotations fetched/updated')
      this._annotations.set(annotations);
    }));
  }

  getAnnotationsForPage(chapterId: number, pageNum: number) {
    return this.httpClient.get<Array<Annotation>>(this.baseUrl + `annotation/page?chapterId=${chapterId}&pageNum=${pageNum}`).pipe(map(annotations => {
      console.log('Annotations fetched/updated')
      this._annotations.set(annotations);
    }));
  }

  createAnnotation(data: CreateAnnotationRequest) {
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
      tap(newAnnotation => {
        this._events.set({
          pageNumber: data.pageNumber,
          type: 'edit',
          annotation: data
        });
      }),
      switchMap(newAnnotation => this.getAllAnnotations(data.chapterId))
    );
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
}
