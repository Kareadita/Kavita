import { DOCUMENT } from '@angular/common';
import { Component, HostListener, Inject, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { Router } from '@angular/router';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { ScrollService } from '../scroll.service';
import { CollectionTag } from '../_models/collection-tag';
import { Library } from '../_models/library';
import { PersonRole } from '../_models/person';
import { ReadingList } from '../_models/reading-list';
import { SearchResult } from '../_models/search-result';
import { SearchResultGroup } from '../_models/search/search-result-group';
import { AccountService } from '../_services/account.service';
import { ImageService } from '../_services/image.service';
import { LibraryService } from '../_services/library.service';
import { NavService } from '../_services/nav.service';

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
    private scrollService: ScrollService) { }

  ngOnInit(): void {
    // this.navService.darkMode$.pipe(takeUntil(this.onDestroy)).subscribe(res => {
    //   if (res) {
    //     this.document.body.classList.remove('bg-light');
    //     this.document.body.classList.add('bg-dark');
    //   } else {
    //     this.document.body.classList.remove('bg-dark');
    //     this.document.body.classList.add('bg-light');
    //   }
    // });
  }

  @HostListener("window:scroll", [])
  checkBackToTopNeeded() {
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
    this.navService.toggleSideNavVisibility(false);
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
    params['page'] = 1;
    this.clearSearch();
    this.router.navigate(['all-series'], {queryParams: params});
  }

  goToPerson(role: PersonRole, filter: any) {
    // TODO: Move this to utility service
    this.clearSearch();
    switch(role) {
      case PersonRole.Writer:
        this.goTo('writers', filter);
        break;
      case PersonRole.Artist:
        this.goTo('artists', filter);
        break;
      case PersonRole.Character:
        this.goTo('character', filter);
        break;
      case PersonRole.Colorist:
        this.goTo('colorist', filter);
        break;
      case PersonRole.Editor:
        this.goTo('editor', filter);
        break;
      case PersonRole.Inker:
        this.goTo('inker', filter);
        break;
      case PersonRole.CoverArtist:
        this.goTo('coverArtists', filter);
        break;
      case PersonRole.Inker:
        this.goTo('inker', filter);
        break;
      case PersonRole.Letterer:
        this.goTo('letterer', filter);
        break;
      case PersonRole.Penciller:
        this.goTo('penciller', filter);
        break;
      case PersonRole.Publisher:
        this.goTo('publisher', filter);
        break;
      case PersonRole.Translator:
        this.goTo('translator', filter);
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
    window.scroll({
      top: 0,
      behavior: 'smooth' 
    });
  }

  focusUpdate(searchFocused: boolean) {
    this.searchFocused = searchFocused
    return searchFocused;
  }

  hideSideNav() {
    this.navService.toggleSideNav();
  }
}
