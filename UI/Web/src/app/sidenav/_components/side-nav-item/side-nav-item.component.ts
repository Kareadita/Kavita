import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, Input, OnInit} from '@angular/core';
import {NavigationEnd, Router, RouterLink} from '@angular/router';
import {filter, map, tap} from 'rxjs';
import {NavService} from 'src/app/_services/nav.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {AsyncPipe, NgClass, NgOptimizedImage, NgTemplateOutlet} from "@angular/common";
import {ImageComponent} from "../../../shared/image/image.component";
import {Breakpoint, UtilityService} from "../../../shared/_services/utility.service";


@Component({
  selector: 'app-side-nav-item',
  standalone: true,
  imports: [RouterLink, NgOptimizedImage, ImageComponent, NgTemplateOutlet, NgClass, AsyncPipe],
  templateUrl: './side-nav-item.component.html',
  styleUrls: ['./side-nav-item.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SideNavItemComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);
  private readonly cdRef = inject(ChangeDetectorRef);
  protected readonly navService = inject(NavService);
  protected readonly utilityService = inject(UtilityService);

  /**
   * Id for automatic scrolling to.
   */
  @Input() id: string | null = null;

  /**
   * Icon to display next to item. ie) 'fa-home'
   */
  @Input() icon: string = '';
  @Input() imageUrl: string | null = '';
  /**
   * Removes all the space around the icon area
   */
  @Input() noIcon: boolean = false;
  /**
   * Text for the item
   */
  @Input() title: string = '';

  /**
   * If a link should be generated when clicked. By default (undefined), no link will be generated
   */
  @Input() link: string | undefined;
  /**
   * If external, link will be used as full href and rel will be applied
   */
  @Input() external: boolean = false;
  /**
   * If using a link, then you can pass optional queryParameters
   */
  @Input() queryParams: any | undefined = undefined;
  /**
   * If using a lin, then you can pass optional fragment to append to the end
   */
  @Input() fragment: string | undefined = undefined;
  /**
   * Optional count to pass in that will show as a red badge on the side, indicating some action needs to be taken
   */
  @Input() badgeCount: number | null = -1;


  @Input() comparisonMethod: 'startsWith' | 'equals' = 'equals';



  highlighted = false;

  constructor() {
    this.router.events
      .pipe(
        filter(event => event instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef),
        map(evt => evt as NavigationEnd),
        tap((evt: NavigationEnd) => this.triggerHighlightCheck(evt.url)),
        tap(_ => this.collapseNavIfApplicable())
      ).subscribe();
  }

  ngOnInit(): void {
    setTimeout(() => {
      this.triggerHighlightCheck(this.router.url);
    }, 100);
  }

  triggerHighlightCheck(routeUrl: string) {
    const [url, queryParams] = routeUrl.split('?');
    const [page, fragment = ''] = url.split('#');

    this.updateHighlight(page, queryParams, url.includes('#') ? fragment : undefined);
  }


  updateHighlight(page: string, queryParams?: string, fragment?: string) {
    if (this.link === undefined) {
      this.highlighted = false;
      this.cdRef.markForCheck();
      return;
    }

    if (!page.endsWith('/') && !queryParams && this.fragment === undefined && queryParams === undefined) {
      page = page + '/';
    }

    let fragmentEqual = false;
    if (fragment === this.fragment) {
      fragmentEqual = true;
    }
    if (this.fragment === '' && fragment === undefined) { // This is the case where we load a fragment of nothing and browser removes the #
      fragmentEqual = true;
    }

    const queryParamsEqual = this.queryParams === queryParams;

    if (this.comparisonMethod === 'equals' && page === this.link && fragmentEqual && queryParamsEqual) {
      this.highlighted = true;
      this.cdRef.markForCheck();
      return;
    }

    if (this.comparisonMethod === 'startsWith' && page.startsWith(this.link)) {
      if (queryParams && queryParams === this.queryParams && fragmentEqual) {
        this.highlighted = true;
        this.cdRef.markForCheck();
        return;
      }

      this.highlighted = true;
      this.cdRef.markForCheck();
      return;
    }

    this.highlighted = false;
    this.cdRef.markForCheck();
  }

  openLink() {
    this.collapseNavIfApplicable();

    if (Object.keys(this.queryParams).length !== 0) {
      this.router.navigateByUrl(this.link + '?' + this.queryParams);
      return;
    } else if (this.fragment) {
      this.router.navigateByUrl(this.link + '#' + this.fragment);
      return;
    }

    this.router.navigateByUrl(this.link!);
  }

  // If on mobile, automatically collapse the side nav after making a selection
  collapseNavIfApplicable() {
    if (this.utilityService.getActiveBreakpoint() < Breakpoint.Tablet) {
      this.navService.collapseSideNav(true);
    }
  }

}
