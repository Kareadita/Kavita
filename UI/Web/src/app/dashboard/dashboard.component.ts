import { Component, OnInit } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute } from '@angular/router';

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit {


  constructor(public route: ActivatedRoute, private titleService: Title) {
    this.titleService.setTitle('Kavita - Dashboard');
  }

  ngOnInit(): void {
  }

}
