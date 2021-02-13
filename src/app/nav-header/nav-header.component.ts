import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { NgbTypeaheadSelectItemEvent } from '@ng-bootstrap/ng-bootstrap';
import { Observable, of } from 'rxjs';
import { debounceTime, distinctUntilChanged, tap, switchMap, catchError } from 'rxjs/operators';
import { AccountService } from '../_services/account.service';
import { LibraryService } from '../_services/library.service';
import { NavService } from '../_services/nav.service';

@Component({
  selector: 'app-nav-header',
  templateUrl: './nav-header.component.html',
  styleUrls: ['./nav-header.component.scss']
})
export class NavHeaderComponent implements OnInit {

  model: any;
  searching = false;
  searchFailed = false;

  constructor(public accountService: AccountService, private router: Router, public navService: NavService, private libraryService: LibraryService) { }

  ngOnInit(): void {
  }

  logout() {
    this.accountService.logout();
    this.router.navigateByUrl('/home');
  }

  moveFocus() {
    document.getElementById('content')?.focus();
  }


  search = (text$: Observable<string>) =>
    text$.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      tap(() => this.searching = true),
      switchMap(term =>
        this.libraryService.search(term).pipe(
          tap(() => this.searchFailed = false),
          catchError(() => {
            this.searchFailed = true;
            return of([]);
          }))
      ),
      tap(() => this.searching = false)
    )

  openSearchResult(event: NgbTypeaheadSelectItemEvent) {
    const libraryId = event.item.libraryId;
    const seriesId = event.item.seriesId;
    event.preventDefault();

    this.router.navigate(['library', libraryId, 'series', seriesId]);
  }

  // TODO: Implement a base64Image component that has fallbacks.
}
