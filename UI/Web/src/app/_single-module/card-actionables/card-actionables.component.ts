import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  EventEmitter,
  inject,
  Input,
  OnChanges,
  OnDestroy,
  OnInit,
  Output
} from '@angular/core';
import {NgbDropdown, NgbDropdownItem, NgbDropdownMenu, NgbDropdownToggle, NgbModal} from '@ng-bootstrap/ng-bootstrap';
import {AccountService} from 'src/app/_services/account.service';
import {ActionableEntity, ActionItem} from 'src/app/_services/action-factory.service';
import {AsyncPipe, NgTemplateOutlet} from "@angular/common";
import {TranslocoDirective} from "@jsverse/transloco";
import {DynamicListPipe} from "./_pipes/dynamic-list.pipe";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {Breakpoint, UtilityService} from "../../shared/_services/utility.service";
import {ActionableModalComponent} from "../actionable-modal/actionable-modal.component";
import {User} from "../../_models/user";


@Component({
  selector: 'app-card-actionables',
  imports: [
    NgbDropdown, NgbDropdownToggle, NgbDropdownMenu, NgbDropdownItem,
    DynamicListPipe, TranslocoDirective, AsyncPipe, NgTemplateOutlet
  ],
  templateUrl: './card-actionables.component.html',
  styleUrls: ['./card-actionables.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CardActionablesComponent implements OnInit, OnChanges, OnDestroy {

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly accountService = inject(AccountService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly utilityService = inject(UtilityService);
  protected readonly modalService = inject(NgbModal);

  protected readonly Breakpoint = Breakpoint;

  @Input() iconClass = 'fa-ellipsis-v';
  @Input() btnClass = '';
  @Input() inputActions: ActionItem<any>[] = [];
  @Input() labelBy = 'card';
  /**
   * Text to display as if actionable was a button
   */
  @Input() label = '';
  @Input() disabled: boolean = false;

  @Input() entity: ActionableEntity = null;
  /**
   * This will only emit when the action is clicked and the entity is null. Otherwise, the entity callback handler will be invoked.
   */
  @Output() actionHandler = new EventEmitter<ActionItem<any>>();


  actions: ActionItem<ActionableEntity>[] = [];
  currentUser: User | undefined = undefined;
  submenu: {[key: string]: NgbDropdown} = {};
  private closeTimeout: any = null;


  ngOnInit(): void {
    this.accountService.currentUser$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((user) => {
      if (!user) return;
      this.currentUser = user;
      this.actions = this.inputActions.filter(a => this.willRenderAction(a, user!));
      this.cdRef.markForCheck();
    });
  }

  ngOnChanges() {
    this.actions = this.inputActions.filter(a => this.willRenderAction(a, this.currentUser!));
    this.cdRef.markForCheck();
  }

  ngOnDestroy() {
    this.cancelCloseSubmenus();
  }

  preventEvent(event: any) {
    event.stopPropagation();
    event.preventDefault();
  }

  performAction(event: any, action: ActionItem<ActionableEntity>) {
    this.preventEvent(event);

    if (typeof action.callback === 'function') {
      if (this.entity === null) {
        this.actionHandler.emit(action);
      } else {
        action.callback(action, this.entity);
      }
    }
  }

  /**
   * The user has required roles (or no roles defined) and action shouldRender returns true
   * @param action
   * @param user
   */
  willRenderAction(action: ActionItem<ActionableEntity>, user: User) {
    return (!action.requiredRoles?.length || this.accountService.hasAnyRole(user, action.requiredRoles)) && action.shouldRender(action, this.entity, user);
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
    // Clear any existing timeout to avoid race conditions
    if (this.closeTimeout) {
      clearTimeout(this.closeTimeout);
    }

    // Set a new timeout to close submenus after a short delay
    this.closeTimeout = setTimeout(() => {
      Object.keys(this.submenu).forEach(key => {
        this.submenu[key].close();
        delete this.submenu[key];
      });
    }, 100); // Small delay to prevent premature closing (dropdown tunneling)
  }

  cancelCloseSubmenus() {
    if (this.closeTimeout) {
      clearTimeout(this.closeTimeout);
      this.closeTimeout = null;
    }
  }

  hasRenderableChildren(action: ActionItem<ActionableEntity>, user: User): boolean {
    if (!action.children || action.children.length === 0) return false;

    for (const child of action.children) {
      const dynamicList = child.dynamicList;
      if (dynamicList !== undefined) return true; // Dynamic list gets rendered if loaded

      if (this.willRenderAction(child, user)) return true;
      if (child.children?.length && this.hasRenderableChildren(child, user)) return true;
    }
    return false;
  }

  performDynamicClick(event: any, action: ActionItem<ActionableEntity>, dynamicItem: any) {
    action._extra = dynamicItem;
    this.performAction(event, action);
  }

  openMobileActionableMenu(event: any) {
    this.preventEvent(event);

    const ref = this.modalService.open(ActionableModalComponent, {fullscreen: true, centered: true});
    ref.componentInstance.entity = this.entity;
    ref.componentInstance.actions = this.actions;
    ref.componentInstance.willRenderAction = this.willRenderAction.bind(this);
    ref.componentInstance.shouldRenderSubMenu = this.shouldRenderSubMenu.bind(this);
    ref.componentInstance.actionPerformed.subscribe((action: ActionItem<any>) => {
      this.performAction(event, action);
    });
  }
}
