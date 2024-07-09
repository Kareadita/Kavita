import {DestroyRef, inject, Injectable} from "@angular/core";
import {map, ReplaySubject} from "rxjs";
import {EpubFont} from "../_models/preferences/epub-font";
import {environment} from 'src/environments/environment';
import {HttpClient} from "@angular/common/http";
import {EVENTS, MessageHubService} from "./message-hub.service";
import {NgxFileDropEntry} from "ngx-file-drop";
import {AccountService} from "./account.service";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {NotificationProgressEvent} from "../_models/events/notification-progress-event";

@Injectable({
  providedIn: 'root'
})
export class FontService {
  private readonly destroyRef = inject(DestroyRef);
  public defaultEpubFont: string = 'default';

  private fontsSource = new ReplaySubject<EpubFont[]>(1);
  public fonts$ = this.fontsSource.asObservable();

  baseUrl: string = environment.apiUrl;
  apiKey: string = '';
  encodedKey: string = '';

  constructor(private httpClient: HttpClient, messageHub: MessageHubService, private accountService: AccountService) {
    this.accountService.currentUser$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(user => {
      if (user) {
        this.apiKey = user.apiKey;
        this.encodedKey = encodeURIComponent(this.apiKey);
      }
    });
  }

  getFonts() {
    return this.httpClient.get<Array<EpubFont>>(this.baseUrl + 'font/all').pipe(map(fonts => {
      this.fontsSource.next(fonts);

      return fonts;
    }));
  }

  getFontFace(font: EpubFont): FontFace {
    return new FontFace(font.name, `url(${this.baseUrl}font?fontId=${font.id}&apiKey=${this.encodedKey})`);
  }

  uploadFont(fontFile: File, fileEntry: NgxFileDropEntry) {
    const formData = new FormData();
    formData.append('formFile', fontFile, fileEntry.relativePath);
    return this.httpClient.post<EpubFont>(this.baseUrl + "font/upload", formData);
  }

  uploadFromUrl(url: string) {

  }

  deleteFont(id: number) {
    return this.httpClient.delete(this.baseUrl + `font?fontId=${id}`);
  }

}
