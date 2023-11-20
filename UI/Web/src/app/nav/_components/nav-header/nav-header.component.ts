import {AsyncPipe, DOCUMENT, NgIf, NgOptimizedImage} from '@angular/common';
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  Inject,
  OnInit,
  ViewChild
} from '@angular/core';
import {NavigationEnd, Router, RouterLink, RouterLinkActive} from '@angular/router';
import {fromEvent} from 'rxjs';
import {debounceTime, distinctUntilChanged, filter, tap} from 'rxjs/operators';
import {Chapter} from 'src/app/_models/chapter';
import {CollectionTag} from 'src/app/_models/collection-tag';
import {Library} from 'src/app/_models/library/library';
import {MangaFile} from 'src/app/_models/manga-file';
import {PersonRole} from 'src/app/_models/metadata/person';
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
import {NgbDropdown, NgbDropdownItem, NgbDropdownMenu, NgbDropdownToggle} from '@ng-bootstrap/ng-bootstrap';
import {EventsWidgetComponent} from '../events-widget/events-widget.component';
import {SeriesFormatComponent} from '../../../shared/series-format/series-format.component';
import {ImageComponent} from '../../../shared/image/image.component';
import {GroupedTypeaheadComponent} from '../grouped-typeahead/grouped-typeahead.component';
import {TranslocoDirective} from "@ngneat/transloco";
import {FilterUtilitiesService} from "../../../shared/_services/filter-utilities.service";
import {FilterStatement} from "../../../_models/metadata/v2/filter-statement";
import {FilterField} from "../../../_models/metadata/v2/filter-field";
import {FilterComparison} from "../../../_models/metadata/v2/filter-comparison";

@Component({
    selector: 'app-nav-header',
    templateUrl: './nav-header.component.html',
    styleUrls: ['./nav-header.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [NgIf, RouterLink, RouterLinkActive, NgOptimizedImage, GroupedTypeaheadComponent, ImageComponent, SeriesFormatComponent, EventsWidgetComponent, NgbDropdown, NgbDropdownToggle, NgbDropdownMenu, NgbDropdownItem, AsyncPipe, PersonRolePipe, SentenceCasePipe, TranslocoDirective]
})
export class NavHeaderComponent implements OnInit {

  @ViewChild('search') searchViewRef!: any;
  private readonly destroyRef = inject(DestroyRef);

  isLoading = false;
  debounceTime = 300;
  searchResults: SearchResultGroup = new SearchResultGroup();
  searchTerm = '';


  backToTopNeeded = false;
  searchFocused: boolean = false;
  scrollElem: HTMLElement;
  protected readonly FilterField = FilterField;

  constructor(public accountService: AccountService, private router: Router, public navService: NavService,
    public imageService: ImageService, @Inject(DOCUMENT) private document: Document,
    private scrollService: ScrollService, private searchService: SearchService, private readonly cdRef: ChangeDetectorRef,
    private filterUtilityService: FilterUtilitiesService) {
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



  onChangeSearch(val: string) {
      this.isLoading = true;
      this.searchTerm = val.trim();
      this.cdRef.markForCheck();

      this.searchService.search(val.trim()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(results => {
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

  goToPerson(role: PersonRole, filter: any) {
    this.clearSearch();
    filter = filter + '';
    switch(role) {
      case PersonRole.Writer:
        this.goTo({field: FilterField.Writers, comparison: FilterComparison.Equal, value: filter});
        break;
      case PersonRole.Artist:
        this.goTo({field: FilterField.CoverArtist, comparison: FilterComparison.Equal, value: filter}); // TODO: What is this supposed to be?
        break;
      case PersonRole.Character:
        this.goTo({field: FilterField.Characters, comparison: FilterComparison.Equal, value: filter});
        break;
      case PersonRole.Colorist:
        this.goTo({field: FilterField.Colorist, comparison: FilterComparison.Equal, value: filter});
        break;
      case PersonRole.Editor:
        this.goTo({field: FilterField.Editor, comparison: FilterComparison.Equal, value: filter});
        break;
      case PersonRole.Inker:
        this.goTo({field: FilterField.Inker, comparison: FilterComparison.Equal, value: filter});
        break;
      case PersonRole.CoverArtist:
        this.goTo({field: FilterField.CoverArtist, comparison: FilterComparison.Equal, value: filter});
        break;
      case PersonRole.Letterer:
        this.goTo({field: FilterField.Letterer, comparison: FilterComparison.Equal, value: filter});
        break;
      case PersonRole.Penciller:
        this.goTo({field: FilterField.Penciller, comparison: FilterComparison.Equal, value: filter});
        break;
      case PersonRole.Publisher:
        this.goTo({field: FilterField.Publisher, comparison: FilterComparison.Equal, value: filter});
        break;
      case PersonRole.Translator:
        this.goTo({field: FilterField.Translators, comparison: FilterComparison.Equal, value: filter});
        break;
    }
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

  clickCollectionSearchResult(item: CollectionTag) {
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

  hideSideNav() {
    this.navService.toggleSideNav();
  }


}
