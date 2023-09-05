import {Component, inject} from '@angular/core';
import { CommonModule } from '@angular/common';
import {SafeHtmlPipe} from "../../../pipe/safe-html.pipe";
import {TranslocoDirective} from "@ngneat/transloco";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {
  DraggableOrderedListComponent, IndexUpdateEvent
} from "../../../reading-list/_components/draggable-ordered-list/draggable-ordered-list.component";
import {
  ReadingListItemComponent
} from "../../../reading-list/_components/reading-list-item/reading-list-item.component";
import {DashboardStream} from "../../../dashboard/_components/dashboard.component";
import {AccountService} from "../../../_services/account.service";

@Component({
  selector: 'app-customize-dashboard-modal',
  standalone: true,
  imports: [CommonModule, SafeHtmlPipe, TranslocoDirective, DraggableOrderedListComponent, ReadingListItemComponent],
  templateUrl: './customize-dashboard-modal.component.html',
  styleUrls: ['./customize-dashboard-modal.component.scss']
})
export class CustomizeDashboardModalComponent {

  items: DashboardStream[] = [];
  accessibilityMode: boolean = false;

  private readonly accountService = inject(AccountService);
  constructor(public modal: NgbActiveModal) {
    this.accountService.getDashboardStreams().subscribe(items => {
      this.items = items;
    })
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
