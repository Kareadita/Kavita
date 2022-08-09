import { ChangeDetectionStrategy, ChangeDetectorRef, Component, Input, OnDestroy, OnInit } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter, map, Subject, takeUntil } from 'rxjs';
import { NavService } from '../../_services/nav.service';

@Component({
  selector: 'app-side-nav-item',
  templateUrl: './side-nav-item.component.html',
  styleUrls: ['./side-nav-item.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SideNavItemComponent implements OnInit, OnDestroy {
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

  @Input() comparisonMethod: 'startsWith' | 'equals' = 'equals';


  highlighted = false;
  private onDestroy: Subject<void> = new Subject();
   
  constructor(public navService: NavService, private router: Router, private readonly cdRef: ChangeDetectorRef) {
    router.events
      .pipe(filter(event => event instanceof NavigationEnd), 
            takeUntil(this.onDestroy),
            map(evt => evt as NavigationEnd))
      .subscribe((evt: NavigationEnd) => {
        this.updateHightlight(evt.url.split('?')[0]);
      });
  }

  ngOnInit(): void {
    setTimeout(() => {
      this.updateHightlight(this.router.url.split('?')[0]);
    }, 100);
    
  }

  ngOnDestroy(): void {
      this.onDestroy.next();
      this.onDestroy.complete();
  }

  updateHightlight(page: string) {
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
