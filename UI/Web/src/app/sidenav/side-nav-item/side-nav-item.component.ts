import { Component, Input, OnInit } from '@angular/core';

@Component({
  selector: 'app-side-nav-item',
  templateUrl: './side-nav-item.component.html',
  styleUrls: ['./side-nav-item.component.scss']
})
export class SideNavItemComponent implements OnInit {

  /**
   * Icon to display next to item. ie) 'fa-home'
   */
  @Input() icon: string = '';
  /**
   * Text for the item
   */
  @Input() title: string = '';

  /**
   * If a link should be generated when clicked. By default (undefined), no link will be generated
   */
  @Input() link: string | undefined;

  constructor() { }

  ngOnInit(): void {
  }

}
