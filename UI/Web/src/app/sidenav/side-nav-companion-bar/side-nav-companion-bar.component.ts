import { Component, ContentChild, EventEmitter, Input, OnInit, Output, TemplateRef } from '@angular/core';

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
   * If the page should show a filter
   */
  @Input() hasFilter: boolean = false;

  /**
   * Is the input open by default
   */
  @Input() filterOpenByDefault: boolean = false;

  /**
   * Should be passed through from Filter component.
   */
  //@Input() filterDisabled: EventEmitter<boolean> = new EventEmitter();

  @Output() filterOpen: EventEmitter<boolean> = new EventEmitter();

  isFilterOpen = false;

  constructor() { }

  ngOnInit(): void {
    this.isFilterOpen = this.filterOpenByDefault;
  }

  toggleFilter() {
    this.isFilterOpen = !this.isFilterOpen;
    this.filterOpen.emit(this.isFilterOpen);
  }

}
