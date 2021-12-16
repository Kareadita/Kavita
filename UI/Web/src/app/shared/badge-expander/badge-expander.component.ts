import { Component, ContentChild, Input, OnInit, TemplateRef } from '@angular/core';

@Component({
  selector: 'app-badge-expander',
  templateUrl: './badge-expander.component.html',
  styleUrls: ['./badge-expander.component.scss']
})
export class BadgeExpanderComponent implements OnInit {

  @Input() items: Array<any> = [];
  @ContentChild('badgeExpanderItem') itemTemplate!: TemplateRef<any>;

  isCollapsed: boolean = false;
  constructor() { }

  ngOnInit(): void {
    console.log(this.items);
  }

}
