import { Component, ContentChild, Input, OnInit, TemplateRef } from '@angular/core';

/**
 * This should go on all pages which have the side nav present and is not Settings related.
 * Content inside [main] selector should not have any padding top or bottom, they are included in this component.
 */
@Component({
  selector: 'app-side-nav-companion-bar',
  templateUrl: './side-nav-companion-bar.component.html',
  styleUrls: ['./side-nav-companion-bar.component.scss']
})
export class SideNavCompanionBarComponent implements OnInit {

  /**
   * Show a dedicated button to go back one history event.
   */
  @Input() showGoBack: boolean = false;
  /**
   * Title of the Page. Should be simple.
   */
  @Input() pageHeader: string = '';

  constructor() { }

  ngOnInit(): void {
  }

  goBack() {

  }

}
