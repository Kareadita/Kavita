import { Component, OnInit } from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import { TagBadgeCursor } from '../shared/tag-badge/tag-badge.component';
import { NavService } from '../_services/nav.service';

@Component({
  selector: 'app-theme-test',
  templateUrl: './theme-test.component.html',
  styleUrls: ['./theme-test.component.scss']
})
export class ThemeTestComponent implements OnInit {

  tabs: Array<{title: string, fragment: string}> = [
    {title: 'General', fragment: ''},
    {title: 'Users', fragment: 'users'},
    {title: 'Libraries', fragment: 'libraries'},
    {title: 'System', fragment: 'system'},
    {title: 'Changelog', fragment: 'changelog'},
  ];
  active = this.tabs[0];

  get TagBadgeCursor(): typeof TagBadgeCursor {
    return TagBadgeCursor;
  }

  constructor(public toastr: ToastrService, public navService: NavService) { }

  ngOnInit(): void {
  }

}
