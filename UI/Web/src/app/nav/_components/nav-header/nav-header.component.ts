import {AsyncPipe, DOCUMENT, NgOptimizedImage, NgTemplateOutlet} from '@angular/common';
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  ElementRef, HostListener,
  inject,
  Inject,
  OnInit,
  ViewChild
} from '@angular/core';
import {NavigationEnd, Router, RouterLink, RouterLinkActive} from '@angular/router';
import {BehaviorSubject, fromEvent, Observable} from 'rxjs';
import {debounceTime, distinctUntilChanged, filter, tap} from 'rxjs/operators';
import {Chapter} from 'src/app/_models/chapter';
import {UserCollection} from 'src/app/_models/collection-tag';
import {Library} from 'src/app/_models/library/library';
import {MangaFile} from 'src/app/_models/manga-file';
import {Person, PersonRole} from 'src/app/_models/metadata/person';
import {ReadingList} from 'src/app/_models/reading-list';
import {SearchResult} from 'src/app/_models/search/search-result';
import {SearchResultGroup} from 'src/app/_models/search/search-result-group';
import {AccountService} from 'src/app/_services/account.service';
import {ImageService} from 'src/app/_services/image.service';
import {NavService} from 'src/app/_services/nav.service';
import {ScrollService} from 'src/app/_services/scroll.service';
import {SearchService} from 'src/app/_services/search.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {SentenceCasePipe} from '../../../_pipes/sentence-case.pipe';
import {PersonRolePipe} from '../../../_pipes/person-role.pipe';
import {NgbDropdown, NgbDropdownItem, NgbDropdownMenu, NgbDropdownToggle, NgbModal} from '@ng-bootstrap/ng-bootstrap';
import {EventsWidgetComponent} from '../events-widget/events-widget.component';
import {SeriesFormatComponent} from '../../../shared/series-format/series-format.component';
import {ImageComponent} from '../../../shared/image/image.component';
import {GroupedTypeaheadComponent, SearchEvent} from '../grouped-typeahead/grouped-typeahead.component';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {FilterUtilitiesService} from "../../../shared/_services/filter-utilities.service";
import {FilterStatement} from "../../../_models/metadata/v2/filter-statement";
import {FilterField} from "../../../_models/metadata/v2/filter-field";
import {FilterComparison} from "../../../_models/metadata/v2/filter-comparison";
import {BookmarkSearchResult} from "../../../_models/search/bookmark-search-result";
import {ScrobbleProvider} from "../../../_services/scrobbling.service";
import {ProviderImagePipe} from "../../../_pipes/provider-image.pipe";
import {ProviderNamePipe} from "../../../_pipes/provider-name.pipe";
import {CollectionOwnerComponent} from "../../../collections/_components/collection-owner/collection-owner.component";
import {PromotedIconComponent} from "../../../shared/_components/promoted-icon/promoted-icon.component";
import {SettingsTabId} from "../../../sidenav/preference-nav/preference-nav.component";
import {Breakpoint, UtilityService} from "../../../shared/_services/utility.service";
import {WikiLink} from "../../../_models/wiki";
import {
  GenericListModalComponent
} from "../../../statistics/_components/_modals/generic-list-modal/generic-list-modal.component";
import {NavLinkModalComponent} from "../nav-link-modal/nav-link-modal.component";

@Component({
    selector: 'app-nav-header',
    templateUrl: './nav-header.component.html',
    styleUrls: ['./nav-header.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [RouterLink, RouterLinkActive, NgOptimizedImage, GroupedTypeaheadComponent, ImageComponent,
    SeriesFormatComponent, EventsWidgetComponent, NgbDropdown, NgbDropdownToggle, NgbDropdownMenu, NgbDropdownItem,
    AsyncPipe, PersonRolePipe, SentenceCasePipe, TranslocoDirective, ProviderImagePipe, ProviderNamePipe, CollectionOwnerComponent, PromotedIconComponent, NgTemplateOutlet]
})
export class NavHeaderComponent implements OnInit {

  private readonly router = inject(Router);
  private readonly scrollService = inject(ScrollService);
  private readonly searchService = inject(SearchService);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  protected readonly accountService = inject(AccountService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly navService = inject(NavService);
  protected readonly imageService = inject(ImageService);
  protected readonly utilityService = inject(UtilityService);
  protected readonly modalService = inject(NgbModal);

  protected readonly FilterField = FilterField;
  protected readonly WikiLink = WikiLink;
  protected readonly ScrobbleProvider = ScrobbleProvider;
  protected readonly SettingsTabId = SettingsTabId;
  protected readonly Breakpoint = Breakpoint;

  @ViewChild('search') searchViewRef!: any;


  isLoading = false;
  debounceTime = 300;
  searchResults: SearchResultGroup = new SearchResultGroup();
  searchTerm = '';

  backToTopNeeded = false;
  searchFocused: boolean = false;
  scrollElem: HTMLElement;

  breakpointSource = new BehaviorSubject<Breakpoint>(this.utilityService.getActiveBreakpoint());
  breakpoint$: Observable<Breakpoint> = this.breakpointSource.asObservable();

  @HostListener('window:resize', ['$event'])
  @HostListener('window:orientationchange', ['$event'])
  onResize(){
    this.breakpointSource.next(this.utilityService.getActiveBreakpoint());
  }

  constructor(@Inject(DOCUMENT) private document: Document) {
      this.scrollElem = this.document.body;
  }

  ngOnInit(): void {
    this.scrollService.scrollContainer$.pipe(distinctUntilChanged(), takeUntilDestroyed(this.destroyRef), tap((scrollContainer) => {
      if (scrollContainer === 'body' || scrollContainer === undefined) {
        this.scrollElem = this.document.body;
      } else {
        const elem = scrollContainer as ElementRef<HTMLDivElement>;
        this.scrollElem = elem.nativeElement;
      }
      fromEvent(this.scrollElem, 'scroll').pipe(debounceTime(20)).subscribe(() => this.checkBackToTopNeeded(this.scrollElem));
    })).subscribe();

    // Sometimes the top event emitter can be slow, so let's also check when a navigation occurs and recalculate
    this.router.events
    .pipe(filter(event => event instanceof NavigationEnd))
    .subscribe(() => {
      this.checkBackToTopNeeded(this.scrollElem);
    });
  }

  checkBackToTopNeeded(elem: HTMLElement) {
    const offset = elem.scrollTop || 0;
    if (offset > 100) {
      this.backToTopNeeded = true;
    } else if (offset < 40) {
        this.backToTopNeeded = false;
    }
    this.cdRef.markForCheck();
  }

  logout() {
    this.accountService.logout();
    this.navService.hideNavBar();
    this.navService.hideSideNav();
    this.router.navigateByUrl('/login');
  }

  moveFocus() {
    this.document.getElementById('content')?.focus();
  }

  onChangeSearch(evt: SearchEvent) {
      this.isLoading = true;
      this.searchTerm = evt.value.trim();
      this.cdRef.markForCheck();

      this.searchService.search(this.searchTerm, evt.includeFiles).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(results => {
        this.searchResults = results;
        this.isLoading = false;
        this.cdRef.markForCheck();
      }, () => {
        this.searchResults.reset();
        this.isLoading = false;
        this.searchTerm = '';
        this.cdRef.markForCheck();
      });
  }

  goTo(statement: FilterStatement) {
    let params: any = {};
    const filter = this.filterUtilityService.createSeriesV2Filter();
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


  scrollToTop() {
    this.scrollService.scrollTo(0, this.scrollElem);
  }

  focusUpdate(searchFocused: boolean) {
    this.searchFocused = searchFocused;
    this.cdRef.markForCheck();
  }

  toggleSideNav(event: any) {
    event.stopPropagation();
    this.navService.toggleSideNav();
  }

  openLinkSelectionMenu() {
    const ref = this.modalService.open(NavLinkModalComponent, {fullscreen: 'sm'});
    ref.componentInstance.logoutFn = this.logout.bind(this);
  }

}
