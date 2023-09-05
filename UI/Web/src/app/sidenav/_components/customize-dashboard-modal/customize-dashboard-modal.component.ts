import {Component, inject} from '@angular/core';
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

@Component({
  selector: 'app-customize-dashboard-modal',
  standalone: true,
  imports: [CommonModule, SafeHtmlPipe, TranslocoDirective, DraggableOrderedListComponent, ReadingListItemComponent],
  templateUrl: './customize-dashboard-modal.component.html',
  styleUrls: ['./customize-dashboard-modal.component.scss'],
})
export class CustomizeDashboardModalComponent {

  items: DashboardStream[] = [];
  accessibilityMode: boolean = false;

  private readonly accountService = inject(AccountService);
  private readonly filterService = inject(FilterService);
  constructor(public modal: NgbActiveModal) {

    forkJoin([this.accountService.getDashboardStreams(), this.filterService.getAllFilters()]).subscribe(results => {
      this.items = results[0];

      const maxOrder = this.items[this.items.length - 1].order;
      const transformedStreams = results[1].map((filter, i) => {
        return {
          streamType: StreamType.Custom,
          id: 0,
          name: filter.name,
          isProvided: false,
          smartFilterEncoded: filter.filter,
          order: maxOrder + i + 1
        } as DashboardStream
      });
      this.items = this.items.concat(...transformedStreams);
      console.log(transformedStreams)
      console.log(this.items)
    });
  }


  orderUpdated(event: IndexUpdateEvent) {

  }

  close() {
    this.modal.close();
  }

  save() {
    this.close();
  }

}
