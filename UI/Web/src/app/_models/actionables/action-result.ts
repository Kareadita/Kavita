import {Action} from "./action";
import {ActionItem} from "./action-item";
import {Observable} from "rxjs";

export type ActionResultCallback<T> = (action: ActionItem<T>, entity: T) => Observable<ActionResult<T>>;

export type ActionEffect = 'update' | 'remove' | 'reload' | 'none';
export interface ActionResult<T> {
  action: Action;
  entity: T;
  effect: ActionEffect;
}
