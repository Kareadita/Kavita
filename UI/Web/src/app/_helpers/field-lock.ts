import {linkedSignal, WritableSignal} from "@angular/core";
import {FieldTree} from "@angular/forms/signals";

export type FieldLock = WritableSignal<boolean>;
export type LockGroup<K extends string> = Readonly<Record<K, FieldLock>>;

/** Model keys whose entity counterpart `${K}Locked` actually exists */
type LockableKeys<TModel, TEntity> = Extract<keyof TModel, string> &
  {[P in keyof TEntity]: P extends `${infer Base}Locked` ? Base : never}[keyof TEntity];

/**
 * A lock that turns itself on the first time the user edits the bound field and can also be set by hand (eg lock button or typeahead's [(locked)] binding)
 * @param field
 * @param initial
 */
export function lockFor(field: FieldTree<unknown> | null, initial: () => boolean): FieldLock {
  let baseline: unknown;

  // Compares values instead of reading dirty(): Angular marks date/time inputs dirty on first paint
  return linkedSignal<unknown, boolean>({
    source: () => field === null ? null : field().value(),
    computation: (value, prev) => {
      if (!prev) {
        baseline = value;
        return initial();
      }
      return value !== baseline || prev.value;
    }
  });
}

/** Builds a lock per model key, paired with the entity's `${key}Locked` flag */
export function lockGroup<TModel extends object, TEntity,
  K extends LockableKeys<TModel, TEntity>>(
  form: FieldTree<TModel>, entity: () => TEntity, keys: ReadonlyArray<K>): LockGroup<K> {

  const locks = {} as Record<K, FieldLock>;
  for (const key of keys) {
    locks[key] = lockFor((form as never)[key], () => (entity() as never)[`${key}Locked`]);
  }
  return locks;
}

/** Locks with no field behind them, keyed by the entity's own field name, for typeaheads that are not bound with [formField] */
export function standaloneLocks<TEntity, K extends Extract<keyof TEntity, string>>(
  entity: () => TEntity, keys: ReadonlyArray<K>): LockGroup<K> {

  const locks = {} as Record<K, FieldLock>;
  for (const key of keys) {
    locks[key] = lockFor(null, () => (entity() as never)[key]);
  }
  return locks;
}

/** Model keys map to entity keys by the `${key}Locked` convention */
export function writeFieldLocks<T extends object>(target: T, locks: LockGroup<string>): void {
  for (const [key, lock] of Object.entries(locks)) {
    (target as Record<string, unknown>)[`${key}Locked`] = lock();
  }
}

/** For locks already keyed by their entity field name */
export function writeNamedLocks<T extends object>(target: T, locks: LockGroup<string>): void {
  for (const [key, lock] of Object.entries(locks)) {
    (target as Record<string, unknown>)[key] = lock();
  }
}
