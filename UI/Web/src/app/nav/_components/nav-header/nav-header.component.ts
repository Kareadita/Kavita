import {DOCUMENT} from '@angular/common';
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  DestroyRef,
  inject,
  signal,
  ViewChild
} from '@angular/core';
import {Router, RouterLink, RouterLinkActive} from '@angular/router';
import {Chapter} from 'src/app/_models/chapter';
import {UserCollection} from 'src/app/_models/collection-tag';
import {Library} from 'src/app/_models/library/library';
import {MangaFile} from 'src/app/_models/manga-file';
import {Person} from 'src/app/_models/metadata/person';
import {ReadingList} from 'src/app/_models/reading-list';
import {SearchResult} from 'src/app/_models/search/search-result';
import {SearchResultGroup} from 'src/app/_models/search/search-result-group';
import {AccountService} from 'src/app/_services/account.service';
import {ImageService} from 'src/app/_services/image.service';
import {NavService} from 'src/app/_services/nav.service';
import {SearchService} from 'src/app/_services/search.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {SentenceCasePipe} from '../../../_pipes/sentence-case.pipe';
import {NgbDropdown, NgbDropdownItem, NgbDropdownMenu, NgbDropdownToggle, NgbModal} from '@ng-bootstrap/ng-bootstrap';
import {EventsWidgetComponent} from '../events-widget/events-widget.component';
import {SeriesFormatComponent} from '../../../shared/series-format/series-format.component';
import {ImageComponent} from '../../../shared/image/image.component';
import {GroupedTypeaheadComponent, SearchEvent} from '../grouped-typeahead/grouped-typeahead.component';
import {TranslocoDirective} from "@jsverse/transloco";
import {FilterUtilitiesService} from "../../../shared/_services/filter-utilities.service";
import {FilterStatement} from "../../../_models/metadata/v2/filter-statement";
import {FilterField} from "../../../_models/metadata/v2/filter-field";
import {FilterComparison} from "../../../_models/metadata/v2/filter-comparison";
import {BookmarkSearchResult} from "../../../_models/search/bookmark-search-result";
import {ScrobbleProvider} from "../../../_services/scrobbling.service";
import {CollectionOwnerComponent} from "../../../collections/_components/collection-owner/collection-owner.component";
import {PromotedIconComponent} from "../../../shared/_components/promoted-icon/promoted-icon.component";
import {SettingsTabId} from "../../../sidenav/preference-nav/preference-nav.component";
import {WikiLink} from "../../../_models/wiki";
import {NavLinkModalComponent} from "../nav-link-modal/nav-link-modal.component";
import {MetadataService} from "../../../_services/metadata.service";
import {Annotation} from "../../../book-reader/_models/annotations/annotation";
import {QuillViewComponent} from "ngx-quill";
import {AnnotationService} from "../../../_services/annotation.service";
import {ProfileIconComponent} from "../../../_single-module/profile-icon/profile-icon.component";
import {BreakpointService} from "../../../_services/breakpoint.service";

@Component({
  selector: 'app-nav-header',
  templateUrl: './nav-header.component.html',
  styleUrls: ['./nav-header.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RouterLinkActive, GroupedTypeaheadComponent, ImageComponent,
    SeriesFormatComponent, EventsWidgetComponent, NgbDropdown, NgbDropdownToggle, NgbDropdownMenu, NgbDropdownItem,
    SentenceCasePipe, TranslocoDirective, CollectionOwnerComponent, PromotedIconComponent, QuillViewComponent, ProfileIconComponent]
})
export class NavHeaderComponent {

  private readonly router = inject(Router);
  private readonly searchService = inject(SearchService);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  protected readonly accountService = inject(AccountService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly navService = inject(NavService);
  protected readonly imageService = inject(ImageService);
  protected readonly breakpointService = inject(BreakpointService);
  protected readonly modalService = inject(NgbModal);
  protected readonly metadataService = inject(MetadataService);
  private readonly annotationService = inject(AnnotationService);
  private readonly document = inject(DOCUMENT);

  @ViewChild('search') searchViewRef!: any;


  profileLink = computed(() => {
    return ['/profile', this.accountService.currentUserSignal()?.id ?? ''];
  });

  currentUser = computed(() => {
    return this.accountService.currentUserSignal();
  });

  isLoading = signal<boolean>(false);
  debounceTime = 300;
  searchResults: SearchResultGroup = new SearchResultGroup();
  searchTerm = '';

  moveFocus() {
    this.document.getElementById('content')?.focus();
  }

  onChangeSearch(evt: SearchEvent) {
      this.isLoading.set(true);
      this.searchTerm = evt.value.trim();
      this.cdRef.markForCheck();

      this.searchService.search(this.searchTerm, evt.includeFiles).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(results => {
        this.searchResults = results;
        this.isLoading.set(false);
        this.cdRef.markForCheck();
      }, () => {
        this.searchResults.reset();
        this.isLoading.set(false);
        this.searchTerm = '';
        this.cdRef.markForCheck();
      });
  }

  goTo(statement: FilterStatement<number>) {
    let params: any = {};
    const filter = this.metadataService.createDefaultFilterDto('series');
    filter.statements = [statement];
    params['page'] = 1;
    this.clearSearch();
    this.filterUtilityService.applyFilterWithParams(['all-series'], filter, params).subscribe();
  }

  goToOther(field: FilterField, value: string) {
    this.goTo({field, comparison: FilterComparison.Equal, value: value + ''});
  }

  goToPerson(person: Person) {
    this.clearSearch();
    this.router.navigate(['person', person.name]);
  }

  clearSearch() {
    this.searchViewRef.clear();
    this.searchTerm = '';
    this.searchResults = new SearchResultGroup();
    this.cdRef.markForCheck();
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

  toggleSideNav(event: any) {
    console.log('nav-header: toggling side nav');
    event.stopPropagation();
    this.navService.toggleSideNav();
  }

  openLinkSelectionMenu() {
    this.modalService.open(NavLinkModalComponent, {fullscreen: 'sm'});
  }

  protected readonly FilterField = FilterField;
  protected readonly WikiLink = WikiLink;
  protected readonly ScrobbleProvider = ScrobbleProvider;
  protected readonly SettingsTabId = SettingsTabId;
}
