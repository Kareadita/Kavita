import { Component, EventEmitter, Input, OnDestroy, OnInit, Output, TemplateRef } from '@angular/core';
import { NgbOffcanvas } from '@ng-bootstrap/ng-bootstrap';
import { Subject, takeUntil } from 'rxjs';
import { Breakpoint, UtilityService } from 'src/app/shared/_services/utility.service';
import { NavService } from 'src/app/_services/nav.service';
import { ToggleService } from 'src/app/_services/toggle.service';

/**
 * This should go on all pages which have the side nav present and is not Settings related.
 * Content inside [main] selector should not have any padding top or bottom, they are included in this component.
 */
@Component({
  selector: 'app-side-nav-companion-bar',
  templateUrl: './side-nav-companion-bar.component.html',
  styleUrls: ['./side-nav-companion-bar.component.scss']
})
export class SideNavCompanionBarComponent implements OnInit, OnDestroy {
  /**
   * If the page should show a filter
   */
  @Input() hasFilter: boolean = false;
  /**
   * If the page should show an extra section button
   */
  @Input() hasExtras: boolean = false;

  /**
   * Is the input open by default
   */
  @Input() filterOpenByDefault: boolean = false;

  /**
   * This implies there is a filter in effect on the underlying page. Will show UI styles to imply this to the user.
   */
  @Input() filterActive: boolean = false;

  @Input() extraDrawer!: TemplateRef<any>; 


  @Output() filterOpen: EventEmitter<boolean> = new EventEmitter();

  isFilterOpen = false;
  isExtrasOpen = false;

  private onDestroy: Subject<void> = new Subject();

  constructor(private navService: NavService, private utilityService: UtilityService, public toggleService: ToggleService, 
    private offcanvasService: NgbOffcanvas) {
  }

  ngOnInit(): void {
    this.isFilterOpen = this.filterOpenByDefault;

    // If user opens side nav while filter is open on mobile, then collapse filter (as it doesn't render well) TODO: Change this when we have new drawer
    this.navService.sideNavCollapsed$.pipe(takeUntil(this.onDestroy)).subscribe(sideNavCollapsed => {
      if (this.isFilterOpen && sideNavCollapsed && this.utilityService.getActiveBreakpoint() < Breakpoint.Tablet) {
        this.isFilterOpen = false;
        this.filterOpen.emit(this.isFilterOpen);
      }
    });
  }

  ngOnDestroy(): void {
      this.onDestroy.next();
      this.onDestroy.complete();
  }

  toggleFilter() {
    this.isFilterOpen = !this.isFilterOpen;
    this.filterOpen.emit(this.isFilterOpen);
  }

  openExtrasDrawer() {
    if (this.extraDrawer === undefined) return;

    this.isExtrasOpen = true;
    const drawerRef = this.offcanvasService.open(this.extraDrawer, {position: 'end', scroll: true});
    drawerRef.closed.subscribe(() => this.isExtrasOpen = false);
    drawerRef.dismissed.subscribe(() => this.isExtrasOpen = false);
  }

}
