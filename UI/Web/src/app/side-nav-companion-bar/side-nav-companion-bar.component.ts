import { Component, ContentChild, OnInit, TemplateRef } from '@angular/core';

@Component({
  selector: 'app-side-nav-companion-bar',
  templateUrl: './side-nav-companion-bar.component.html',
  styleUrls: ['./side-nav-companion-bar.component.scss']
})
export class SideNavCompanionBarComponent implements OnInit {

  @ContentChild('pageHeader') pageHeaderTemplate!: TemplateRef<any>;

  constructor() { }

  ngOnInit(): void {
  }

}
