import { Component, OnDestroy, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, Subject } from 'rxjs';
import { filter, take, takeUntil, takeWhile } from 'rxjs/operators';
import { EVENTS, MessageHubService } from 'src/app/_services/message-hub.service';
import { UtilityService } from '../../shared/_services/utility.service';
import { Library } from '../../_models/library';
import { User } from '../../_models/user';
import { AccountService } from '../../_services/account.service';
import { Action, ActionFactoryService, ActionItem } from '../../_services/action-factory.service';
import { ActionService } from '../../_services/action.service';
import { LibraryService } from '../../_services/library.service';
import { NavService } from '../../_services/nav.service';

@Component({
  selector: 'app-side-nav',
  templateUrl: './side-nav.component.html',
  styleUrls: ['./side-nav.component.scss']
})
export class SideNavComponent implements OnInit, OnDestroy {

  user: User | undefined;
  libraries: Library[] = [];
  isAdmin = false;
  actions: ActionItem<Library>[] = [];

  filterQuery: string = '';
  filterLibrary = (library: Library) => {
    return library.name.toLowerCase().indexOf((this.filterQuery || '').toLowerCase()) >= 0;
  }

  private onDestory: Subject<void> = new Subject();


  constructor(public accountService: AccountService, private libraryService: LibraryService,
    public utilityService: UtilityService, private messageHub: MessageHubService,
    private actionFactoryService: ActionFactoryService, private actionService: ActionService, public navService: NavService) { }

  ngOnInit(): void {
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      this.user = user;
      if (this.user) {
        this.isAdmin = this.accountService.hasAdminRole(this.user);
      }
      this.libraryService.getLibrariesForMember().pipe(take(1)).subscribe((libraries: Library[]) => {
        this.libraries = libraries;
      });
      this.actions = this.actionFactoryService.getLibraryActions(this.handleAction.bind(this));
    });

    this.messageHub.messages$.pipe(takeUntil(this.onDestory), filter(event => event.event === EVENTS.LibraryModified)).subscribe(event => {
      this.libraryService.getLibrariesForMember().pipe(take(1)).subscribe((libraries: Library[]) => {
        this.libraries = libraries;
      });
    });

    
  }

  ngOnDestroy(): void {
    this.onDestory.next();
    this.onDestory.complete();
  }

  handleAction(action: Action, library: Library) {
    switch (action) {
      case(Action.ScanLibrary):
        this.actionService.scanLibrary(library);
        break;
      case(Action.RefreshMetadata):
        this.actionService.refreshMetadata(library);
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

}