import {inject, Injectable, signal} from '@angular/core';
import {HttpClient} from "@angular/common/http";
import {catchError, map, tap, throwError} from "rxjs";
import {environment} from "../../environments/environment";
import {TextResonse} from '../_types/text-response';
import {LicenseInfo} from "../_models/kavitaplus/license-info";

@Injectable({
  providedIn: 'root'
})
export class LicenseService {
  private readonly httpClient = inject(HttpClient);

  private readonly baseUrl = environment.apiUrl;

  private readonly _hasValidLicense = signal<boolean>(false);
  /** Does the user have an active license */
  public readonly hasValidLicense = this._hasValidLicense.asReadonly();


  /**
   * Delete the license from the server and update hasValidLicenseSource to false
   */
  deleteLicense() {
    return this.httpClient.delete<string>(this.baseUrl + 'license', TextResonse).pipe(
      map(res => res === "true"),
      tap(_ => {
        this._hasValidLicense.set(false);
      }),
      catchError(error => {
        this._hasValidLicense.set(false);
        return throwError(error); // Rethrow the error to propagate it further
      })
    );
  }

  /** Break the registration between Kavita+ and this instance */
  resetLicense(license: string, email: string) {
    return this.httpClient.post<string>(this.baseUrl + 'license/reset', {license, email}, TextResonse);
  }

  resendLicense() {
    return this.httpClient.post<boolean>(this.baseUrl + 'license/resend-license', {}, TextResonse).pipe(map(res => (res + '') === "true"));
  }

  /**
   * Returns information about License and will internally cache if license is valid or not
   */
  licenseInfo(forceCheck: boolean = false) {
    return this.httpClient.get<LicenseInfo | null>(this.baseUrl + `license/info?forceCheck=${forceCheck}`).pipe(
      tap(res => {
        this._hasValidLicense.set(res?.isActive || false);
      }),
      catchError(error => {
        console.error(error);
        this._hasValidLicense.set(false);
        return throwError(error); // Rethrow the error to propagate it further
      })
    );
  }

  /** Checks with the Server if the user has an active license. Force check will passthru to K+ */
  checkForValidLicense(forceCheck: boolean = false) {
    return this.httpClient.get<string>(this.baseUrl + 'license/valid-license?forceCheck=' + forceCheck, TextResonse)
      .pipe(
        map(res => res === "true"),
        tap(value => {
          this._hasValidLicense.set(value);
        }),
        catchError(error => {
          this._hasValidLicense.set(false);
          return throwError(error); // Rethrow the error to propagate it further
        })
      );
  }

  /** Has any license registered with the instance. Does not validate against Kavita+ API */
  hasAnyLicense() {
    return this.httpClient.get<string>(this.baseUrl + 'license/has-license', TextResonse)
      .pipe(
        map(res => res === "true"),
      );
  }

  updateUserLicense(license: string, email: string, discordId?: string) {
    return this.httpClient.post<string>(this.baseUrl + 'license', {license, email, discordId}, TextResonse)
      .pipe(map(res => res === "true"));
  }
}
