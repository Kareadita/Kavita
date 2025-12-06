import {effect, inject, Injectable} from "@angular/core";
import {EpubFont, FontProvider} from "../_models/preferences/epub-font";
import {environment} from 'src/environments/environment';
import {HttpClient} from "@angular/common/http";
import {NgxFileDropEntry} from "ngx-file-drop";
import {AccountService} from "./account.service";
import {TextResonse} from "../_types/text-response";
import {map} from "rxjs/operators";

@Injectable({
  providedIn: 'root'
})
export class FontService {

  public static readonly DefaultEpubFont = 'Default';

  private readonly httpClient = inject(HttpClient);
  private readonly accountService = inject(AccountService);

  baseUrl = environment.apiUrl;
  apiKey: string = '';
  encodedKey: string = '';

  constructor() {
    effect(() => {
      const apiKey = this.accountService.currentUserGenericApiKey();
      if (apiKey) {
        this.apiKey = apiKey;
        this.encodedKey = encodeURIComponent(this.apiKey);
      }
    });
  }

  getFonts() {
    return this.httpClient.get<Array<EpubFont>>(this.baseUrl + 'font/all');
  }

  getFontFace(font: EpubFont): FontFace {
    if (font.provider === FontProvider.System) {
      return new FontFace(font.name, `url('assets/fonts/${font.name}/${font.fileName}')`);
    }

    return new FontFace(font.name, `url(${this.baseUrl}font?fontId=${font.id}&apiKey=${this.encodedKey})`);
  }

  uploadFont(fontFile: File, fileEntry: NgxFileDropEntry) {
    const formData = new FormData();
    formData.append('formFile', fontFile, fileEntry.relativePath);
    return this.httpClient.post<EpubFont>(this.baseUrl + "font/upload", formData);
  }

  uploadFromUrl(url: string) {
    return this.httpClient.post<EpubFont>(this.baseUrl + "font/upload-by-url?url=" + encodeURIComponent(url), {});
  }

  deleteFont(id: number, force: boolean = false) {
    return this.httpClient.delete(this.baseUrl + `font?fontId=${id}&force=${force}`);
  }

  isFontInUse(id: number) {
    return this.httpClient.get(this.baseUrl + `font/in-use?fontId=${id}`, TextResonse).pipe(map(res => res == 'true'));
  }

}
