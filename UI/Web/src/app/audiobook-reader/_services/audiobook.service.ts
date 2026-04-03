import {inject, Injectable} from '@angular/core';
import {HttpClient} from '@angular/common/http';
import {environment} from '../../../environments/environment';
import {AudioInfo} from '../_models/audio-info';

@Injectable({
  providedIn: 'root'
})
export class AudiobookService {

  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  getAudioInfo(chapterId: number) {
    return this.http.get<AudioInfo>(this.baseUrl + `audio/${chapterId}/audio-info`);
  }

  /**
   * Returns the URL to stream the audio file for the given chapter.
   * The HTML5 audio element will handle range requests for seeking.
   */
  getStreamUrl(chapterId: number, apiKey: string): string {
    return `${this.baseUrl}audio/${chapterId}/stream?apiKey=${encodeURIComponent(apiKey)}`;
  }
}
