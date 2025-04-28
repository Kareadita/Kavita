import { Injectable } from '@angular/core';
import {UserReview} from "../_single-module/review-card/user-review";
import {environment} from "../../environments/environment";
import {HttpClient} from "@angular/common/http";
import {Rating} from "../_models/rating";

@Injectable({
  providedIn: 'root'
})
export class ReviewService {

  private baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient) { }

  deleteReview(seriesId: number, chapterId?: number) {
    if (chapterId) {
      return this.httpClient.delete(this.baseUrl + `review?chapterId=${chapterId}&seriesId=${seriesId}`);
    }

    return this.httpClient.delete(this.baseUrl + 'review?seriesId=' + seriesId);
  }

  updateReview(seriesId: number, body: string, chapterId?: number) {
    if (chapterId) {
      return this.httpClient.post<UserReview>(this.baseUrl + `review`, {
        seriesId, chapterId, body
      });
    }

    return this.httpClient.post<UserReview>(this.baseUrl + 'review', {
      seriesId, body
    });
  }

  updateRating(seriesId: number, userRating: number, chapterId?: number) {
    return this.httpClient.post(this.baseUrl + 'rating', {
      seriesId, chapterId, userRating
    })
  }

  overallRating(seriesId: number, chapterId?: number) {
    if (chapterId) {
      return this.httpClient.get<Rating>(this.baseUrl + `rating/overall?chapterId=${chapterId}&seriesId=${seriesId}`);
    }
    return this.httpClient.get<Rating>(this.baseUrl + 'rating/overall?seriesId=' + seriesId);
  }

}
