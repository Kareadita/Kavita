import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { take } from 'rxjs';
import { AccountService } from 'src/app/_services/account.service';
import { ActionItem } from 'src/app/_services/action-factory.service';

@Component({
  selector: 'app-card-actionables',
  templateUrl: './card-actionables.component.html',
  styleUrls: ['./card-actionables.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CardActionablesComponent implements OnInit {

  @Input() iconClass = 'fa-ellipsis-v';
  @Input() btnClass = '';
  @Input() actions: ActionItem<any>[] = [];
  @Input() labelBy = 'card';
  @Input() disabled: boolean = false;
  @Output() actionHandler = new EventEmitter<ActionItem<any>>();

  adminActions: ActionItem<any>[] = [];
  nonAdminActions: ActionItem<any>[] = [];

  isAdmin: boolean = false;
  canDownload: boolean = false;

  constructor(private readonly cdRef: ChangeDetectorRef, private accountService: AccountService) { }

  ngOnInit(): void {
    this.nonAdminActions = this.actions.filter(item => !item.requiresAdmin);
    this.adminActions = this.actions.filter(item => item.requiresAdmin);

    this.accountService.currentUser$.pipe(take(1)).subscribe((user) => {
      if (!user) return;
      this.isAdmin = this.accountService.hasAdminRole(user);
      this.canDownload = this.accountService.hasDownloadRole(user);
    });

    this.cdRef.markForCheck();
  }

  preventClick(event: any) {
    event.stopPropagation();
    event.preventDefault();
  }

  performAction(event: any, action: ActionItem<any>) {
    this.preventClick(event);

    if (typeof action.callback === 'function') {
      this.actionHandler.emit(action);
    }
  }

  getNonAdminActions(actionsList: ActionItem<any>[]): ActionItem<any>[] {
    return actionsList.filter(item => !item.requiresAdmin);
  }

  getAdminActions(actionsList: ActionItem<any>[]): ActionItem<any>[] {
    return actionsList.filter(item => item.requiresAdmin);
  }

  getDivider(actionsList: ActionItem<any>[]): Boolean {
    return actionsList.filter(item => !item.requiresAdmin).length > 0 && actionsList.filter(item => item.requiresAdmin).length > 0;
  }

}
