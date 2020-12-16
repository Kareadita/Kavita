import { Component, OnInit } from '@angular/core';

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit {

  tabs = ['users', 'libraries'];
  counter = this.tabs.length + 1;
  active = this.tabs[0];

  constructor() { }

  ngOnInit(): void {
  }

}
