import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { ServerService } from 'src/app/_services/server.service';
import { Title } from '@angular/platform-browser';
import { NavService } from '../../_services/nav.service';

enum TabID {
  General = '',
  Email = 'email',
  Media = 'media',
  Users = 'users',
  Libraries = 'libraries',
  System = 'system',
  Plugins = 'plugins',
  Tasks = 'tasks',
  Logs = 'logs',
  Statistics = 'statistics',
  KavitaPlus = 'kavitaplus'
}

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit {

  tabs: Array<{title: string, fragment: string}> = [
    {title: 'General', fragment: TabID.General},
    {title: 'Users', fragment: TabID.Users},
    {title: 'Libraries', fragment: TabID.Libraries},
    //{title: 'Logs', fragment: TabID.Logs},
    {title: 'Media', fragment: TabID.Media},
    {title: 'Email', fragment: TabID.Email},
    //{title: 'Plugins', fragment: TabID.Plugins},
    {title: 'Tasks', fragment: TabID.Tasks},
    {title: 'Statistics', fragment: TabID.Statistics},
    {title: 'System', fragment: TabID.System},
    {title: 'Kavita+', fragment: TabID.KavitaPlus},
  ];
  active = this.tabs[0];

  get TabID() {
    return TabID;
  }

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
  }
}
