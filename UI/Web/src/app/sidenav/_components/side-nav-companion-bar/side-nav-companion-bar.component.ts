import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  input,
  OnInit,
  output,
  signal,
  TemplateRef
} from '@angular/core';
import {NgbOffcanvas, NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {NavService} from 'src/app/_services/nav.service';
import {ToggleService} from 'src/app/_services/toggle.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {TranslocoDirective} from "@jsverse/transloco";
import {tap} from "rxjs";
import {BreakpointService} from "../../../_services/breakpoint.service";

/**
 * This should go on all pages which have the side nav present and is not Settings related.
 * Content inside [main] selector should not have any padding top or bottom, they are included in this component.
 */
@Component({
  selector: 'app-side-nav-companion-bar',
  imports: [NgbTooltip, TranslocoDirective],
  templateUrl: './side-nav-companion-bar.component.html',
  styleUrls: ['./side-nav-companion-bar.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SideNavCompanionBarComponent implements OnInit {
  private readonly navService = inject(NavService);
  protected readonly toggleService = inject(ToggleService);
  private readonly offcanvasService = inject(NgbOffcanvas);
  protected readonly breakpointService = inject(BreakpointService);
  private readonly destroyRef = inject(DestroyRef);

  /**
   * If the page should show a filter
   */
  hasFilter = input<boolean>(false);
  /**
   * If the page should show an extra section button
   */
  hasExtras = input<boolean>(false);

  /**
   * This implies there is a filter in effect on the underlying page. Will show UI styles to imply this to the user.
   */
  filterActive = input<boolean>(false);

  extraDrawer = input<TemplateRef<any> | undefined>(undefined);

  isFilterOpen = signal<boolean>(false);
  isExtrasOpen = signal<boolean>(false);

  filterOpen = output<boolean>();



  ngOnInit(): void {
    // If user opens side nav while filter is open on mobile, then collapse filter (as it doesn't render well) TODO: Change this when we have new drawer
    this.navService.sideNavCollapsed$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(sideNavCollapsed => {
      if (this.isFilterOpen() && sideNavCollapsed && this.breakpointService.isMobile()) {
        this.isFilterOpen.set(false);
        this.filterOpen.emit(false);
      }
    });

    this.toggleService.toggleState$.pipe(takeUntilDestroyed(this.destroyRef), tap(isOpen => {
      this.isFilterOpen.set(isOpen);
    })).subscribe();
  }

  openExtrasDrawer() {
    if (this.extraDrawer() === undefined) return;

    this.isExtrasOpen.set(true);
    const drawerRef = this.offcanvasService.open(this.extraDrawer(), {position: 'end', scroll: true});
    drawerRef.closed.subscribe(() => this.isExtrasOpen.set(false));
    drawerRef.dismissed.subscribe(() => this.isExtrasOpen.set(false));
  }

}
