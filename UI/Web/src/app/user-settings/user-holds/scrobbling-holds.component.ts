import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject} from '@angular/core';
import {AsyncPipe} from '@angular/common';
import {ScrobblingService} from "../../_services/scrobbling.service";
import {shareReplay} from "rxjs/operators";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {ScrobbleEventTypePipe} from "../../_pipes/scrobble-event-type.pipe";

import {
  NgbAccordionBody,
  NgbAccordionCollapse,
  NgbAccordionDirective,
  NgbAccordionHeader,
  NgbAccordionItem
} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {ImageService} from "../../_services/image.service";
import {ImageComponent} from "../../shared/image/image.component";

@Component({
  selector: 'app-user-holds',
  standalone: true,
  imports: [ScrobbleEventTypePipe, NgbAccordionDirective, NgbAccordionCollapse, NgbAccordionBody,
    NgbAccordionItem, NgbAccordionHeader, TranslocoDirective, AsyncPipe, ImageComponent],
  templateUrl: './scrobbling-holds.component.html',
  styleUrls: ['./scrobbling-holds.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ScrobblingHoldsComponent {
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly scrobblingService = inject(ScrobblingService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly imageService = inject(ImageService);
  holds$ = this.scrobblingService.getHolds().pipe(takeUntilDestroyed(this.destroyRef), shareReplay({bufferSize: 1, refCount: true}));
}
