import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  input,
  signal
} from '@angular/core';
import {NavigationEnd, Router, RouterLink} from '@angular/router';
import {filter} from 'rxjs';
import {NavService} from 'src/app/_services/nav.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {NgClass, NgTemplateOutlet} from "@angular/common";
import {ImageComponent} from "../../../shared/image/image.component";
import {UtilityService} from "../../../shared/_services/utility.service";
import {BreakpointService} from "../../../_services/breakpoint.service";
import {CompactNumberPipe} from "../../../_pipes/compact-number.pipe";


@Component({
  selector: 'app-side-nav-item',
  imports: [RouterLink, ImageComponent, NgTemplateOutlet, NgClass, CompactNumberPipe],
  templateUrl: './side-nav-item.component.html',
  styleUrls: ['./side-nav-item.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SideNavItemComponent {
  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);
  protected readonly navService = inject(NavService);
  protected readonly utilityService = inject(UtilityService);
  protected readonly breakpointService = inject(BreakpointService);

  /**
   * Id for automatic scrolling to.
   */
  id = input<string | null>(null);

  /**
   * Icon to display next to item. ie) 'fa-home'
   */
  icon = input<string>('');
  imageUrl = input<string | null>(null);
  /**
   * Removes all the space around the icon area
   */
  noIcon = input<boolean>(false)
  /**
   * Text for the item
   */
  title = input<string>('');
  /**
   * If a link should be generated when clicked. By default (undefined), no link will be generated
   */
  link = input<string | undefined>(undefined);
  /**
   * If external, link will be used as full href and rel will be applied
   */
  external = input<boolean>(false);
  /**
   * If using a link, then you can pass optional queryParameters
   */
  queryParams = input<any | undefined>(undefined);
  /**
   * If using a link, then you can pass optional fragment to append to the end
   */
  fragment = input<string | undefined>(undefined);
  /**
   * Optional count to pass in that will show as a red badge on the side, indicating some action needs to be taken
   */
  badgeCount = input<number>(-1);

  /**
   * Optional, display item in edit mode (replaces icon with handle)
   */
  editMode = input<boolean>(false);
  /**
   * Comparison Method for route to determine when to highlight item based on route
   */
  comparisonMethod = input<'startsWith' | 'equals'>('equals');

  private currentUrl = signal(this.router.url);

  highlighted = computed(() => {
    const routeUrl = this.currentUrl();
    const link = this.link();
    if (!link) return false;

    const [url, queryParamsStr] = routeUrl.split('?');
    const [page, fragmentPart = ''] = url.split('#');
    const routeFragment = url.includes('#') ? fragmentPart : undefined;

    return this.isHighlighted(page, queryParamsStr, routeFragment);
  });

  constructor() {
    this.router.events
      .pipe(
        filter(event => event instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef),
      ).subscribe((evt: NavigationEnd) => {
        this.currentUrl.set(evt.url);
        this.collapseNavIfApplicable();
      });
  }

  private isHighlighted(page: string, queryParamsStr?: string, fragment?: string): boolean {
    const link = this.link();
    if (!link) return false;

    if (!page.endsWith('/') && !queryParamsStr && this.fragment() === undefined && queryParamsStr === undefined) {
      page = page + '/';
    }

    let fragmentEqual = false;
    if (fragment === this.fragment()) {
      fragmentEqual = true;
    }
    if (this.fragment() === '' && fragment === undefined) {
      fragmentEqual = true;
    }

    const queryParamsEqual = this.queryParams() === queryParamsStr;

    if (this.comparisonMethod() === 'equals' && page === link && fragmentEqual && queryParamsEqual) {
      return true;
    }

    if (this.comparisonMethod() === 'startsWith' && page.startsWith(link)) {
      if (queryParamsStr && queryParamsStr === this.queryParams() && fragmentEqual) {
        return true;
      }
      return true;
    }

    return false;
  }

  openLink() {
    this.collapseNavIfApplicable();

    const queryParams = this.queryParams();
    const fragment = this.fragment();

    if (queryParams && Object.keys(queryParams).length !== 0) {
      this.router.navigateByUrl(this.link() + '?' + queryParams);
      return;
    } else if (fragment) {
      this.router.navigateByUrl(this.link() + '#' + fragment);
      return;
    }

    this.router.navigateByUrl(this.link()!);
  }

  // If on mobile, automatically collapse the side nav after making a selection
  collapseNavIfApplicable() {
    if (!this.breakpointService.isMobile()) return;
    this.navService.collapseSideNav(true);
  }

}
