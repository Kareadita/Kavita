import { DOCUMENT } from '@angular/common';
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, DestroyRef,
  ElementRef,
  inject,
  Inject,
  OnInit,
  ViewChild
} from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { fromEvent } from 'rxjs';
import { debounceTime, distinctUntilChanged, filter, takeUntil, tap } from 'rxjs/operators';
import { FilterQueryParam } from 'src/app/shared/_services/filter-utilities.service';
import { Chapter } from 'src/app/_models/chapter';
import { CollectionTag } from 'src/app/_models/collection-tag';
import { Library } from 'src/app/_models/library';
import { MangaFile } from 'src/app/_models/manga-file';
import { PersonRole } from 'src/app/_models/metadata/person';
import { ReadingList } from 'src/app/_models/reading-list';
import { SearchResult } from 'src/app/_models/search/search-result';
import { SearchResultGroup } from 'src/app/_models/search/search-result-group';
import { AccountService } from 'src/app/_services/account.service';
import { ImageService } from 'src/app/_services/image.service';
import { NavService } from 'src/app/_services/nav.service';
import { ScrollService } from 'src/app/_services/scroll.service';
import { SearchService } from 'src/app/_services/search.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";

@Component({
  selector: 'app-nav-header',
  templateUrl: './nav-header.component.html',
  styleUrls: ['./nav-header.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
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

  constructor(public accountService: AccountService, private router: Router, public navService: NavService,
    public imageService: ImageService, @Inject(DOCUMENT) private document: Document,
    private scrollService: ScrollService, private searchService: SearchService, private readonly cdRef: ChangeDetectorRef) {
      this.scrollElem = this.document.body;
  }

  ngOnInit(): void {
    this.scrollService.scrollContainer$.pipe(distinctUntilChanged(), takeUntilDestroyed(this.destroyRef), tap((scrollContainer) => {
      if (scrollContainer === 'body' || scrollContainer === undefined) {
        this.scrollElem = this.document.body;
        fromEvent(this.document.body, 'scroll').pipe(debounceTime(20)).subscribe(() => this.checkBackToTopNeeded(this.document.body));
      } else {
        const elem = scrollContainer as ElementRef<HTMLDivElement>;
        this.scrollElem = elem.nativeElement;
        fromEvent(elem.nativeElement, 'scroll').pipe(debounceTime(20)).subscribe(() => this.checkBackToTopNeeded(elem.nativeElement));
      }
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

  goTo(queryParamName: string, filter: any) {
    let params: any = {};
    params[queryParamName] = filter;
    params[FilterQueryParam.Page] = 1;
    this.clearSearch();
    this.router.navigate(['all-series'], {queryParams: params});
  }

  goToPerson(role: PersonRole, filter: any) {
    this.clearSearch();
    switch(role) {
      case PersonRole.Writer:
        this.goTo(FilterQueryParam.Writers, filter);
        break;
      case PersonRole.Artist:
        this.goTo(FilterQueryParam.Artists, filter);
        break;
      case PersonRole.Character:
        this.goTo(FilterQueryParam.Character, filter);
        break;
      case PersonRole.Colorist:
        this.goTo(FilterQueryParam.Colorist, filter);
        break;
      case PersonRole.Editor:
        this.goTo(FilterQueryParam.Editor, filter);
        break;
      case PersonRole.Inker:
        this.goTo(FilterQueryParam.Inker, filter);
        break;
      case PersonRole.CoverArtist:
        this.goTo(FilterQueryParam.CoverArtists, filter);
        break;
      case PersonRole.Letterer:
        this.goTo(FilterQueryParam.Letterer, filter);
        break;
      case PersonRole.Penciller:
        this.goTo(FilterQueryParam.Penciller, filter);
        break;
      case PersonRole.Publisher:
        this.goTo(FilterQueryParam.Publisher, filter);
        break;
      case PersonRole.Translator:
        this.goTo(FilterQueryParam.Translator, filter);
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
