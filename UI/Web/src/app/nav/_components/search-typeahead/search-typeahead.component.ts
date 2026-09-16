import {
  afterRenderEffect,
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
  viewChild,
  viewChildren
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
import {Genre} from "../../../_models/metadata/genre";
import {Tag} from "../../../_models/tag";
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
import {KEY_CODES} from "../../../shared/_services/utility.service";
import {EmptyStateComponent} from "../../../shared/_components/empty-state/empty-state.component";
import {TranslocoInjectComponent} from "../../../shared/_components/transloco-inject/transloco-inject.component";
import {TranslocoSlotDirective} from "../../../_directives/transloco-slot.directive";
import {NgbPopover} from "@ng-bootstrap/ng-bootstrap";
import {SafeHtmlPipe} from "../../../_pipes/safe-html.pipe";

export interface SearchEvent {
  value: string;
  includeFiles: boolean;
}

interface FormModel {
  typeahead: string;
  includeExtras: boolean;
}

/**
 * Every result group, in the order the template renders them. Both the flattened row list and the
 * per-group index offsets derive from this, so the two can never drift out of sync with each other.
 */
const GROUP_ORDER = [
  'series', 'collections', 'readingLists', 'bookmarks', 'libraries',
  'genres', 'tags', 'persons', 'chapters', 'files', 'annotations'
] as const;

type GroupKey = typeof GROUP_ORDER[number];
type SearchRowKind = GroupKey | 'searchAll';

interface SearchRow {
  kind: SearchRowKind;
  item?: unknown;
}

/**
 * The global search component
 */
@Component({
  selector: 'app-search-typeahead',
  templateUrl: './search-typeahead.component.html',
  styleUrls: ['./search-typeahead.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgTemplateOutlet, TranslocoDirective, KeyBindPipe, FormRoot, FormField, CollectionOwnerComponent, ImageComponent, PromotedIconComponent, QuillViewComponent, SeriesFormatComponent, EmptyStateComponent, TranslocoInjectComponent, TranslocoSlotDirective, NgbPopover, SafeHtmlPipe]
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
  private readonly hostElem = inject(ElementRef<HTMLElement>);

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

  private readonly inputElem = viewChild.required<ElementRef<HTMLInputElement>>('input');
  private readonly optionRows = viewChildren<ElementRef<HTMLElement>>('optionRow');
  private readonly configPopover = viewChild<NgbPopover>('configPopover');
  private readonly configBtn = viewChild<ElementRef<HTMLButtonElement>>('configBtn');

  groupedData = signal<SearchResultGroup>(new SearchResultGroup());
  hasFocus = signal(false);
  isLoading = signal<boolean>(false);
  id = signal<string>(generateUniqueId());
  /**
   * Index into flatRows() of the virtually focused option. Real focus never leaves the input.
   */
  focusedIndex = signal<number>(0);

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

  /**
   * The "search everything" row only exists while there is a term, so it must gate the row indexes too
   */
  readonly hasSearchAllRow = computed(() => this.searchTerm().trim().length > 0);

  /**
   * Every rendered option as one flat list, so the keyboard has a single index space to walk
   */
  private readonly flatRows = computed<SearchRow[]>(() => {
    const data = this.groupedData();
    const rows: SearchRow[] = [];

    if (this.hasSearchAllRow()) {
      rows.push({kind: 'searchAll'});
    }

    for (const key of GROUP_ORDER) {
      for (const item of data[key]) {
        rows.push({kind: key, item});
      }
    }

    return rows;
  });

  readonly rowCount = computed(() => this.flatRows().length);

  /**
   * Announced count, excluding the synthetic "search everything" row which isn't a result
   */
  readonly resultCount = computed(() => this.rowCount() - (this.hasSearchAllRow() ? 1 : 0));

  /**
   * First flat index of each group, so a template @for can map its local $index onto the flat one
   */
  readonly offsets = computed<Record<GroupKey, number>>(() => {
    const data = this.groupedData();
    const result = {} as Record<GroupKey, number>;
    let index = this.hasSearchAllRow() ? 1 : 0;

    for (const key of GROUP_ORDER) {
      result[key] = index;
      index += data[key].length;
    }

    return result;
  });

  readonly listboxId = computed(() => `${this.id()}-listbox`);
  readonly extrasId = computed(() => `${this.id()}-include-extras`);

  readonly activeRowId = computed(() => {
    if (!this.hasFocus()) return null;

    const index = this.focusedIndex();
    return index >= 0 && index < this.rowCount() ? this.rowId(index) : null;
  });

  rowId(index: number) {
    return `${this.id()}-row-${index}`;
  }

  groupId(key: GroupKey) {
    return `${this.id()}-${key}-group`;
  }


  @HostListener('window:click', ['$event'])
  handleDocumentClick(event: MouseEvent) {
    // The config popover renders into the body, so a click inside it is not a click "outside" the search
    if ((event.target as HTMLElement | null)?.closest('.search-config-popover')) return;

    this.close();
  }

  /**
   * Arrow keys walk the flattened option list while the caret stays in the input. Home/End are
   * deliberately left to the textbox for caret movement, per the editable-combobox pattern.
   */
  @HostListener('window:keydown', ['$event'])
  handleKeyPress(event: KeyboardEvent) {
    if (!this.hasFocus()) return;

    const count = this.rowCount();
    if (count === 0) return;

    switch (event.key) {
      case KEY_CODES.DOWN_ARROW:
        event.preventDefault();
        this.focusedIndex.set(Math.min(this.focusedIndex() + 1, count - 1));
        break;
      case KEY_CODES.UP_ARROW:
        event.preventDefault();
        this.focusedIndex.set(Math.max(this.focusedIndex() - 1, 0));
        break;
      case KEY_CODES.ENTER:
        event.preventDefault();
        this.activateRow(this.focusedIndex());
        break;
    }
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
      this.focusedIndex.set(0);
    });

    afterRenderEffect(() => {
      if (!this.hasFocus()) return;

      this.optionRows()[this.focusedIndex()]?.nativeElement.scrollIntoView({block: 'nearest'});
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
        const popover = this.configPopover();
        if (popover?.isOpen()) {
          popover.close();
          this.configBtn()?.nativeElement.focus();
          e.triggered = true;
          return;
        }

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

  /**
   * Whether focus is moving to something still within the search, including the config popover
   */
  private containsFocusTarget(target: EventTarget | null) {
    const next = target as HTMLElement | null;
    if (!next) return false;

    return this.hostElem.nativeElement.contains(next) || !!next.closest('.search-config-popover');
  }

  /**
   * Tabbing past the last control in the popover leaves the search entirely, so tear both down
   */
  onConfigFocusOut(event: FocusEvent) {
    if (this.containsFocusTarget(event.relatedTarget)) return;

    this.configPopover()?.close();
    this.close();
  }

  close(event?: FocusEvent) {
    // Tabbing to the config button is still "inside" the search, so it must not collapse the bar
    if (event && this.containsFocusTarget(event.relatedTarget)) return;

    // If the user is tabbing out of the input field, check if there are results first before closing
    if (event && (this.hasAnyData() || this.searchTerm())) return;

    this.configPopover()?.close();

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
    this.focusedIndex.set(0);
    this.clearField.emit();
  }

  /**
   * Runs the same navigation a click on that row would, so Enter and the mouse can never diverge
   */
  private activateRow(index: number) {
    const row = this.flatRows()[index];
    if (!row) return;

    switch (row.kind) {
      case 'searchAll': this.searchAll(); break;
      case 'series': this.clickSeriesSearchResult(row.item as SearchResult); break;
      case 'collections': this.clickCollectionSearchResult(row.item as UserCollection); break;
      case 'readingLists': this.clickReadingListSearchResult(row.item as ReadingList); break;
      case 'bookmarks': this.clickBookmarkSearchResult(row.item as BookmarkSearchResult); break;
      case 'libraries': this.clickLibraryResult(row.item as Library); break;
      case 'genres': this.goToOther(SeriesFilterField.Genres, (row.item as Genre).id + ''); break;
      case 'tags': this.goToOther(SeriesFilterField.Tags, (row.item as Tag).id + ''); break;
      case 'persons': this.goToPerson(row.item as Person); break;
      case 'chapters': this.clickChapterSearchResult(row.item as Chapter); break;
      case 'files': this.clickFileSearchResult(row.item as MangaFile); break;
      case 'annotations': this.clickAnnotationSearchResult(row.item as Annotation); break;
    }

    // A mouse pick closes via the window:click listener; a keyboard pick has to close itself
    this.close();
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

  searchAll() {
    const term = this.searchTerm().trim();
    if (term.length === 0) return;

    this.goTo({field: SeriesFilterField.SeriesName, comparison: FilterComparison.Matches, value: term});
  }

  goToPerson(person: Person) {
    this.clearSearch();
    this.router.navigate(['person', person.name]);
  }

  protected readonly FilterField = SeriesFilterField;
}
