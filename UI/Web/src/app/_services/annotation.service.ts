import {inject, Injectable} from '@angular/core';
import {environment} from "../../environments/environment";
import {HttpClient} from "@angular/common/http";
import {Annotation} from '../book-reader/_models/annotation';
import {CreateAnnotationRequest} from "../book-reader/_models/create-annotation-request";
import {TextResonse} from "../_types/text-response";

@Injectable({
  providedIn: 'root'
})
export class AnnotationService {

  private readonly httpClient = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  getAnnotations(chapterId: number) {
    return this.httpClient.get<Array<Annotation>>(this.baseUrl + 'annotation/all?chapterId=' + chapterId);
  }

  createAnnotation(data: CreateAnnotationRequest) {
    return this.httpClient.post<Annotation>(this.baseUrl + 'annotation/create', data);
  }

  delete(id: number) {
    return this.httpClient.delete(this.baseUrl + `annotation?annotationId=${id}`, TextResonse);
  }
}
