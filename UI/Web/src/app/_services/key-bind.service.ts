import {computed, DestroyRef, inject, Injectable} from '@angular/core';
import {AccountService} from "./account.service";
import {KeyBindTarget} from "../_models/preferences/preferences";
import {DOCUMENT} from "@angular/common";
import {filter, ReplaySubject, tap} from "rxjs";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {KEY_CODES} from "../shared/_services/utility.service";

export enum KeyCode {
  KeyA = "KeyA",
  KeyB = "KeyB",
  KeyC = "KeyC",
  KeyD = "KeyD",
  KeyE = "KeyE",
  KeyF = "KeyF",
  KeyG = "KeyG",
  KeyH = "KeyH",
  KeyI = "KeyI",
  KeyJ = "KeyJ",
  KeyK = "KeyK",
  KeyL = "KeyL",
  KeyM = "KeyM",
  KeyN = "KeyN",
  KeyO = "KeyO",
  KeyP = "KeyP",
  KeyQ = "KeyQ",
  KeyR = "KeyR",
  KeyS = "KeyS",
  KeyT = "KeyT",
  KeyU = "KeyU",
  KeyV = "KeyV",
  KeyW = "KeyW",
  KeyX = "KeyX",
  KeyY = "KeyY",
  KeyZ = "KeyZ",

  Digit0 = "Digit0",
  Digit1 = "Digit1",
  Digit2 = "Digit2",
  Digit3 = "Digit3",
  Digit4 = "Digit4",
  Digit5 = "Digit5",
  Digit6 = "Digit6",
  Digit7 = "Digit7",
  Digit8 = "Digit8",
  Digit9 = "Digit9",

  F1 = "F1",
  F2 = "F2",
  F3 = "F3",
  F4 = "F4",
  F5 = "F5",
  F6 = "F6",
  F7 = "F7",
  F8 = "F8",
  F9 = "F9",
  F10 = "F10",
  F11 = "F11",
  F12 = "F12",

  ShiftLeft = "ShiftLeft",
  ShiftRight = "ShiftRight",
  ControlLeft = "ControlLeft",
  ControlRight = "ControlRight",
  AltLeft = "AltLeft",
  AltRight = "AltRight",
  MetaLeft = "MetaLeft",
  MetaRight = "MetaRight",

  ArrowUp = "ArrowUp",
  ArrowDown = "ArrowDown",
  ArrowLeft = "ArrowLeft",
  ArrowRight = "ArrowRight",

  Space = "Space",
  Enter = "Enter",
  Tab = "Tab",
  Backspace = "Backspace",
  Delete = "Delete",
  Escape = "Escape",
  Home = "Home",
  End = "End",
  PageUp = "PageUp",
  PageDown = "PageDown",
  Insert = "Insert",
  CapsLock = "CapsLock",
  ContextMenu = "ContextMenu"
}

export const ModifierKeyCodes: KeyCode[] = [
  KeyCode.ShiftLeft,
  KeyCode.ShiftRight,
  KeyCode.ControlLeft,
  KeyCode.ControlRight,
  KeyCode.AltLeft,
  KeyCode.AltRight,
  KeyCode.MetaLeft,
  KeyCode.MetaRight,
];

export function getReadableComboLabel(combo: string[]): string {
  return combo.map(getReadableKeyLabel).join("+")
}

export function getReadableKeyLabel(code: string): string {
  if (code.startsWith('Key')) {
    return code.slice(3);
  }

  if (code.startsWith('Digit')) {
    return code.slice(5);
  }

  if (code.startsWith("Shift")) {
    return "Shift";
  }

  if (code.startsWith("Control")) {
    return "Ctrl";
  }

  if (code.startsWith("Alt")) {
    return "Alt";
  }

  if (code.startsWith("Meta")) {
    return "Meta ⌘";
  }

  if (code.startsWith("Arrow")) {
    return code.slice(5);
  }

  return code;
}


export interface KeyBindEvent {
  target: KeyBindTarget;
  /**
   * Set triggered to true, if the event is used to trigger a flow
   */
  triggered: boolean;
}

/**
 * Add any combo's in this array which cannot be used by any KeyBinds
 * Example: Page refresh
 */
export const ReservedKeyBinds: string[][] = [
  [KEY_CODES.CONTROL, KeyCode.KeyR],
  [KEY_CODES.META, KeyCode.KeyR]
]

/**
 * This record should hold all KeyBinds Kavita has to offer, with their default combination(s).
 * To add a new keybind to the system, all you have to do it add it here. Event system, and settings page
 * Will update automatically.
 */
export const DefaultKeyBinds: Readonly<Record<KeyBindTarget, string[][]>> = {
  [KeyBindTarget.ToggleSideNav]: [[KeyCode.KeyH]]
} as const;

@Injectable({
  providedIn: 'root'
})
export class KeyBindService {

  private readonly accountService = inject(AccountService);
  private readonly document = inject(DOCUMENT);

  /**
   * All key binds that could be activated
   * @private
   */
  private readonly activeKeyBinds = computed<Record<KeyBindTarget, string[][]>>(() => {
    const customKeyBindsRaw =  this.accountService.currentUserSignal()?.preferences.customKeyBinds ?? {};

    const customKeyBinds: Partial<Record<KeyBindTarget, string[][]>> = {};
    for (const [target, combos] of Object.entries(customKeyBindsRaw) as [KeyBindTarget, string[][]][]) {
      customKeyBinds[target] = combos.filter(combo => !this.isReservedKeyCombo(combo));
    }

    return {
      ...DefaultKeyBinds,
      ...customKeyBinds,
    } satisfies Record<KeyBindTarget, readonly string[][]>;
  });
  /**
   * A record of all possible keybinds in Kavita, as configured by the user
   */
  public readonly allKeyBinds = computed(() => {
    const customKeyBinds =  this.accountService.currentUserSignal()?.preferences.customKeyBinds ?? {};

    return {
      ...DefaultKeyBinds,
      ...customKeyBinds,
    } satisfies Record<KeyBindTarget, readonly string[][]>;
  });

  /**
   * A set of all keys used in all keybinds, other keys should not be tracked
   * @private
   */
  private readonly listenedKeys = computed(() => {
    const keyBinds = this.activeKeyBinds();
    const combos = Object.values(keyBinds);
    const allKeys = combos.flatMap(c => c).flatMap(c => c);
    return new Set(allKeys);
  });

  private readonly eventsSubject = new ReplaySubject<KeyBindEvent>(1);
  /**
   * KeyBindPressEvent events. Subscribe here for full control, otherwise use KeyBindService#registerListener
   */
  public readonly events$ = this.eventsSubject.asObservable();

  constructor() {
    // We use keydown as to intercept before native browser keybinds, in case we want to cancel the event
    this.document.addEventListener('keydown', e => this.handleKeyEvent(e));
  }

  private handleKeyEvent(event: KeyboardEvent) {
    if (!this.listenedKeys().has(event.code)) return;
    if (this.isEditableTarget(event.target)) return;

    const combo = new Set<string>();
    if (event.ctrlKey) combo.add(KEY_CODES.CONTROL);
    if (event.altKey) combo.add(KEY_CODES.ALT);
    if (event.shiftKey) combo.add(KEY_CODES.SHIFT);
    if (event.metaKey) combo.add(KEY_CODES.META);

    combo.add(event.code)
    this.checkCombo(combo, event);
  }

  private checkCombo(activeCombo: Set<string>, e: KeyboardEvent) {
    const keybinds = this.activeKeyBinds();
    if (!activeCombo) return;

    for (const [target, combos] of Object.entries(keybinds)) {
      for (const combo of combos) {
        if (combo.length !== activeCombo.size) continue;

        const allPressed = combo.every(key => activeCombo.has(key));
        if (allPressed) {
          const event = {
            target: target as KeyBindTarget,
            triggered: false,
          };

          this.eventsSubject.next(event);

          if (event.triggered) {
            e.preventDefault();
            e.stopPropagation();
          }
        }
      }
    }
  }

  private isEditableTarget(target: EventTarget | null): boolean {
    if (!(target instanceof HTMLElement)) return false;

    if (target instanceof HTMLInputElement) return true;
    if (target instanceof HTMLTextAreaElement) return true;

    return target.isContentEditable;
  }

  /**
   * QOL method to register a listener for targets. When a match is found will set KeyBindEvent#triggered to true
   * @param destroyRef$
   * @param callback
   * @param targetFilter
   */
  public registerListener(destroyRef$: DestroyRef, callback: (e: KeyBindEvent) => void, targetFilter?: KeyBindTarget[]) {
    this.events$.pipe(
      takeUntilDestroyed(destroyRef$),
      filter(e => !targetFilter || targetFilter.includes(e.target)),
      tap(e => {
        e.triggered = true;
        callback(e);
      })
    ).subscribe();
  }

  public isReservedKeyCombo(combo: string[]) {
    for (let reservedKeyBind of ReservedKeyBinds) {
      if (combo.length !== reservedKeyBind.length) continue;

      const allMatch = combo.every((key => reservedKeyBind.includes(key)));
      if (allMatch) return true;
    }

    return false;
  }

}
