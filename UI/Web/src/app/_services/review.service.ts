import {inject, Injectable, Signal} from '@angular/core';
import {UserReview, UserReviewExtended} from "../_models/user-review";
import {environment} from "../../environments/environment";
import {HttpClient, HttpParams, httpResource} from "@angular/common/http";
import {Rating} from "../_models/rating";

@Injectable({
  providedIn: 'root'
})
export class ReviewService {
  private httpClient = inject(HttpClient);


  private baseUrl = environment.apiUrl;

  deleteReview(seriesId: number, chapterId?: number) {
    if (chapterId) {
      return this.httpClient.delete(this.baseUrl + `review/chapter?chapterId=${chapterId}`);
    }

    return this.httpClient.delete(this.baseUrl + `review/series?seriesId=${seriesId}`);
  }

  updateReview(seriesId: number, body: string, chapterId?: number) {
    if (chapterId) {
      return this.httpClient.post<UserReview>(this.baseUrl + `review/chapter`, {
        seriesId, chapterId, body
      });
    }

    return this.httpClient.post<UserReview>(this.baseUrl + 'review/series', {
      seriesId, body
    });
  }

  updateRating(seriesId: number, userRating: number, chapterId?: number) {
    if (chapterId) {
      return this.httpClient.post(this.baseUrl + 'rating/chapter', {
        seriesId, chapterId, userRating
      })
    }

    return this.httpClient.post(this.baseUrl + 'rating/series', {
      seriesId, userRating
    })
  }

  overallRating(seriesId: number, chapterId?: number) {
    if (chapterId) {
      return this.httpClient.get<Rating>(this.baseUrl + `rating/overall-chapter?chapterId=${chapterId}`);
    }

    return this.httpClient.get<Rating>(this.baseUrl + `rating/overall-series?seriesId=${seriesId}`);
  }

  getReviewsByUserResource(userId: () => number, filterQuery: () => string | null, rating: () => number | null) {
    const params = new HttpParams();
    params.set('userId', userId())
    if (filterQuery()) {
      params.set('filterQuery', filterQuery()!);
    }
    if (rating()) {
      params.set('rating', rating()!);
    }

    return httpResource<UserReviewExtended[]>(() => this.baseUrl + `review/all?${params.toString()}`).asReadonly();
  }

  getReviewsByUser(userId: number, filterQuery: string | null, rating: number | null) {
    const paramObject: Record<string, string> = {
      userId: userId.toString()
    };

    if (filterQuery) {
      paramObject.filterQuery = filterQuery;
    }
    if (rating !== null) {
      paramObject.rating = rating.toString();
    }

    const params = new HttpParams({ fromObject: paramObject });

    return this.httpClient.get<UserReviewExtended[]>(this.baseUrl + 'review/all', { params });
  }

}
