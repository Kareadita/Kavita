import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  OnDestroy,
  output
} from '@angular/core';
import {NgbDropdown, NgbDropdownItem, NgbDropdownMenu, NgbDropdownToggle} from '@ng-bootstrap/ng-bootstrap';
import {AccountService} from 'src/app/_services/account.service';
import {ActionableEntity} from 'src/app/_services/action-factory.service';
import {AsyncPipe, NgClass, NgTemplateOutlet} from "@angular/common";
import {TranslocoDirective} from "@jsverse/transloco";
import {DynamicListPipe} from "./_pipes/dynamic-list.pipe";
import {ActionableModalComponent} from "./_modals/actionable-modal/actionable-modal.component";
import {User} from "../../_models/user/user";
import {BreakpointService} from "../../_services/breakpoint.service";
import {ActionItem} from "../../_models/actionables/action-item";
import {ActionResult} from "../../_models/actionables/action-result";
import {filterActionTree} from "../../../libs/action-utils";
import {ModalService} from "../../_services/modal.service";


@Component({
  selector: 'app-card-actionables',
  imports: [
    NgbDropdown, NgbDropdownToggle, NgbDropdownMenu, NgbDropdownItem,
    DynamicListPipe, TranslocoDirective, AsyncPipe, NgTemplateOutlet, NgClass
  ],
  templateUrl: './card-actionables.component.html',
  styleUrls: ['./card-actionables.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CardActionablesComponent implements OnDestroy {

  private readonly accountService = inject(AccountService);
  protected readonly modalService = inject(ModalService);
  protected readonly breakpointService = inject(BreakpointService);

  iconClass = input<string>('fa-ellipsis-v');
  btnClass = input<string>('');
  actions = input<ActionItem<any>[]>([]);
  labelBy = input<string>('card');
  /**
   * Text to display as if actionable was a button
   */
  label = input<string>('');
  disabled = input<boolean>(false);
  /**
   * Hide label when on mobile
   */
  hideLabelOnMobile = input<boolean>(false);

  entity = input<ActionableEntity>(null);
  /**
   * This will only emit when the action is clicked and the entity is null. Otherwise, the entity callback handler will be invoked.
   */
  readonly actionHandler = output<ActionResult<any>>();

  filteredActions = computed(() => {
    const entity = this.entity();
    const user = this.accountService.currentUser();
    const actions = this.actions();
    if (!user || !actions.length) return [];

    return filterActionTree(actions, entity, user, this.accountService);
  });

  currentUser = this.accountService.currentUser;
  submenu: {[key: string]: NgbDropdown} = {};
  private closeTimeout: any = null;

  ngOnDestroy() {
    this.cancelCloseSubmenus();
  }

  preventEvent(event: any) {
    event.stopPropagation();
    event.preventDefault();
  }

  performAction(event: any, action: ActionItem<ActionableEntity>) {
    this.preventEvent(event);

    action.callback(action, this.entity()).subscribe(actionResult => {
      this.actionHandler.emit(actionResult);
    });
  }

  /**
   * The user has required roles (or no roles defined) and action shouldRender returns true
   * @param action
   * @param user
   */
  willRenderAction(action: ActionItem<ActionableEntity>, user: User) {
    const hasValidRole = !action.requiredRoles?.length || this.accountService.hasAnyRole(user, action.requiredRoles);
    const shouldRenderFuncPasses = action.shouldRender(action, this.entity(), user);
    //console.log('Action: ', action, 'has valid role: ', hasValidRole, ' and should render func passes: ', shouldRenderFuncPasses);

    return hasValidRole && shouldRenderFuncPasses;
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

  performDynamicClick(event: any, action: ActionItem<ActionableEntity>, dynamicItem: any) {
    action._extra = dynamicItem;
    this.performAction(event, action);
  }

  openMobileActionableMenu(event: any) {
    this.preventEvent(event);

    // TODO: See if we can use a drawer instead
    const ref = this.modalService.open(ActionableModalComponent);
    ref.setInput('entity', this.entity());
    ref.setInput('filteredActions', this.filteredActions());

    ref.componentInstance.actionPerformed.subscribe((actionOrResult: any) => {
      this.actionHandler.emit(actionOrResult);
    });
  }
}
