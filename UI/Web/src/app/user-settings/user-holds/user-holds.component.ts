import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject} from '@angular/core';
import {CommonModule} from '@angular/common';
import {ScrobblingService} from "../../_services/scrobbling.service";
import {shareReplay} from "rxjs/operators";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {ScrobbleEventTypePipe} from "../../_single-module/scrobble-event-type.pipe";

import {
  NgbAccordionBody,
  NgbAccordionCollapse,
  NgbAccordionDirective, NgbAccordionHeader,
  NgbAccordionItem
} from "@ng-bootstrap/ng-bootstrap";

@Component({
  selector: 'app-user-holds',
  standalone: true,
  imports: [CommonModule, ScrobbleEventTypePipe, NgbAccordionDirective, NgbAccordionCollapse, NgbAccordionBody, NgbAccordionItem, NgbAccordionHeader],
  templateUrl: './user-holds.component.html',
  styleUrls: ['./user-holds.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UserHoldsComponent {
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly scrobblingService = inject(ScrobblingService);
  private readonly destroyRef = inject(DestroyRef);
  holds$ = this.scrobblingService.getHolds().pipe(takeUntilDestroyed(this.destroyRef), shareReplay());

  constructor() {}
}
