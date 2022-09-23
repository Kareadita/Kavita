import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { NgbDropdown } from '@ng-bootstrap/ng-bootstrap';
import { take } from 'rxjs';
import { AccountService } from 'src/app/_services/account.service';
import { Action, ActionItem } from 'src/app/_services/action-factory.service';

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

  isAdmin: boolean = false;
  canDownload: boolean = false;
  submenu: {[key: string]: NgbDropdown} = {};

  constructor(private readonly cdRef: ChangeDetectorRef, private accountService: AccountService) { }

  ngOnInit(): void {
    this.accountService.currentUser$.pipe(take(1)).subscribe((user) => {
      if (!user) return;
      this.isAdmin = this.accountService.hasAdminRole(user);
      this.canDownload = this.accountService.hasDownloadRole(user);
      this.cdRef.markForCheck();
    });
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

  willRenderAction(action: ActionItem<any>): boolean {
    return (action.requiresAdmin && this.isAdmin) 
        || (action.action === Action.Download && (this.canDownload || this.isAdmin))
        || (!action.requiresAdmin && action.action !== Action.Download)
  }

  openSubmenu(actionTitle: string, subMenu: NgbDropdown) {
    // We keep track when we open and when we get a request to open, if we have other keys, we close them and clear their keys
    if (Object.keys(this.submenu).length > 0) {
      const keys = Object.keys(this.submenu).filter(k => k !== actionTitle);
      keys.forEach(key => {
        this.submenu[key].close();
        delete this.submenu[key];
      });
    }
    this.submenu[actionTitle] = subMenu;
    subMenu.open();
  }

}
