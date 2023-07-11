import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
  Input,
  OnInit
} from '@angular/core';
import {NavigationEnd, Router, RouterLink} from '@angular/router';
import { filter, map } from 'rxjs';
import { NavService } from 'src/app/_services/nav.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {CommonModule, NgOptimizedImage} from "@angular/common";


@Component({
  selector: 'app-side-nav-item',
  standalone: true,
  imports: [CommonModule, RouterLink, NgOptimizedImage],
  templateUrl: './side-nav-item.component.html',
  styleUrls: ['./side-nav-item.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SideNavItemComponent implements OnInit {
  /**
   * Icon to display next to item. ie) 'fa-home'
   */
  @Input() icon: string = '';
  @Input() imageUrl: string | null = '';
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

  @Input() comparisonMethod: 'startsWith' | 'equals' = 'equals';
  private readonly destroyRef = inject(DestroyRef);


  highlighted = false;

  constructor(public navService: NavService, private router: Router, private readonly cdRef: ChangeDetectorRef) {
    router.events
      .pipe(filter(event => event instanceof NavigationEnd),
            takeUntilDestroyed(this.destroyRef),
            map(evt => evt as NavigationEnd))
      .subscribe((evt: NavigationEnd) => {
        this.updateHighlight(evt.url.split('?')[0]);

      });
  }

  ngOnInit(): void {
    setTimeout(() => {
      this.updateHighlight(this.router.url.split('?')[0]);
    }, 100);

  }

  updateHighlight(page: string) {
    if (this.link === undefined) {
      this.highlighted = false;
      this.cdRef.markForCheck();
      return;
    }

    if (!page.endsWith('/')) {
      page = page + '/';
    }

    if (this.comparisonMethod === 'equals' && page === this.link) {
      this.highlighted = true;
      this.cdRef.markForCheck();
      return;
    }
    if (this.comparisonMethod === 'startsWith' && page.startsWith(this.link)) {
      this.highlighted = true;
      this.cdRef.markForCheck();
      return;
    }

    this.highlighted = false;
    this.cdRef.markForCheck();
  }

}
