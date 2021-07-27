import { Component, OnInit } from '@angular/core';
import { AccountService } from './_services/account.service';
import { NavService } from './_services/nav.service';
import { StatsService } from './_services/stats.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit {

  constructor(private accountService: AccountService, public navService: NavService, private statsService: StatsService) { }

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
    } else {
      this.navService.setDarkMode(true);
    }
  }
}

