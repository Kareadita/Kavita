import {linkedSignal, WritableSignal} from "@angular/core";
import {FieldTree} from "@angular/forms/signals";

export type FieldLock = WritableSignal<boolean>;
export type LockGroup<K extends string> = Readonly<Record<K, FieldLock>>;

/** Model keys whose entity counterpart `${K}Locked` actually exists */
type LockableKeys<TModel, TEntity> = Extract<keyof TModel, string> &
  {[P in keyof TEntity]: P extends `${infer Base}Locked` ? Base : never}[keyof TEntity];

const NEVER_DIRTY = () => false;

/**
 * A lock that turns itself on the first time the user edits the bound field and can also be set by hand
 * (eg the lock button or a typeahead's [(locked)] binding). Pass a null field for a lock with no input behind it.
 *
 * `initial` is a thunk because these are declared as class fields, which run before @Input()s are set
 */
export function lockFor(field: FieldTree<unknown> | null, initial: () => boolean): FieldLock {
  return linkedSignal<boolean, boolean>({
    source: field ? () => field().dirty() : NEVER_DIRTY,
    computation: (dirty, prev) => dirty || (prev ? prev.value : initial())
  });
}

/**
 * Builds a lock per model key, paired with the entity's `${key}Locked` flag
 */
export function lockGroup<TModel extends object, TEntity,
  K extends LockableKeys<TModel, TEntity>>(
  form: FieldTree<TModel>, entity: () => TEntity, keys: ReadonlyArray<K>): LockGroup<K> {

  const locks = {} as Record<K, FieldLock>;
  for (const key of keys) {
    locks[key] = lockFor((form as never)[key], () => (entity() as never)[`${key}Locked`]);
  }
  return locks;
}

/**
 * Locks with no form field behind them, keyed by the entity's own field name.
 * For typeaheads that are not bound with [formField], and roles like `writers`/`writerLocked` that break the convention
 */
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
