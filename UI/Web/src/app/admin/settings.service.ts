import { HttpClient } from '@angular/common/http';
import { computed, Injectable, signal, inject } from '@angular/core';
import {map, of, tap} from 'rxjs';
import { environment } from 'src/environments/environment';
import { TextResonse } from '../_types/text-response';
import { ServerSettings } from './_models/server-settings';
import {MetadataSettings} from "./_models/metadata-settings";
import {MetadataMappingsExport} from "./manage-metadata-mappings/manage-metadata-mappings.component";
import {FieldMappingsImportResult, ImportSettings} from "../_models/import-field-mappings";
import {OidcPublicConfig} from "./_models/oidc-config";

/**
 * Used only for the Test Email Service call
 */
export interface EmailTestResult {
  successful: boolean;
  errorMessage: string;
  emailAddress: string;
}

@Injectable({
  providedIn: 'root'
})
export class SettingsService {
  private http = inject(HttpClient);


  baseUrl = environment.apiUrl;

  getServerSettings() {
    return this.http.get<ServerSettings>(this.baseUrl + 'settings');
  }

  getPublicOidcConfig() {
    return this.http.get<OidcPublicConfig>(this.baseUrl + "settings/oidc");
  }

  getMetadataSettings() {
    return this.http.get<MetadataSettings>(this.baseUrl + 'settings/metadata-settings');
  }
  updateMetadataSettings(model: MetadataSettings) {
    return this.http.post<MetadataSettings>(this.baseUrl + 'settings/metadata-settings', model);
  }

  importFieldMappings(data: MetadataMappingsExport, settings: ImportSettings) {
    const body = {
      data: data,
      settings: settings,
    }
    return this.http.post<FieldMappingsImportResult>(this.baseUrl + 'settings/import-field-mappings', body);
  }

  updateServerSettings(model: ServerSettings) {
    return this.http.post<ServerSettings>(this.baseUrl + 'settings', model);
  }

  resetServerSettings() {
    return this.http.post<ServerSettings>(this.baseUrl + 'settings/reset', {});
  }

  resetIPAddressesSettings() {
    return this.http.post<ServerSettings>(this.baseUrl + 'settings/reset-ip-addresses', {});
  }

  resetBaseUrl() {
    return this.http.post<ServerSettings>(this.baseUrl + 'settings/reset-base-url', {});
  }

  testEmailServerSettings() {
    return this.http.post<EmailTestResult>(this.baseUrl + 'settings/test-email-url', {});
  }

  isEmailSetup() {
    return this.http.get<string>(this.baseUrl + 'server/is-email-setup', TextResonse).pipe(map(d => d == "true"));
  }

  getTaskFrequencies() {
    return this.http.get<string[]>(this.baseUrl + 'settings/task-frequencies');
  }

  getLoggingLevels() {
    return this.http.get<string[]>(this.baseUrl + 'settings/log-levels');
  }

  getLibraryTypes() {
    return this.http.get<string[]>(this.baseUrl + 'settings/library-types');
  }

  getOpdsEnabled() {
    return this.http.get<string>(this.baseUrl + 'settings/opds-enabled', TextResonse).pipe(map(d => d === 'true'));
  }

  isValidCronExpression(val: string) {
    if (val === '' || val === undefined || val === null) return of(false);
    return this.http.get<string>(this.baseUrl + 'settings/is-valid-cron?cronExpression=' + val, TextResonse).pipe(map(d => d === 'true'));
  }

  ifValidAuthority(authority: string) {
    if (authority === '' || authority === undefined || authority === null) return of(false);

    return this.http.post<boolean>(this.baseUrl + 'settings/is-valid-authority', {authority}, TextResonse).pipe(map(r => r + '' == 'true'));
  }
}
