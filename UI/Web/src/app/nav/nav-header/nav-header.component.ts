import { DOCUMENT } from '@angular/common';
import { Component, ContentChildren, ElementRef, HostListener, Inject, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { NavigationStart, Router } from '@angular/router';
import { fromEvent, Subject } from 'rxjs';
import { debounceTime, filter, takeUntil } from 'rxjs/operators';
import { Chapter } from 'src/app/_models/chapter';
import { MangaFile } from 'src/app/_models/manga-file';
import { ScrollService } from 'src/app/_services/scroll.service';
import { SeriesService } from 'src/app/_services/series.service';
import { FilterQueryParam } from '../../shared/_services/filter-utilities.service';
import { CollectionTag } from '../../_models/collection-tag';
import { Library } from '../../_models/library';
import { PersonRole } from '../../_models/person';
import { ReadingList } from '../../_models/reading-list';
import { SearchResult } from '../../_models/search-result';
import { SearchResultGroup } from '../../_models/search/search-result-group';
import { AccountService } from '../../_services/account.service';
import { ImageService } from '../../_services/image.service';
import { LibraryService } from '../../_services/library.service';
import { NavService } from '../../_services/nav.service';

@Component({
  selector: 'app-nav-header',
  templateUrl: './nav-header.component.html',
  styleUrls: ['./nav-header.component.scss']
})
export class NavHeaderComponent implements OnInit, OnDestroy {

  @ViewChild('search') searchViewRef!: any;

  isLoading = false;
  debounceTime = 300;
  imageStyles = {width: '24px', 'margin-top': '5px'};
  searchResults: SearchResultGroup = new SearchResultGroup();
  searchTerm = '';
  customFilter: (items: SearchResult[], query: string) => SearchResult[] = (items: SearchResult[], query: string) => {
    const normalizedQuery = query.trim().toLowerCase();
    const matches = items.filter(item => {
      const normalizedSeriesName = item.name.toLowerCase().trim();
      const normalizedOriginalName = item.originalName.toLowerCase().trim();
      const normalizedLocalizedName = item.localizedName.toLowerCase().trim();
      return normalizedSeriesName.indexOf(normalizedQuery) >= 0 || normalizedOriginalName.indexOf(normalizedQuery) >= 0 || normalizedLocalizedName.indexOf(normalizedQuery) >= 0;
    });
    return matches;
  };


  backToTopNeeded = false;
  searchFocused: boolean = false;
  private readonly onDestroy = new Subject<void>();

  constructor(public accountService: AccountService, private router: Router, public navService: NavService,
    private libraryService: LibraryService, public imageService: ImageService, @Inject(DOCUMENT) private document: Document,
    private scrollService: ScrollService, private seriesService: SeriesService,) { }

  ngOnInit(): void {
    // setTimeout(() => this.setupScrollChecker(), 1000);
    // // TODO: on router change, reset the scroll check 

    // this.router.events
    //   .pipe(filter(event => event instanceof NavigationStart))
    //   .subscribe((event) => {
    //     setTimeout(() => this.setupScrollChecker(), 1000);
    //   });
  }

  // setupScrollChecker() {
  //   const viewportScroller = this.document.querySelector('.viewport-container');
  //   console.log('viewport container', viewportScroller);

  //   if (viewportScroller) {
  //     fromEvent(viewportScroller, 'scroll').pipe(debounceTime(20)).subscribe(() => this.checkBackToTopNeeded());
  //   } else {
  //     fromEvent(this.document.body, 'scroll').pipe(debounceTime(20)).subscribe(() => this.checkBackToTopNeeded());
  //   }
  // }

  @HostListener('body:scroll', [])
  checkBackToTopNeeded() {
    // TODO: This somehow needs to hook into the scrolling for virtual scroll
    
    const offset = this.scrollService.scrollPosition;
    if (offset > 100) {
      this.backToTopNeeded = true;
    } else if (offset < 40) {
        this.backToTopNeeded = false;
    }
  }

  ngOnDestroy() {
    this.onDestroy.next();
    this.onDestroy.complete();
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

      this.libraryService.search(val.trim()).pipe(takeUntil(this.onDestroy)).subscribe(results => {
        this.searchResults = results;
        this.isLoading = false;
      }, err => {
        this.searchResults.reset();
        this.isLoading = false;
        this.searchTerm = '';
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
  }

  clickSeriesSearchResult(item: SearchResult) {
    this.clearSearch();
    const libraryId = item.libraryId;
    const seriesId = item.seriesId;
    this.router.navigate(['library', libraryId, 'series', seriesId]);
  }

  clickFileSearchResult(item: MangaFile) {
    this.clearSearch();
    this.seriesService.getSeriesForMangaFile(item.id).subscribe(series => {
      if (series !== undefined && series !== null) {
        this.router.navigate(['library', series.libraryId, 'series', series.id]);
      }
    })
  }

  clickChapterSearchResult(item: Chapter) {
    this.clearSearch();
    this.seriesService.getSeriesForChapter(item.id).subscribe(series => {
      if (series !== undefined && series !== null) {
        this.router.navigate(['library', series.libraryId, 'series', series.id]);
      }
    })
  }

  clickLibraryResult(item: Library) {
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
    this.scrollService.scrollTo(0, this.document.body);
  }

  focusUpdate(searchFocused: boolean) {
    this.searchFocused = searchFocused
    return searchFocused;
  }

  hideSideNav() {
    this.navService.toggleSideNav();
  }
}
