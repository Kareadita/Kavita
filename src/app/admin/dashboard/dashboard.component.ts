import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit {

  tabs = ['users', 'libraries'];
  counter = this.tabs.length + 1;
  active = this.tabs[0];

  constructor(private router: Router) {
    // TODO: Depending on active route, set the tab else default to first tab.
    console.log('current route: ', this.router.url);
  }

  ngOnInit(): void {
  }

}
