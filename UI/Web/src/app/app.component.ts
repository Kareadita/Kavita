import { Component, OnInit } from '@angular/core';
import { take } from 'rxjs/operators';
import { AccountService } from './_services/account.service';
import { LibraryService } from './_services/library.service';
import { MessageHubService } from './_services/message-hub.service';
import { NavService } from './_services/nav.service';
import { PresenceHubService } from './_services/presence-hub.service';
import { StatsService } from './_services/stats.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit {

  constructor(private accountService: AccountService, public navService: NavService, 
    private statsService: StatsService, private messageHub: MessageHubService, 
    private presenceHub: PresenceHubService, private libraryService: LibraryService) { }

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

