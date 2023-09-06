import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject} from '@angular/core';
import {CommonModule} from '@angular/common';
import {SafeHtmlPipe} from "../../../pipe/safe-html.pipe";
import {TranslocoDirective} from "@ngneat/transloco";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {
  DraggableOrderedListComponent,
  IndexUpdateEvent
} from "../../../reading-list/_components/draggable-ordered-list/draggable-ordered-list.component";
import {
  ReadingListItemComponent
} from "../../../reading-list/_components/reading-list-item/reading-list-item.component";
import {DashboardStream, StreamType} from "../../../dashboard/_components/dashboard.component";
import {AccountService} from "../../../_services/account.service";
import {forkJoin} from "rxjs";
import {FilterService} from "../../../_services/filter.service";
import {StreamListItemComponent} from "../stream-list-item/stream-list-item.component";
import {SmartFilter} from "../../../_models/metadata/v2/smart-filter";

@Component({
  selector: 'app-customize-dashboard-modal',
  standalone: true,
  imports: [CommonModule, SafeHtmlPipe, TranslocoDirective, DraggableOrderedListComponent, ReadingListItemComponent, StreamListItemComponent],
  templateUrl: './customize-dashboard-modal.component.html',
  styleUrls: ['./customize-dashboard-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CustomizeDashboardModalComponent {

  items: DashboardStream[] = [];
  smartFilters: SmartFilter[] = [];
  accessibilityMode: boolean = false;

  private readonly accountService = inject(AccountService);
  private readonly filterService = inject(FilterService);
  private readonly cdRef = inject(ChangeDetectorRef);
  constructor(public modal: NgbActiveModal) {

    forkJoin([this.accountService.getDashboardStreams(), this.filterService.getAllFilters()]).subscribe(results => {
      this.items = results[0];
      const smartFilterStreams = new Set(results[0].filter(d => !d.isProvided).map(d => d.name));
      this.smartFilters = results[1].filter(d => !smartFilterStreams.has(d.name));
      this.cdRef.markForCheck();
    });
  }

  addFilterToStream(filter: SmartFilter) {
    const maxOrder = this.items[this.items.length - 1].order;
    const data = {
          streamType: StreamType.Custom,
          id: 0,
          name: filter.name,
          isProvided: false,
          smartFilterEncoded: filter.filter,
          visible: true,
          order: maxOrder + 1
        } as DashboardStream;
    this.items.push(data);
    this.smartFilters = this.smartFilters.filter(d => d.name !== filter.name);
    this.cdRef.detectChanges();
  }


  orderUpdated(event: IndexUpdateEvent) {
    this.accountService.updateDashboardStreamPosition(event.item.name, event.item.id, event.fromPosition, event.toPosition).subscribe();
  }

  updateVisibility(item: DashboardStream, position: number) {
    this.items[position].visible = !this.items[position].visible;
    this.cdRef.markForCheck();
  }

  close() {
    this.modal.close();
  }

  save() {

    this.close();
  }

}
