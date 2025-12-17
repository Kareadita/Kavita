import {ChangeDetectorRef, DestroyRef, Directive, ElementRef, inject, output} from '@angular/core';
import {Location} from "@angular/common";
import {NgbNav, NgbNavChangeEvent} from "@ng-bootstrap/ng-bootstrap";
import {filter, tap} from "rxjs";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {ActivatedRoute} from "@angular/router";

@Directive({
  selector: 'ul[ngbNav][syncUrlFragment]',
  standalone: true,
})
export class NavTabUrlDirective {
  private readonly location = inject(Location);
  private readonly nav = inject(NgbNav);
  private readonly destroyRef = inject(DestroyRef);

  /** Re-emits navChange for consumers who still need to react to tab changes */
  readonly navChange = output<NgbNavChangeEvent>();

  constructor() {

    this.nav.navChange.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event: NgbNavChangeEvent) => {
      this.updateUrlFragment(event.nextId);
      this.navChange.emit(event);
    });
  }

  private updateUrlFragment(tabId: string): void {
    const pathWithoutFragment = this.location.path().split('#')[0];
    this.location.replaceState(`${pathWithoutFragment}#${tabId}`);
  }
}
