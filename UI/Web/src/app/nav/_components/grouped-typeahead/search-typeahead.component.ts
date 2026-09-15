import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  HostListener,
  inject,
  input,
  output,
  signal,
  viewChild
} from '@angular/core';
import {takeUntilDestroyed, toObservable} from "@angular/core/rxjs-interop";
import {NgTemplateOutlet} from '@angular/common';
import {TranslocoDirective} from "@jsverse/transloco";
import {KeyBindEvent, KeyBindService} from "../../../_services/key-bind.service";
import {KeyBindTarget} from "../../../_models/preferences/preferences";
import {KeyBindPipe} from "../../../_pipes/key-bind.pipe";
import {SearchResultGroup} from "../../../_models/search/search-result-group";
import {form, FormField, FormRoot} from "@angular/forms/signals";
import {catchError, debounceTime, distinctUntilChanged, of, switchMap} from "rxjs";
import {SearchService} from "../../../_services/search.service";
import {CollectionOwnerComponent} from "../../../collections/_components/collection-owner/collection-owner.component";
import {ImageComponent} from "../../../shared/image/image.component";
import {PromotedIconComponent} from "../../../shared/_components/promoted-icon/promoted-icon.component";
import {QuillViewComponent} from "ngx-quill";
import {SeriesFormatComponent} from "../../../shared/series-format/series-format.component";
import {SearchResult} from "../../../_models/search/search-result";
import {Annotation} from "../../../book-reader/_models/annotations/annotation";
import {BookmarkSearchResult} from "../../../_models/search/bookmark-search-result";
import {MangaFile} from "../../../_models/manga-file";
import {Chapter} from "../../../_models/chapter";
import {Library} from "../../../_models/library/library";
import {UserCollection} from "../../../_models/collection-tag";
import {ReadingList} from "../../../_models/reading-list/reading-list";
import {FilterStatement} from "../../../_models/metadata/v2/filter-statement";
import {SeriesFilterField} from "../../../_models/metadata/v2/series-filter-field";
import {FilterComparison} from "../../../_models/metadata/v2/filter-comparison";
import {Person} from "../../../_models/metadata/person";
import {Router} from "@angular/router";
import {AnnotationService} from "../../../_services/annotation.service";
import {MetadataService} from "../../../_services/metadata.service";
import {FilterUtilitiesService} from "../../../shared/_services/filter-utilities.service";
import {ImageService} from "../../../_services/image.service";
import {generateUniqueId} from "../../../_helpers/random";
import {EmptyStateComponent} from "../../../shared/_components/empty-state/empty-state.component";

export interface SearchEvent {
  value: string;
  includeFiles: boolean;
}

interface FormModel {
  typeahead: string;
  includeExtras: boolean;
}

/**
 * The global search component
 */
@Component({
  selector: 'app-search-typeahead',
  templateUrl: './search-typeahead.component.html',
  styleUrls: ['./search-typeahead.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgTemplateOutlet, TranslocoDirective, KeyBindPipe, FormRoot, FormField, CollectionOwnerComponent, ImageComponent, PromotedIconComponent, QuillViewComponent, SeriesFormatComponent, EmptyStateComponent]
})
export class SearchTypeaheadComponent {

  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);
  private readonly searchService = inject(SearchService);
  private readonly annotationService = inject(AnnotationService);
  private readonly metadataService = inject(MetadataService);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  protected readonly keyBindService = inject(KeyBindService);
  protected readonly imageService = inject(ImageService);

  /**
   * Minimum number of characters in input to trigger a search
   */
  minQueryLength = input<number>(1);

  /**
   * Number of milliseconds after typing before triggering a search
   */
  private readonly debounceTime: number = 200;
  /**
   * Emits when the search that is about to run changes
   */
  readonly inputChanged = output<SearchEvent>();
  /**
   * Emits an event when the field is cleared
   */
  readonly clearField = output<void>();
  /**
   * Emits when a change in the search field looses/gains focus
   */
  readonly focusChanged = output<boolean>();

  readonly inputElem = viewChild.required<ElementRef<HTMLInputElement>>('input');

  groupedData = signal<SearchResultGroup>(new SearchResultGroup());
  hasFocus = signal(false);
  isLoading = signal<boolean>(false);
  id = signal<string>(generateUniqueId());

  private readonly formModel = signal<FormModel>({
    typeahead: '',
    includeExtras: false
  });
  formGroup = form(this.formModel);

  searchTerm = computed(() => {
    return this.formModel().typeahead ?? '';
  });

  /**
   * Everything a search depends on. Toggling the extras switch re-runs the search just like typing does.
   */
  private readonly searchQuery = computed(() => ({
    term: this.searchTerm().trim(),
    includeExtras: this.formModel().includeExtras
  }));

  hasAnyData = computed(() => {
    const data = this.groupedData();

    return data.series.length > 0 || data.collections.length > 0 || data.readingLists.length > 0
      || data.bookmarks.length > 0 || data.libraries.length > 0 || data.genres.length > 0
      || data.tags.length > 0 || data.persons.length > 0 || data.chapters.length > 0
      || data.files.length > 0 || data.annotations.length > 0;
  });


  @HostListener('window:click')
  handleDocumentClick() {
    this.close();
  }

  private focusElement(e: KeyBindEvent) {
    e.triggered = true;
    this.inputElem().nativeElement.focus();
  }


  constructor() {
    toObservable(this.searchQuery).pipe(
      debounceTime(this.debounceTime),
      distinctUntilChanged((a, b) => a.term === b.term && a.includeExtras === b.includeExtras),
      switchMap(query => {
        const canSearch = query.term.length >= this.minQueryLength();
        this.isLoading.set(canSearch);

        if (!canSearch) return of(new SearchResultGroup());

        this.inputChanged.emit({value: query.term, includeFiles: query.includeExtras});

        // Swallow failures here so a single bad request doesn't tear down the stream for the session
        return this.searchService.search(query.term, query.includeExtras)
          .pipe(catchError(() => of(new SearchResultGroup())));
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(results => {
      this.groupedData.set(results);
      this.isLoading.set(false);
    });

    this.keyBindService.registerListener(
      this.destroyRef,
      (e) => this.focusElement(e),
      [KeyBindTarget.OpenSearch],
      {fireInEditable: true},
    );

    this.keyBindService.registerListener(
      this.destroyRef,
      (e) => {
        if (this.hasFocus()) {
          this.close();
          e.triggered = true;
        }
      },
      [KeyBindTarget.Escape],
      {markAsTriggered: false, fireInEditable: true},
    );
  }

  /**
   * Clicks within the input area must not reach the window listener, which closes the dropdown
   */
  onInputClick(event: MouseEvent) {
    event.stopPropagation();
    this.inputElem().nativeElement.focus();
  }

  close(event?: FocusEvent) {
    // If the user is tabbing out of the input field, check if there are results first before closing
    if (event && (this.hasAnyData() || this.searchTerm())) return;

    this.hasFocus.set(false);
    this.focusChanged.emit(false);
    this.inputElem().nativeElement.blur();
  }

  open() {
    this.hasFocus.set(true);
    this.focusChanged.emit(true);
  }

  public clearSearch() {
    this.formGroup.typeahead().value.set('');
    this.clearField.emit();
  }



  clickSeriesSearchResult(item: SearchResult) {
    this.clearSearch();
    const libraryId = item.libraryId;
    const seriesId = item.seriesId;
    this.router.navigate(['library', libraryId, 'series', seriesId]);
  }

  clickAnnotationSearchResult(item: Annotation) {
    this.clearSearch();
    this.annotationService.navigateToAnnotation(item);
  }

  clickBookmarkSearchResult(item: BookmarkSearchResult) {
    this.clearSearch();
    const libraryId = item.libraryId;
    const seriesId = item.seriesId;
    this.router.navigate(['library', libraryId, 'series', seriesId, 'manga', item.chapterId], {queryParams: {
        incognitoMode: false, bookmarkMode: true
      }});
  }

  clickFileSearchResult(item: MangaFile) {
    this.clearSearch();
    this.searchService.getSeriesForMangaFile(item.id).subscribe(series => {
      if (series !== undefined && series !== null) {
        this.router.navigate(['library', series.libraryId, 'series', series.id]);
      }
    });
  }

  clickChapterSearchResult(item: Chapter) {
    this.clearSearch();
    this.searchService.getSeriesForChapter(item.id).subscribe(series => {
      if (series !== undefined && series !== null) {
        this.router.navigate(['library', series.libraryId, 'series', series.id]);
      }
    });
  }

  clickLibraryResult(item: Library) {
    this.clearSearch();
    this.router.navigate(['library', item.id]);
  }

  clickCollectionSearchResult(item: UserCollection) {
    this.clearSearch();
    this.router.navigate(['collections', item.id]);
  }

  clickReadingListSearchResult(item: ReadingList) {
    this.clearSearch();
    this.router.navigate(['lists', item.id]);
  }

  goTo(statement: FilterStatement<number>) {
    let params: any = {};
    const filter = this.metadataService.createDefaultFilterDto('series');
    filter.statements = [statement];
    params['page'] = 1;
    this.clearSearch();
    this.filterUtilityService.applyFilterWithParams(['all-series'], filter, params).subscribe();
  }

  goToOther(field: SeriesFilterField, value: string) {
    this.goTo({field, comparison: FilterComparison.Equal, value: value + ''});
  }

  goToPerson(person: Person) {
    this.clearSearch();
    this.router.navigate(['person', person.name]);
  }

  protected readonly FilterField = SeriesFilterField;
}
