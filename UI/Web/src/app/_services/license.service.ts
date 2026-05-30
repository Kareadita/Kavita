import {inject, Injectable, signal} from '@angular/core';
import {HttpClient} from "@angular/common/http";
import {catchError, map, tap, throwError} from "rxjs";
import {environment} from "../../environments/environment";
import {TextResonse} from '../_types/text-response';
import {LicenseInfo} from "../_models/kavitaplus/license-info";
import {KavitaPlusRegisterResult} from "../_models/kavitaplus/registration/kavita-plus-register-result";
import {KavitaPlusProviderHealthSnapshot} from '../_models/kavitaplus/kavita-plus-provider-health';
import {ScrobbleProvider} from "./scrobbling.service";
import {KavitaPlusLicenseUsage} from "../_models/kavitaplus/kavita-plus-license-usage";

@Injectable({
  providedIn: 'root'
})
export class LicenseService {
  private readonly httpClient = inject(HttpClient);

  private readonly baseUrl = environment.apiUrl;

  private readonly _hasActiveLicense = signal<boolean>(false);
  /** Does the server have an active license */
  public readonly hasActiveLicense = this._hasActiveLicense.asReadonly();

  private readonly _hasLicenseOnFile = signal<boolean>(false);
  /** Does the server have a license stored - doesn't indicate being active */
  public readonly hasLicenseOnFile = this._hasLicenseOnFile.asReadonly();

  private readonly _licenseInfo = signal<LicenseInfo | null>(null);
  /** Cached license info - Should always be accessed this way */
  public readonly licenseInfo = this._licenseInfo.asReadonly();


  /**
   * Delete the license from the server and update hasValidLicenseSource to false
   */
  deleteLicense() {
    return this.httpClient.delete<string>(this.baseUrl + 'license', TextResonse).pipe(
      map(res => res === "true"),
      tap(_ => {
        this._hasActiveLicense.set(false);
        this._licenseInfo.set(null);
        this._hasLicenseOnFile.set(false);
      }),
      catchError(error => {
        this._hasActiveLicense.set(false);
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
  getLicenseInfo(forceCheck: boolean = false) {
    return this.httpClient.get<LicenseInfo | null>(this.baseUrl + `license/info?forceCheck=${forceCheck}`).pipe(
      tap(res => {
        this._hasActiveLicense.set(res?.isActive || false);
        this._licenseInfo.set(res ? LicenseInfo.from(res) : null);
        this._hasLicenseOnFile.set(res?.hasLicense ?? false);
      }),
      catchError(error => {
        console.error(error);
        this._hasActiveLicense.set(false);
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
          this._hasActiveLicense.set(value);
          if (forceCheck) this.getLicenseInfo(true).subscribe();
        }),
        catchError(error => {
          this._hasActiveLicense.set(false);
          return throwError(error); // Rethrow the error to propagate it further
        })
      );
  }

  /** Has any license registered with the instance. Does not validate against Kavita+ API */
  hasAnyLicense() {
    return this.httpClient.get<string>(this.baseUrl + 'license/has-license', TextResonse)
      .pipe(
        map(res => res === "true"),
        tap(value => {
          this._hasLicenseOnFile.set(value);
        }),
        catchError(error => {
          this._hasLicenseOnFile.set(false);
          return throwError(error); // Rethrow the error to propagate it further
        })
      );
  }


  registerLicense(license: string, email: string, discordId?: string) {
    return this.updateUserLicense(license, email, discordId);
  }

  updateUserLicense(license: string, email: string, discordId?: string) {
    return this.httpClient.post<KavitaPlusRegisterResult>(this.baseUrl + 'license', {license, email, discordId}).pipe(
      tap(result => {
        if (result.success) this.getLicenseInfo(true).subscribe();
      })
    );
  }

  getProviderHealthSnapshot(forceCheck = false) {
    return this.httpClient.get<KavitaPlusProviderHealthSnapshot[]>(this.baseUrl + `license/provider-health?forceCheck=${forceCheck}`).pipe(
      map(res => res.filter(s => s.provider !== (3 as ScrobbleProvider)))); // Take out GoogleBooks, it's being repaced by Hardcover
  }

  getLicenseUsage() {
    return this.httpClient.get<KavitaPlusLicenseUsage>(this.baseUrl + `license/stats`);
  }
}
