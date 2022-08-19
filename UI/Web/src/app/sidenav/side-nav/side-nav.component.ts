import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { Subject } from 'rxjs';
import { filter, map, shareReplay, take, takeUntil } from 'rxjs/operators';
import { EVENTS, MessageHubService } from 'src/app/_services/message-hub.service';
import { Breakpoint, UtilityService } from '../../shared/_services/utility.service';
import { Library, LibraryType } from '../../_models/library';
import { AccountService } from '../../_services/account.service';
import { Action, ActionFactoryService, ActionItem } from '../../_services/action-factory.service';
import { ActionService } from '../../_services/action.service';
import { LibraryService } from '../../_services/library.service';
import { NavService } from '../../_services/nav.service';

@Component({
  selector: 'app-side-nav',
  templateUrl: './side-nav.component.html',
  styleUrls: ['./side-nav.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SideNavComponent implements OnInit, OnDestroy {

  libraries: Library[] = [];
  actions: ActionItem<Library>[] = [];

  filterQuery: string = '';
  filterLibrary = (library: Library) => {
    return library.name.toLowerCase().indexOf((this.filterQuery || '').toLowerCase()) >= 0;
  }

  private onDestroy: Subject<void> = new Subject();


  constructor(public accountService: AccountService, private libraryService: LibraryService,
    public utilityService: UtilityService, private messageHub: MessageHubService,
    private actionFactoryService: ActionFactoryService, private actionService: ActionService, 
    public navService: NavService, private router: Router, private readonly cdRef: ChangeDetectorRef) {

      this.router.events.pipe(
        filter(event => event instanceof NavigationEnd), 
        takeUntil(this.onDestroy),
        map(evt => evt as NavigationEnd),
        filter(() => this.utilityService.getActiveBreakpoint() < Breakpoint.Tablet))
      .subscribe((evt: NavigationEnd) => {
        // Collapse side nav on mobile
        this.navService.sideNavCollapsed$.pipe(take(1)).subscribe(collapsed => {
          if (!collapsed) {
            this.navService.toggleSideNav();
            this.cdRef.markForCheck();
          }
        });
      });
  }

  ngOnInit(): void {
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) {
        this.libraryService.getLibrariesForMember().pipe(take(1), shareReplay()).subscribe((libraries: Library[]) => {
          this.libraries = libraries;
          this.cdRef.markForCheck();
        });
      }
      this.actions = this.actionFactoryService.getLibraryActions(this.handleAction.bind(this));
      this.cdRef.markForCheck();
    });

    this.messageHub.messages$.pipe(takeUntil(this.onDestroy), filter(event => event.event === EVENTS.LibraryModified)).subscribe(event => {
      this.libraryService.getLibrariesForMember().pipe(take(1), shareReplay()).subscribe((libraries: Library[]) => {
        this.libraries = libraries;
        this.cdRef.markForCheck();
      });
    });
  }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  handleAction(action: Action, library: Library) {
    switch (action) {
      case(Action.ScanLibrary):
        this.actionService.scanLibrary(library);
        break;
      case(Action.RefreshMetadata):
        this.actionService.refreshMetadata(library);
        break;
      case (Action.AnalyzeFiles):
        this.actionService.analyzeFiles(library);
        break;
      default:
        break;
    }
  }


  performAction(action: ActionItem<Library>, library: Library) {
    if (typeof action.callback === 'function') {
      action.callback(action.action, library);
    }
  }

  getLibraryTypeIcon(format: LibraryType) {
    switch (format) {
      case LibraryType.Book:
        return 'fa-book';
      case LibraryType.Comic:
      case LibraryType.Manga:
        return 'fa-book-open';
    }
  }

  toggleNavBar() {
    this.navService.toggleSideNav();
  }

}