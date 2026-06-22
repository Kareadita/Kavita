import {inject, Injectable} from '@angular/core';
import {HttpClient, HttpParams} from '@angular/common/http';
import {Observable} from 'rxjs';
import {map} from 'rxjs/operators';
import {environment} from '../../environments/environment';
import {UtilityService} from '../shared/_services/utility.service';
import {KavitaPlusAuditEntry} from '../_models/kavitaplus/kavita-plus-audit-entry';
import {KavitaPlusAuditFilter} from '../_models/kavitaplus/kavita-plus-audit-filter';
import {KavitaPlusAuditStats} from '../_models/kavitaplus/kavita-plus-audit-stats';
import {KavitaPlusAuditSeriesInfo} from '../_models/kavitaplus/kavita-plus-audit-series-info';
import {PaginatedResult} from '../_models/pagination';

@Injectable({
  providedIn: 'root'
})
export class KavitaPlusAuditService {
  private readonly httpClient = inject(HttpClient);
  private readonly utilityService = inject(UtilityService);
  private readonly baseUrl = environment.apiUrl + 'kavita-plus-audit/';

  getSeriesInfo(seriesId: number): Observable<KavitaPlusAuditSeriesInfo> {
    return this.httpClient.get<KavitaPlusAuditSeriesInfo>(
      `${this.baseUrl}entries/series/${seriesId}`
    );
  }

  getEntries(filter: KavitaPlusAuditFilter, pageNum?: number, itemsPerPage?: number): Observable<PaginatedResult<KavitaPlusAuditEntry[]>> {
    const params = this.utilityService.addPaginationIfExists(new HttpParams(), pageNum, itemsPerPage);
    return this.httpClient.post<Array<KavitaPlusAuditEntry>>(
      `${this.baseUrl}entries`, filter, { observe: 'response', params }
    ).pipe(map(res => this.utilityService.createPaginatedResult<KavitaPlusAuditEntry>(res)));
  }

  getStats(): Observable<KavitaPlusAuditStats> {
    return this.httpClient.get<KavitaPlusAuditStats>(`${this.baseUrl}stats`);
  }

  getMyActivity(filter: KavitaPlusAuditFilter, pageNum?: number, itemsPerPage?: number): Observable<PaginatedResult<KavitaPlusAuditEntry[]>> {
    const params = this.utilityService.addPaginationIfExists(new HttpParams(), pageNum, itemsPerPage);
    return this.httpClient.post<Array<KavitaPlusAuditEntry>>(
      `${this.baseUrl}my-activity`, filter, { observe: 'response', params }
    ).pipe(map(res => this.utilityService.createPaginatedResult<KavitaPlusAuditEntry>(res)));
  }
}
