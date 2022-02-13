import { Component, OnInit } from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import { NavService } from '../_services/nav.service';

@Component({
  selector: 'app-theme-test',
  templateUrl: './theme-test.component.html',
  styleUrls: ['./theme-test.component.scss']
})
export class ThemeTestComponent implements OnInit {

  constructor(public toastr: ToastrService, public navService: NavService) { }

  ngOnInit(): void {
  }

}
