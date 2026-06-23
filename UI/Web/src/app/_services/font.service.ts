import {effect, inject, Injectable} from "@angular/core";
import {EpubFont, FontDeleteResult, FontProvider} from "../_models/preferences/epub-font";
import {environment} from 'src/environments/environment';
import {HttpClient} from "@angular/common/http";
import {NgxFileDropEntry} from "ngx-file-drop";
import {AccountService} from "./account.service";

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


  /**
   * The CSS family name a font renders under. User fonts get a namespaced alias so an uploaded font can never
   * clobber a built-in family of the same name (e.g. "Poppins"). Keyed by family so every file in the family shares it.
   */
  resolveCssFamily(font: EpubFont): string {
    return font.provider === FontProvider.User ? `kavita-epub-${font.family}` : font.family;
  }

  /**
   * Builds a FontFace for the given font. Pass {@link familyName} to register the face under a different
   * family name than the font's own (e.g. a namespaced preview alias) so it cannot clobber a global family.
   */
  getFontFace(font: EpubFont, familyName: string = font.family): FontFace {
    if (font.provider === FontProvider.System) {
      return new FontFace(familyName, `url('assets/fonts/${font.family}/${font.fileName}')`, { style: font.style, weight: font.weight });
    }

    return new FontFace(familyName, `url(${this.baseUrl}font?fontId=${font.id}&apiKey=${this.encodedKey})`, { style: font.style, weight: font.weight });
  }

  uploadFont(fontFile: File, fileEntry: NgxFileDropEntry) {
    const formData = new FormData();
    formData.append('formFile', fontFile, fileEntry.relativePath);
    return this.httpClient.post<EpubFont>(this.baseUrl + "font/upload", formData);
  }

  uploadFromUrl(url: string) {
    return this.httpClient.post<EpubFont[]>(this.baseUrl + "font/upload-by-url?url=" + encodeURIComponent(url), {});
  }

  /**
   * Deletes the entire font family the given font belongs to. The backend validates in-use; when the family is
   * still selected by a user it is only removed when {@link force} is set (admin only, enforced server side).
   * @param fontId Any file within the family to delete
   * @param force Delete even when the family is in use
   */
  deleteFontFamily(fontId: number, force: boolean = false) {
    return this.httpClient.delete<FontDeleteResult>(this.baseUrl + `font?fontId=${fontId}&force=${force}`);
  }

}
