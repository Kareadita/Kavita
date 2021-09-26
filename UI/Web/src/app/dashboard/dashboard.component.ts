import { Component, OnInit } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { ServerService } from '../_services/server.service';

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit {

  tabs: Array<{title: string, fragment: string}> = [
    {title: 'Libraries', fragment: ''},
    {title: 'Lists', fragment: 'lists'},
    {title: 'Collections', fragment: 'collections'},
  ];
  active = this.tabs[0];

  constructor(public route: ActivatedRoute, private serverService: ServerService, 
    private toastr: ToastrService, private titleService: Title) {
    this.route.fragment.subscribe(frag => {
      const tab = this.tabs.filter(item => item.fragment === frag);
      if (tab.length > 0) {
        this.active = tab[0];
      } else {
        this.active = this.tabs[0]; // Default to first tab
      }
    });
    this.titleService.setTitle('Kavita - Dashboard');
  }

  ngOnInit(): void {
  }

}
