import {inject, Injectable} from '@angular/core';
import {HttpClient} from "@angular/common/http";
import {catchError, map, of, ReplaySubject, tap, throwError} from "rxjs";
import {environment} from "../../environments/environment";
import { TextResonse } from '../_types/text-response';
import {LicenseInfo} from "../_models/kavitaplus/license-info";
import {translate} from "@jsverse/transloco";
import {ConfirmService} from "../shared/confirm.service";

@Injectable({
  providedIn: 'root'
})
export class LicenseService {
  private readonly httpClient = inject(HttpClient);

  baseUrl = environment.apiUrl;

  private readonly hasValidLicenseSource = new ReplaySubject<boolean>(1);
  /**
   * Does the user have an active license
   */
  public readonly hasValidLicense$ = this.hasValidLicenseSource.asObservable();


  /**
   * Delete the license from the server and update hasValidLicenseSource to false
   */
  deleteLicense() {
    return this.httpClient.delete<string>(this.baseUrl + 'license', TextResonse).pipe(
      map(res => res === "true"),
      tap(_ => {
        this.hasValidLicenseSource.next(false)
      }),
      catchError(error => {
        this.hasValidLicenseSource.next(false);
        return throwError(error); // Rethrow the error to propagate it further
      })
    );
  }

  resetLicense(license: string, email: string) {
    return this.httpClient.post<string>(this.baseUrl + 'license/reset', {license, email}, TextResonse);
  }

  /**
   * Returns information about License and will internally cache if license is valid or not
   */
  licenseInfo(forceCheck: boolean = false) {
    return this.httpClient.get<LicenseInfo | null>(this.baseUrl + `license/info?forceCheck=${forceCheck}`).pipe(
      tap(res => {
        this.hasValidLicenseSource.next(res?.isActive || false)
      }),
      catchError(error => {
        this.hasValidLicenseSource.next(false);
        return throwError(error); // Rethrow the error to propagate it further
      })
    );
  }

  hasValidLicense(forceCheck: boolean = false) {
    console.log('hasValidLicense being called: ', forceCheck);
    return this.httpClient.get<string>(this.baseUrl + 'license/valid-license?forceCheck=' + forceCheck, TextResonse)
      .pipe(
        map(res => res === "true"),
        tap(res => {
          this.hasValidLicenseSource.next(res)
        }),
        catchError(error => {
          this.hasValidLicenseSource.next(false);
          return throwError(error); // Rethrow the error to propagate it further
        })
      );
  }

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
