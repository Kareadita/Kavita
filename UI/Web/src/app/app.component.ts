import { Component, HostListener, Inject, OnInit } from '@angular/core';
import { NavigationStart, Router } from '@angular/router';
import { take } from 'rxjs/operators';
import { AccountService } from './_services/account.service';
import { LibraryService } from './_services/library.service';
import { MessageHubService } from './_services/message-hub.service';
import { NavService } from './_services/nav.service';
import { filter } from 'rxjs/operators';
import { NgbModal, NgbRatingConfig } from '@ng-bootstrap/ng-bootstrap';
import { DOCUMENT } from '@angular/common';
import { ThemeService } from './theme.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit {

  constructor(private accountService: AccountService, public navService: NavService, 
    private messageHub: MessageHubService, private libraryService: LibraryService, 
    router: Router, private ngbModal: NgbModal, ratingConfig: NgbRatingConfig, 
    @Inject(DOCUMENT) private document: Document, private themeService: ThemeService) {

    // Setup default rating config
    ratingConfig.max = 5;
    ratingConfig.resettable = true;
    
    // Close any open modals when a route change occurs
    router.events
      .pipe(filter(event => event instanceof NavigationStart))
      .subscribe((event) => {
        if (this.ngbModal.hasOpenModals()) {
          this.ngbModal.dismissAll();
        }
      });
  }

  @HostListener('resize')
  onResize() {
    this.setDocHeight();
  }

  @HostListener('orientationchange')
  onOrientationChange() {
    this.setDocHeight();
  }

  ngOnInit(): void {
    this.setCurrentUser();

    this.setDocHeight();
  }

  setCurrentUser() {
    const user = this.accountService.getUserFromLocalStorage();
    this.accountService.setCurrentUser(user);

    if (user) {
      //this.navService.setDarkMode(user.preferences.siteDarkMode);
      this.themeService.setTheme(user.preferences.theme.name);
      this.messageHub.createHubConnection(user, this.accountService.hasAdminRole(user));
      this.libraryService.getLibraryNames().pipe(take(1)).subscribe(() => {/* No Operation */});
    } else {
      //this.navService.setDarkMode(true);
      this.themeService.setTheme(this.themeService.defaultTheme);
    }
  }

  setDocHeight() {
    // Sets a CSS variable for the actual device viewport height. Needed for mobile dev.
    this.document.documentElement.style.setProperty('--vh', `${window.innerHeight/100}px`);
  }
}
