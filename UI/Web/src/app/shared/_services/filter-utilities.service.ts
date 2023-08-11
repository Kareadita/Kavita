import {Injectable} from '@angular/core';
import {ActivatedRouteSnapshot, Router} from '@angular/router';
import {Pagination} from 'src/app/_models/pagination';
import {SortField, SortOptions} from 'src/app/_models/metadata/series-filter';
import {MetadataService} from "../../_services/metadata.service";
import {SeriesFilterV2} from "../../_models/metadata/v2/series-filter-v2";
import {FilterStatement} from "../../_models/metadata/v2/filter-statement";
import {FilterCombination} from "../../_models/metadata/v2/filter-combination";
import {FilterField} from "../../_models/metadata/v2/filter-field";
import {FilterComparison} from "../../_models/metadata/v2/filter-comparison";

/**
 * Used to pass state between the filter and the url
 */
export enum FilterQueryParam {
    Format = 'format',
    Genres = 'genres',
    AgeRating = 'ageRating',
    PublicationStatus = 'publicationStatus',
    Tags = 'tags',
    Languages = 'languages',
    CollectionTags = 'collectionTags',
    Libraries = 'libraries',
    Writers = 'writers',
    Artists = 'artists',
    Character = 'character',
    Colorist = 'colorist',
    CoverArtists = 'coverArtists',
    Editor = 'editor',
    Inker = 'inker',
    Letterer = 'letterer',
    Penciller = 'penciller',
    Publisher = 'publisher',
    Translator = 'translators',
    ReadStatus = 'readStatus',
    SortBy = 'sortBy',
    Rating = 'rating',
    Name = 'name',
    /**
     * This is a pagination control
     */
    Page = 'page',
    /**
     * Special case for the UI. Does not trigger filtering
     */
    None = 'none'
}

@Injectable({
    providedIn: 'root'
})
export class FilterUtilitiesService {

    constructor(private metadataService: MetadataService, private router: Router) {
    }

    applyFilter(page: Array<any>, filter: FilterField, comparison: FilterComparison, value: string) {
        const dto: SeriesFilterV2 = {
            statements:  [this.metadataService.createDefaultFilterStatement(filter, comparison, value + '')],
            combination: FilterCombination.Or,
            limitTo: 0
        };

        console.log('applying filter: ', this.urlFromFilterV2(page.join('/') + '?', dto))
        this.router.navigateByUrl(this.urlFromFilterV2(page.join('/') + '?', dto));
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
     * @param ActivatedRouteSnapshot to fetch page from. Must be from component else may get stale data
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
        const encodedSortOptions = filter.sortOptions ? `sortOptions=${this.encodeSortOptions(filter.sortOptions)}` : '';
        const encodedLimitTo = `limitTo=${filter.limitTo}`;

        return `${this.encodeName(filter.name)}stmts=${encodedStatements}&${encodedSortOptions}&${encodedLimitTo}&combination=${filter.combination}`;
    }

    encodeName(name: string | undefined) {
        if (name === undefined || name === '') return '';
        return `name=${encodeURIComponent(name)}&`
    }


    encodeSortOptions(sortOptions: SortOptions) {
        return `sortField=${sortOptions.sortField}&isAscending=${sortOptions.isAscending}`;
    }

    encodeFilterStatements(statements: Array<FilterStatement>) {
        return encodeURIComponent(statements.map(statement => {
            const encodedComparison = `comparison=${statement.comparison}`;
            const encodedField = `field=${statement.field}`;
            const encodedValue = `value=${encodeURIComponent(statement.value)}`;

            return `${encodedComparison}&${encodedField}&${encodedValue}`;
        }).join(','));
    }

    filterPresetsFromUrlV2(snapshot: ActivatedRouteSnapshot): SeriesFilterV2 {
        const filter = this.metadataService.createDefaultFilterDto();
        if (!window.location.href.includes('?')) return filter;

        const queryParams = snapshot.queryParams;

        if (queryParams.name) {
            filter.name = queryParams.name;
        }

        const fullUrl = window.location.href.split('?')[1];
        const stmtsStartIndex = fullUrl.indexOf('stmts=');
        let endIndex = fullUrl.indexOf('&sortOptions=');
        if (endIndex < 0) {
            endIndex = fullUrl.indexOf('&limitTo=');
        }

        if (stmtsStartIndex !== -1 && endIndex !== -1) {
            const stmtsEncoded = fullUrl.substring(stmtsStartIndex + 6, endIndex);
            filter.statements = this.decodeFilterStatements(stmtsEncoded);
        }

        if (queryParams.sortOptions) {
            const sortOptions = this.decodeSortOptions(queryParams.sortOptions);
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
        const parts = encodedSortOptions.split('&');
        const sortFieldPart = parts.find(part => part.startsWith('sortField='));
        const isAscendingPart = parts.find(part => part.startsWith('isAscending='));

        if (sortFieldPart && isAscendingPart) {
            const sortField = parseInt(sortFieldPart.split('=')[1], 10) as SortField;
            const isAscending = isAscendingPart.split('=')[1] === 'true';
            return {sortField, isAscending};
        }

        return null;
    }

    decodeFilterStatements(encodedStatements: string): FilterStatement[] {
        const statementStrings = decodeURIComponent(encodedStatements).split(','); // I don't think this will wrk
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
