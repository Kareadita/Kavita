import { Component, OnInit } from '@angular/core';
import { AccountService } from './_services/account.service';
import { NavService } from './_services/nav.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit {

  constructor(private accountService: AccountService, public navService: NavService) { }

  ngOnInit(): void {
    this.setCurrentUser();
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
