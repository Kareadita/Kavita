import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { take } from 'rxjs/operators';
import { UtilityService } from '../shared/_services/utility.service';
import { Library } from '../_models/library';
import { User } from '../_models/user';
import { AccountService } from '../_services/account.service';
import { Action, ActionFactoryService, ActionItem } from '../_services/action-factory.service';
import { ActionService } from '../_services/action.service';
import { LibraryService } from '../_services/library.service';
import { NavService } from '../_services/nav.service';

@Component({
  selector: 'app-side-nav',
  templateUrl: './side-nav.component.html',
  styleUrls: ['./side-nav.component.scss']
})
export class SideNavComponent implements OnInit {

  user: User | undefined;
  libraries: Library[] = [];
  isAdmin = false;
  actions: ActionItem<Library>[] = [];

  constructor(public accountService: AccountService, private libraryService: LibraryService,
    public utilityService: UtilityService, private router: Router,
    private actionFactoryService: ActionFactoryService, private actionService: ActionService, public navService: NavService) { }

  ngOnInit(): void {
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      this.user = user;
      if (this.user) {
        this.isAdmin = this.accountService.hasAdminRole(this.user);
      }
      this.libraryService.getLibrariesForMember().pipe(take(1)).subscribe(libraries => {
        this.libraries = libraries;
      });
      this.actions = this.actionFactoryService.getLibraryActions(this.handleAction.bind(this));
    });

    this.navService.sideNavVisible$.subscribe()

    // const sideNav = this.document.querySelector('.side-nav');
    // if (sideNav?.classList.contains('closed')){
    //   sideNav?.classList.remove('closed');
    //   this.navService.showSideNav();
    // } else {
    //   sideNav?.classList.add('closed');
    //   this.navService.hideSideNav();
    // }
  }

  handleClick(event: Event, library: Library) {
    this.router.navigate(['library', library.id]);
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