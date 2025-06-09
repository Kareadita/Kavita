import {inject, Injectable} from '@angular/core';
import {ActivatedRouteSnapshot, Params, Router} from '@angular/router';
import {SortField} from 'src/app/_models/metadata/series-filter';
import {MetadataService} from "../../_services/metadata.service";
import {FilterV2} from "../../_models/metadata/v2/filter-v2";
import {FilterStatement} from "../../_models/metadata/v2/filter-statement";
import {FilterCombination} from "../../_models/metadata/v2/filter-combination";
import {FilterField} from "../../_models/metadata/v2/filter-field";
import {FilterComparison} from "../../_models/metadata/v2/filter-comparison";
import {HttpClient} from "@angular/common/http";
import {TextResonse} from "../../_types/text-response";
import {environment} from "../../../environments/environment";
import {map, tap} from "rxjs/operators";
import {Observable, of, switchMap} from "rxjs";
import {PersonFilterField} from "../../_models/metadata/v2/person-filter-field";


@Injectable({
    providedIn: 'root'
})
export class FilterUtilitiesService {

  private readonly router = inject(Router);
  private readonly metadataService = inject(MetadataService);
  private readonly http = inject(HttpClient);

  private readonly apiUrl = environment.apiUrl;

  encodeFilter(filter: FilterV2<number> | undefined) {
    return this.http.post<string>(this.apiUrl + 'filter/encode', filter, TextResonse);
  }

  decodeFilter(encodedFilter: string) {
    return this.http.post<FilterV2<number>>(this.apiUrl + 'filter/decode', {encodedFilter}).pipe(map(filter => {
      if (filter == null) {
        filter = this.metadataService.createDefaultFilterDto();
        filter.statements.push(this.createSeriesV2DefaultStatement());
      }

      return filter;
    }))
  }

  updateUrlFromFilter(filter: FilterV2<number> | undefined) {
    return this.encodeFilter(filter).pipe(tap(encodedFilter => {
      window.history.replaceState(window.location.href, '', window.location.href.split('?')[0]+ '?' + encodedFilter);
    }));
  }

  filterPresetsFromUrl(snapshot: ActivatedRouteSnapshot): Observable<FilterV2<number>> {
    const filter = this.metadataService.createDefaultFilterDto();
    filter.statements.push(this.createSeriesV2DefaultStatement());
    if (!window.location.href.includes('?')) return of(filter);

    return this.decodeFilter(window.location.href.split('?')[1]);
  }

  /**
   * Applies and redirects to the passed page with the filter encoded
   * @param page
   * @param filter
   * @param comparison
   * @param value
   */
  applyFilter(page: Array<any>, filter: FilterField, comparison: FilterComparison, value: string) {
    const dto = this.createSeriesV2Filter();
    dto.statements.push(this.metadataService.createDefaultFilterStatement(filter, comparison, value + ''));

    return this.encodeFilter(dto).pipe(switchMap(encodedFilter => {
      return this.router.navigateByUrl(page.join('/') + '?' + encodedFilter);
    }));
  }

  applyFilterWithParams(page: Array<any>, filter: FilterV2<any>, extraParams: Params) {
    return this.encodeFilter(filter).pipe(switchMap(encodedFilter => {
      let url = page.join('/') + '?' + encodedFilter;
      url += Object.keys(extraParams).map(k => `&${k}=${extraParams[k]}`).join('');

      return this.router.navigateByUrl(url, extraParams);
    }));
  }

  createSeriesV2Filter(): FilterV2<FilterField> {
      return {
          combination: FilterCombination.And,
          statements: [],
          limitTo: 0,
          sortOptions: {
              isAscending: true,
              sortField: SortField.SortName
          },
      };
  }

  createSeriesV2DefaultStatement(): FilterStatement<FilterField> {
      return {
          comparison: FilterComparison.Equal,
          value: '',
          field: FilterField.SeriesName
      }
  }

  createPersonV2Filter(): FilterV2<PersonFilterField> {
    return {
      combination: FilterCombination.And,
      statements: [],
      limitTo: 0,
      sortOptions: {
        isAscending: true,
        sortField: SortField.SortName
      },
    };
  }

  createPersonV2DefaultStatement(): FilterStatement<PersonFilterField> {
    return {
      comparison: FilterComparison.Equal,
      value: '',
      field: PersonFilterField.Name
    }
  }
}
