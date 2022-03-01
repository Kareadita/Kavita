import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { ServerService } from 'src/app/_services/server.service';
import { Title } from '@angular/platform-browser';
import { NavService } from '../../_services/nav.service';



@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit {

  tabs: Array<{title: string, fragment: string}> = [
    {title: 'General', fragment: ''},
    {title: 'Users', fragment: 'users'},
    {title: 'Libraries', fragment: 'libraries'},
    {title: 'System', fragment: 'system'},
  ];
  counter = this.tabs.length + 1;
  active = this.tabs[0];

  constructor(public route: ActivatedRoute, private serverService: ServerService, 
    private toastr: ToastrService, private titleService: Title, public navService: NavService) {
    this.route.fragment.subscribe(frag => {
      const tab = this.tabs.filter(item => item.fragment === frag);
      if (tab.length > 0) {
        this.active = tab[0];
      } else {
        this.active = this.tabs[0]; // Default to first tab
      }
    });

  }

  ngOnInit() {
    this.titleService.setTitle('Kavita - Admin Dashboard');
    this.navService.hideSideNav();
  }

  restartServer() {
    this.serverService.restart().subscribe(() => {
      setTimeout(() => this.toastr.success('Please reload.'), 1000);
    });
  }
}
