import {Injectable} from '@angular/core';
import {ActivatedRouteSnapshot, Params, Router} from '@angular/router';
import {Pagination} from 'src/app/_models/pagination';
import {SortField, SortOptions} from 'src/app/_models/metadata/series-filter';
import {MetadataService} from "../../_services/metadata.service";
import {SeriesFilterV2} from "../../_models/metadata/v2/series-filter-v2";
import {FilterStatement} from "../../_models/metadata/v2/filter-statement";
import {FilterCombination} from "../../_models/metadata/v2/filter-combination";
import {FilterField} from "../../_models/metadata/v2/filter-field";
import {FilterComparison} from "../../_models/metadata/v2/filter-comparison";

const sortOptionsKey = 'sortOptions=';
const statementsKey = 'stmts=';
const limitToKey = 'limitTo=';
const combinationKey = 'combination=';

@Injectable({
    providedIn: 'root'
})
export class FilterUtilitiesService {

    constructor(private metadataService: MetadataService, private router: Router) {}

    applyFilter(page: Array<any>, filter: FilterField, comparison: FilterComparison, value: string) {
        const dto: SeriesFilterV2 = {
            statements:  [this.metadataService.createDefaultFilterStatement(filter, comparison, value + '')],
            combination: FilterCombination.Or,
            limitTo: 0
        };

        const url = this.urlFromFilterV2(page.join('/') + '?', dto);
        return this.router.navigateByUrl(url);
    }

    applyFilterWithParams(page: Array<any>, filter: SeriesFilterV2, extraParams: Params) {
        let url = this.urlFromFilterV2(page.join('/') + '?', filter);
        url += Object.keys(extraParams).map(k => `&${k}=${extraParams[k]}`).join('');
        return this.router.navigateByUrl(url, extraParams);
    }

    /**
     * Updates the window location with a custom url based on filter and pagination objects
     * @param pagination
     * @param filter
     */
    updateUrlFromFilterV2(pagination: Pagination, filter: SeriesFilterV2 | undefined) {
        const params = '?page=' + pagination.currentPage + '&';

        const url = this.urlFromFilterV2(window.location.href.split('?')[0] + params, filter);
        window.history.replaceState(window.location.href, '', this.replacePaginationOnUrl(url, pagination));
    }


    private replacePaginationOnUrl(url: string, pagination: Pagination) {
        return url.replace(/page=\d+/i, 'page=' + pagination.currentPage);
    }

    /**
     * Will fetch current page from route if present
     * @param snapshot to fetch page from. Must be from component else may get stale data
     * @param itemsPerPage If you want pagination, pass non-zero number
     * @returns A default pagination object
     */
    pagination(snapshot: ActivatedRouteSnapshot, itemsPerPage: number = 0): Pagination {
        return {currentPage: parseInt(snapshot.queryParamMap.get('page') || '1', 10), itemsPerPage, totalItems: 0, totalPages: 1};
    }


    /**
     * Returns the current url with query params for the filter
     * @param currentUrl Full url, with ?page=1 as a minimum
     * @param filter Filter to build url off
     * @returns current url with query params added
     */
    urlFromFilterV2(currentUrl: string, filter: SeriesFilterV2 | undefined) {
        if (filter === undefined) return currentUrl;

        return currentUrl + this.encodeSeriesFilter(filter);
    }

    encodeSeriesFilter(filter: SeriesFilterV2) {
        const encodedStatements = this.encodeFilterStatements(filter.statements);
        const encodedSortOptions = filter.sortOptions ? `${sortOptionsKey}${this.encodeSortOptions(filter.sortOptions)}` : '';
        const encodedLimitTo = `${limitToKey}${filter.limitTo}`;

        return `${this.encodeName(filter.name)}${encodedStatements}&${encodedSortOptions}&${encodedLimitTo}&${combinationKey}${filter.combination}`;
    }

    encodeName(name: string | undefined) {
        if (name === undefined || name === '') return '';
        return `name=${encodeURIComponent(name)}&`
    }


    encodeSortOptions(sortOptions: SortOptions) {
        return `sortField=${sortOptions.sortField},isAscending=${sortOptions.isAscending}`;
    }

    encodeFilterStatements(statements: Array<FilterStatement>) {
        if (statements.length === 0) return '';
        return statementsKey + encodeURIComponent(statements.map(statement => {
            const encodedComparison = `comparison=${statement.comparison}`;
            const encodedField = `field=${statement.field}`;
            const encodedValue = `value=${encodeURIComponent(statement.value)}`;

            return `${encodedComparison}&${encodedField}&${encodedValue}`;
        }).join(','));
    }

  decodeSeriesFilter(encodedFilter: string) {
    const filter = this.metadataService.createDefaultFilterDto();

    if (encodedFilter.includes('name=')) {
      filter.name = decodeURIComponent(encodedFilter).split('name=')[1].split('&')[0];
    }

    const stmtsStartIndex = encodedFilter.indexOf(statementsKey);
    let endIndex = encodedFilter.indexOf('&' + sortOptionsKey);
    if (endIndex < 0) {
      endIndex = encodedFilter.indexOf('&' + limitToKey);
    }

    if (stmtsStartIndex !== -1 || endIndex !== -1) {
      // +1 is for the =
      const stmtsEncoded = encodedFilter.substring(stmtsStartIndex + statementsKey.length, endIndex);
      filter.statements = this.decodeFilterStatements(stmtsEncoded);
    }

    if (encodedFilter.includes(sortOptionsKey)) {
      const optionsStartIndex = encodedFilter.indexOf('&' + sortOptionsKey);
      const endIndex = encodedFilter.indexOf('&' + limitToKey);
      const sortOptionsEncoded = encodedFilter.substring(optionsStartIndex + sortOptionsKey.length + 1, endIndex);
      const sortOptions = this.decodeSortOptions(sortOptionsEncoded);
      if (sortOptions) {
        filter.sortOptions = sortOptions;
      }
    }

    if (encodedFilter.includes(limitToKey)) {
      const limitTo = decodeURIComponent(encodedFilter).split(limitToKey)[1].split('&')[0];
      filter.limitTo = parseInt(limitTo, 10);
    }

    if (encodedFilter.includes(combinationKey)) {
      const combo = decodeURIComponent(encodedFilter).split(combinationKey)[1].split('&')[0];;
      filter.combination = parseInt(combo, 10) as FilterCombination;
    }

    return filter;
  }


    filterPresetsFromUrlV2(snapshot: ActivatedRouteSnapshot): SeriesFilterV2 {
        const filter = this.metadataService.createDefaultFilterDto();
        if (!window.location.href.includes('?')) return filter;

        const queryParams = snapshot.queryParams;

        if (queryParams.name) {
            filter.name = queryParams.name;
        }

        const fullUrl = window.location.href.split('?')[1];
        const stmtsStartIndex = fullUrl.indexOf(statementsKey);
        let endIndex = fullUrl.indexOf('&' + sortOptionsKey);
        if (endIndex < 0) {
            endIndex = fullUrl.indexOf('&' + limitToKey);
        }

        if (stmtsStartIndex !== -1 || endIndex !== -1) {
            // +1 is for the =
            const stmtsEncoded = fullUrl.substring(stmtsStartIndex + statementsKey.length, endIndex);
            filter.statements = this.decodeFilterStatements(stmtsEncoded);
        }

        if (queryParams.sortOptions) {
            const optionsStartIndex = fullUrl.indexOf('&' + sortOptionsKey);
            const endIndex = fullUrl.indexOf('&' + limitToKey);
            const sortOptionsEncoded = fullUrl.substring(optionsStartIndex + sortOptionsKey.length + 1, endIndex);
            const sortOptions = this.decodeSortOptions(sortOptionsEncoded);
            if (sortOptions) {
                filter.sortOptions = sortOptions;
            }
        }

        if (queryParams.limitTo) {
            filter.limitTo = parseInt(queryParams.limitTo, 10);
        }

        if (queryParams.combination) {
            filter.combination = parseInt(queryParams.combination, 10) as FilterCombination;
        }

        return filter;
    }

    decodeSortOptions(encodedSortOptions: string): SortOptions | null {
        const parts = decodeURIComponent(encodedSortOptions).split(',');
        const sortFieldPart = parts.find(part => part.startsWith('sortField='));
        const isAscendingPart = parts.find(part => part.startsWith('isAscending='));

        if (sortFieldPart && isAscendingPart) {
            const sortField = parseInt(sortFieldPart.split('=')[1], 10) as SortField;
            const isAscending = isAscendingPart.split('=')[1].toLowerCase() === 'true';
            return {sortField, isAscending};
        }

        return null;
    }

    decodeFilterStatements(encodedStatements: string): FilterStatement[] {
        const statementStrings = decodeURIComponent(encodedStatements).split(',');
        return statementStrings.map(statementString => {
            const parts = statementString.split('&');
            if (parts === null || parts.length < 3) return null;

            const comparisonStartToken = parts.find(part => part.startsWith('comparison='));
            if (!comparisonStartToken) return null;
            const comparison = parseInt(comparisonStartToken.split('=')[1], 10) as FilterComparison;

            const fieldStartToken = parts.find(part => part.startsWith('field='));
            if (!fieldStartToken) return null;
            const field = parseInt(fieldStartToken.split('=')[1], 10) as FilterField;

            const valueStartToken = parts.find(part => part.startsWith('value='));
            if (!valueStartToken) return null;
            const value = decodeURIComponent(valueStartToken.split('=')[1]);
            return {comparison, field, value};
        }).filter(o => o != null) as FilterStatement[];
    }

    createSeriesV2Filter(): SeriesFilterV2 {
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

    createSeriesV2DefaultStatement(): FilterStatement {
        return {
            comparison: FilterComparison.Equal,
            value: '',
            field: FilterField.SeriesName
        }
    }

}
