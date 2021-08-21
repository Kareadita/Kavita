import { Component, OnInit } from '@angular/core';
import { NavigationStart, Router } from '@angular/router';
import { take } from 'rxjs/operators';
import { AccountService } from './_services/account.service';
import { LibraryService } from './_services/library.service';
import { MessageHubService } from './_services/message-hub.service';
import { NavService } from './_services/nav.service';
import { PresenceHubService } from './_services/presence-hub.service';
import { StatsService } from './_services/stats.service';
import { filter } from 'rxjs/operators';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit {

  constructor(private accountService: AccountService, public navService: NavService, 
    private statsService: StatsService, private messageHub: MessageHubService, 
    private presenceHub: PresenceHubService, private libraryService: LibraryService, private router: Router, private ngbModal: NgbModal) {
    
    // Close any open modals when a route change occurs
    router.events
      .pipe(filter(event => event instanceof NavigationStart))
      .subscribe((event) => {
        if (this.ngbModal.hasOpenModals()) {
          this.ngbModal.dismissAll();
        }
      });
  }

  ngOnInit(): void {
    this.setCurrentUser();

    this.statsService.getInfo().then(data => {
      this.statsService.sendClientInfo(data).subscribe(() => {/* No Operation */});
    });
  }


  setCurrentUser() {
    const user = this.accountService.getUserFromLocalStorage();

    this.accountService.setCurrentUser(user);

    if (user) {
      this.navService.setDarkMode(user.preferences.siteDarkMode);
      this.messageHub.createHubConnection(user);
      this.presenceHub.createHubConnection(user);
      this.libraryService.getLibraryNames().pipe(take(1)).subscribe(() => {/* No Operation */});
    } else {
      this.navService.setDarkMode(true);
    }
  }
}

