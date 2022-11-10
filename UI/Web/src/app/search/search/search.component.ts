import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { map, Observable, Subject, switchMap, takeUntil } from 'rxjs';
import { SearchResult } from 'src/app/_models/search-result';
import { Series } from 'src/app/_models/series';
import { SearchService } from 'src/app/_services/search.service';

@Component({
  selector: 'app-search',
  templateUrl: './search.component.html',
  styleUrls: ['./search.component.scss']
})
export class SearchComponent implements OnInit, OnDestroy {

  isLoading: boolean = false;
  originalQueryString: string = '';
  series$!: Observable<Series[]>;

  private onDestroy: Subject<void> = new Subject();

  constructor(private route: ActivatedRoute, private router: Router, public searchService: SearchService,) {

  }

  ngOnInit(): void {
    const queryString = this.route.snapshot.queryParamMap.get('query');
    console.log('query: ', queryString)
    if (queryString === undefined || queryString === null || queryString === '') {
      this.router.navigateByUrl('/libraries');
      return;
    }
    this.originalQueryString = queryString;

    // const searchResults$ = this.searchService.searchTerm$.pipe(
    //   takeUntil(this.onDestroy),
    //   switchMap(() =>)

    // );

    this.series$ = this.searchService.searchResults$.pipe(
      takeUntil(this.onDestroy),
      map(g => g.series.map(s => {
        return {
          id: s.seriesId,
          sortName: s.sortName,
          libraryName: s.libraryName,
          libraryId: s.libraryId,
          localizedName: s.localizedName,
          name: s.name,
          originalName: s.originalName,
          format: s.format,
          volumes: [],
          pages: 0,
          pagesRead: 0,
          userRating: 0,
          userReview: '',
          coverImageLocked: false,
          sortNameLocked: false,
          localizedNameLocked: false,
          nameLocked: false,
          created: '',
          latestReadDate: '',
          lastChapterAdded: '',
          lastFolderScanned: '',
          wordCount: 0,
          minHoursToRead: 0,
          maxHoursToRead: 0,
          avgHoursToRead: 0,
          folderPath: '',
        };
      })),
    );
  }

  ngOnDestroy() {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  loadPage() {}

}
