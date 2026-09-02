import {AbstractControl} from "@angular/forms";
import {computed, DestroyRef, Signal} from "@angular/core";
import {FieldTree, isFieldTree} from "@angular/forms/signals";
import {takeUntilDestroyed, toObservable, toSignal} from "@angular/core/rxjs-interop";
import {startWith, switchMap} from "rxjs";

/** This is all to allow the validation components to work on both AbstractControls and signal-forms. */

export type AnyField = AbstractControl | FieldTree<unknown>;

export interface FieldError {
  kind: string;
  message?: string;
  params: Record<string, unknown>;
}

/** A common interface for form validation that allows for both AbstractControl and Signal-based controls */
export interface FieldView {
  errors: Signal<FieldError[]>;
  touched: Signal<boolean>;
  dirty: Signal<boolean>;
  invalid: Signal<boolean>;
}

/** signal forms use camelCase and localization doesn't, so we provide a mapping */
const KIND_ALIASES: Record<string, string> = {
  minLength: 'minlength',
  maxLength: 'maxlength'
}

/** Maps signal form error subclasses to localization params (reactive form-based) */
function signalParams(e: any): Record<string, unknown> {
  switch (e.kind) {
    case 'minLength': return {requiredLength: e.minLength};
    case 'maxLength': return {requiredLength: e.maxLength};
    case 'min': return {min: e.min};
    case 'max': return {max: e.max};
    case 'pattern': return {pattern: String(e.pattern)};
    default: return {};
  }
}


export function toFieldView(source: Signal<AnyField>, destroyRef: DestroyRef): FieldView {
  const tree = computed(() => {
    const s = source();
    return isFieldTree(s) ? s : null;
  });
  const state = () => tree()!();

  // reactive path only, empty for signal fields
  const events = toSignal(
    toObservable(source).pipe(
      switchMap(s => isFieldTree(s) ? [] : (s as AbstractControl).events.pipe(startWith(null))),
      takeUntilDestroyed(destroyRef)
    )
  );

  function fromControl<T>(read: (c: AbstractControl) => T): Signal<T> {
    return computed(() => {
      events();
      return read(source() as AbstractControl);
    });
  }


  return {
    errors: computed<FieldError[]>(() => {
      if (tree()) {
        return state().errors().map(e => {
          return {
            kind: KIND_ALIASES[e.kind] ?? e.kind,
            message: e.message,
            params: signalParams(e)
          }
        });
      }
      events();

      const errs = (source() as AbstractControl).errors;
      if (!errs) return [];

      return Object.keys(errs).map(kind => ({
        kind,
        params: typeof errs[kind] === 'object' && errs[kind] !== null ? errs[kind] : {}
      }));
    }),
    touched: computed(() => {
      return tree() ? state().touched() : fromControl(c => c.touched)();
    }),
    dirty: computed(() => {
      return tree() ? state().dirty() : fromControl(c => c.dirty)();
    }),
    invalid: computed(() => {
      return tree() ? state().invalid() : fromControl(c => c.invalid)();
    }),
  }

}
