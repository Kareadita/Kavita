import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import {NgbDropdown, NgbDropdownItem, NgbDropdownMenu, NgbDropdownToggle} from '@ng-bootstrap/ng-bootstrap';
import { take } from 'rxjs';
import { AccountService } from 'src/app/_services/account.service';
import { Action, ActionItem } from 'src/app/_services/action-factory.service';
import {CommonModule} from "@angular/common";
import {TranslocoDirective} from "@ngneat/transloco";
import {DynamicListPipe} from "./_pipes/dynamic-list.pipe";

@Component({
  selector: 'app-card-actionables',
  standalone: true,
  imports: [CommonModule, NgbDropdown, NgbDropdownToggle, NgbDropdownMenu, NgbDropdownItem, DynamicListPipe, TranslocoDirective],
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

      // We want to avoid an empty menu when user doesn't have access to anything
      if (!this.isAdmin && this.actions.filter(a => !a.requiresAdmin).length === 0) {
        this.actions = [];
      }
      this.cdRef.markForCheck();
    });
  }

  preventEvent(event: any) {
    event.stopPropagation();
    event.preventDefault();
  }

  performAction(event: any, action: ActionItem<any>) {
    this.preventEvent(event);

    if (typeof action.callback === 'function') {
      this.actionHandler.emit(action);
    }
  }

  willRenderAction(action: ActionItem<any>) {
    return (action.requiresAdmin && this.isAdmin)
        || (action.action === Action.Download && (this.canDownload || this.isAdmin))
        || (!action.requiresAdmin && action.action !== Action.Download);
  }

  shouldRenderSubMenu(action: ActionItem<any>, dynamicList: null | Array<any>) {
    return (action.children[0].dynamicList === undefined || action.children[0].dynamicList === null) || (dynamicList !== null && dynamicList.length > 0);
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

  closeAllSubmenus() {
    Object.keys(this.submenu).forEach(key => {
      this.submenu[key].close();
        delete this.submenu[key];
    });
  }

  performDynamicClick(event: any, action: ActionItem<any>, dynamicItem: any) {
    action._extra = dynamicItem;
    this.performAction(event, action);
  }
}
