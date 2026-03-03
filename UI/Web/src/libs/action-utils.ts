import {ActionItem} from "../app/_models/actionables/action-item";
import {User} from "../app/_models/user/user";
import {AccountService} from "../app/_services/account.service";

/**
 * Determines if a single action should render for the given user.
 * Pure function — no side effects.
 */
export function willRenderAction<T>(
  action: ActionItem<T>,
  entity: T,
  user: User,
  accountService: AccountService
): boolean {
  const hasValidRole = !action.requiredRoles?.length || accountService.hasAnyRole(user, action.requiredRoles);
  return hasValidRole && action.shouldRender(action, entity, user);
}

/**
 * Recursively filters an action tree, removing actions the user cannot see.
 * Submenu parents with no visible children are also removed.
 */
export function filterActionTree<T>(
  actions: ActionItem<T>[],
  entity: T,
  user: User,
  accountService: AccountService
): ActionItem<T>[] {
  return actions.reduce<ActionItem<T>[]>((acc, action) => {
    // Submenu parent (has static children, no dynamicList)
    if (action.children?.length > 0 && !action.dynamicList) {
      const filteredChildren = filterActionTree(action.children, entity, user, accountService);
      if (filteredChildren.length > 0) {
        acc.push({...action, children: filteredChildren});
      }
      return acc;
    }

    // Leaf action
    if (willRenderAction(action, entity, user, accountService)) {
      acc.push(action);
    }

    return acc;
  }, []);
}

/**
 * Checks whether a submenu should render based on its dynamic list state.
 */
export function shouldRenderSubMenu(action: ActionItem<any>, dynamicList: null | Array<any>): boolean {
  const firstChild = action.children?.[0];
  return (firstChild?.dynamicList == null) || (dynamicList != null && dynamicList.length > 0);
}
