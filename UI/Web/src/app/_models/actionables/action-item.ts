import {Observable} from "rxjs";
import {Action} from "./action";
import {User} from "../user/user";
import {Role} from "../../_services/account.service";
import {ActionResultCallback} from "./action-result";

/**
 * Callback for an action
 */
export type ActionCallback<T> = (action: ActionItem<T>, entity: T) => void;
export type ActionShouldRenderFunc<T> = (action: ActionItem<T>, entity: T, user: User) => boolean;

export interface ActionItem<T> {
  title: string;
  description: string;
  action: Action;
  callback: ActionResultCallback<T>;
  /**
   * Roles required to be present for ActionItem to show. If empty, assumes anyone can see. At least one needs to apply.
   */
  requiredRoles: Role[];
  children: Array<ActionItem<T>>;
  /**
   * An optional class which applies to an item. ie) danger on a delete action
   */
  class?: string;
  /**
   * Indicates that there exists a separate list will be loaded from an API.
   * Rule: If using this, only one child should exist in children with the Action for dynamicList.
   */
  dynamicList?: Observable<{title: string, data: any}[]> | undefined;
  /**
   * Extra data that needs to be sent back from the card item. Used mainly for dynamicList. This will be the item from dynamicList return
   */
  _extra?: {title: string, data: any};
  /**
   * Will call on each action to determine if it should show for the appropriate entity based on state and user
   */
  shouldRender: ActionShouldRenderFunc<T>;
}
