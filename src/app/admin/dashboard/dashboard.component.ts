import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { ServerService } from 'src/app/_services/server.service';
import { saveAs } from 'file-saver';



@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit {

  tabs: Array<{title: string, fragment: string}> = [
    {title: 'General', fragment: ''},
    {title: 'Users', fragment: 'users'},
    {title: 'Libraries', fragment: 'libraries'}
  ];
  counter = this.tabs.length + 1;
  active = this.tabs[0];

  constructor(public route: ActivatedRoute, private serverService: ServerService, private toastr: ToastrService) {
    this.route.fragment.subscribe(frag => {
      const tab = this.tabs.filter(item => item.fragment === frag);
      if (tab.length > 0) {
        this.active = tab[0];
      } else {
        this.active = this.tabs[0]; // Default to first tab
      }
    });

  }

  ngOnInit() {}

  restartServer() {
    this.serverService.restart().subscribe(() => {
      setTimeout(() => this.toastr.success('Please reload.'), 1000);
    });
  }

  fetchLogs() {
    this.serverService.fetchLogs().subscribe(res => {
      const blob = new Blob([res], {type: 'text/plain;charset=utf-8'});
      saveAs(blob, 'kavita.zip');
    });
  }

}
