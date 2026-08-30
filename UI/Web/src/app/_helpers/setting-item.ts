import {afterRenderEffect, isDevMode, signal, Signal} from "@angular/core";
import {idPostfix} from "../shared/_components/validation-errors/validation-errors.component";

export interface SettingControlOptions {
  /** Element the projected value content renders into */
  scope: Signal<HTMLElement | null>;
  /** Id to stamp onto the control */
  elementId: Signal<string>;
  /** True when the host renders its own app-validation-errors for this control */
  describeValidation?: Signal<boolean>;
  /** Used in the multi-control dev warning */
  label?: Signal<string>;
}

/**
 * Sets up the id generation and
 * @param opts
 */
export function wireSettingControl(opts: SettingControlOptions): {hasControl: Signal<boolean>} {
  const hasControl = signal(false);

  afterRenderEffect(() => {
    const scope = opts.scope();
    if (!scope) return;

    const control = scope.querySelector<HTMLElement>('input:not([type=hidden]), select, textarea');
    hasControl.set(control !== null);
    if (!control) return;

    const elementId = opts.elementId();
    if (control.id !== elementId) {
      control.id = elementId;
    }

    if (opts.describeValidation?.()) {
      addDescribedBy(control, `${elementId}${idPostfix}`);
    }

    if (isDevMode()) {
      warnOnMultipleControls(scope, opts.label?.() ?? elementId);
    }
  });

  return {hasControl: hasControl.asReadonly()};
}

function addDescribedBy(element: HTMLElement, token: string) {
  const tokens = (element.getAttribute('aria-describedby') || '').split(/\s+/)
    .filter(t => t.length > 0);
  if (tokens.includes(token)) return;

  tokens.push(token);
  element.setAttribute('aria-describedby', tokens.join(' '));
}

function warnOnMultipleControls(scope: HTMLElement, title: string) {
  const bound = scope.querySelectorAll('[formControlName]');
  if (bound.length <= 1) return;

  console.warn(`[app-setting-item] "${title}" projects ${bound.length} form controls. `
    + `Only the first is wired to the label and aria-describedby, wire the others up manually.`);
}
