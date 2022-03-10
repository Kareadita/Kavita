import { Component, Input, OnDestroy, OnInit } from '@angular/core';
import { NavigationEnd, NavigationStart, Router } from '@angular/router';
import { filter, map, Subject, takeUntil } from 'rxjs';
import { NavService } from '../../_services/nav.service';

@Component({
  selector: 'app-side-nav-item',
  templateUrl: './side-nav-item.component.html',
  styleUrls: ['./side-nav-item.component.scss']
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


  highlighted = false;
  private onDestroy: Subject<void> = new Subject();
   
  constructor(public navService: NavService, private router: Router) {
    router.events
      .pipe(filter(event => event instanceof NavigationEnd), 
            takeUntil(this.onDestroy),
            map(evt => evt as NavigationEnd))
      .subscribe((evt: NavigationEnd) => {
        if (this.link !== undefined && evt.url.split('?')[0] === this.link) {
          this.highlighted = true;
        }
      });
  }

  ngOnInit(): void {}

  ngOnDestroy(): void {
      this.onDestroy.next();
      this.onDestroy.complete();
  }

}
