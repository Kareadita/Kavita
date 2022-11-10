import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { map, Observable, Subject, switchMap, takeUntil } from 'rxjs';
import { SearchResult } from 'src/app/_models/search-result';
import { SearchService } from 'src/app/_services/search.service';

@Component({
  selector: 'app-search',
  templateUrl: './search.component.html',
  styleUrls: ['./search.component.scss']
})
export class SearchComponent implements OnInit, OnDestroy {

  isLoading: boolean = false;
  originalQueryString: string = '';
  series$!: Observable<SearchResult[]>;

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
      map(g => g.series),
    );
  }

  ngOnDestroy() {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  loadPage() {}

}
