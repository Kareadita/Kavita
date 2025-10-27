import {computed, DestroyRef, inject, Injectable} from '@angular/core';
import {AccountService} from "./account.service";
import {KeyBindTarget} from "../_models/preferences/preferences";
import {DOCUMENT} from "@angular/common";
import {filter, ReplaySubject, tap} from "rxjs";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";

/**
 * Codes as returned by KeyBoardEvent.code
 */
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
  ContextMenu = "ContextMenu",

  // These are not real codes, but ones we map. As we do not want to make
  // a distinction between ShiftLeft and ShiftRight
  Control = "Control",
  Alt = "Alt",
  Shift = "Shift",
  Meta = "Meta",
}

export type KeyCombo = KeyCode[];

/**
 * KeyCodes we consider modifiers
 */
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


/**
 * Returns a more human-readable string for the give key
 * @param code
 */
function getReadableKeyLabel(code: KeyCode): string {
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

/**
 * Emitted if a combo has been recorded
 */
export interface KeyBindEvent {
  /**
   * Target of the event
   */
  target: KeyBindTarget;
  /**
   * Set triggered to true, if the event is used to trigger a flow. This must be done in the sync callback of your
   * observable. When true after all observables have completed, will cancel the event that triggered it
   */
  triggered: boolean;
}

/**
 * Add any combo's in this array which cannot be used by any KeyBinds
 * Example: Page refresh
 */
const ReservedKeyBinds: KeyCombo[] = [
  [KeyCode.Control, KeyCode.KeyR],
  [KeyCode.Meta, KeyCode.KeyR]
]

/**
 * This record should hold all KeyBinds Kavita has to offer, with their default combination(s).
 * To add a new keybind to the system, all you have to do it add it here. Event system, and settings page
 * Will update automatically.
 */
export const DefaultKeyBinds: Readonly<Record<KeyBindTarget, KeyCombo[]>> = {
  [KeyBindTarget.ToggleSideNav]: [[KeyCode.KeyH]]
} as const;

export const AllKeyBindTargets: KeyBindTarget[] = Object.keys(KeyBindTarget) as KeyBindTarget[];

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
  private readonly activeKeyBinds = computed<Record<KeyBindTarget, KeyCombo[]>>(() => {
    const customKeyBindsRaw =  this.accountService.currentUserSignal()?.preferences.customKeyBinds ?? {};

    const customKeyBinds: Partial<Record<KeyBindTarget, KeyCombo[]>> = {};
    for (const [target, combos] of Object.entries(customKeyBindsRaw) as [KeyBindTarget, KeyCombo[]][]) {
      customKeyBinds[target] = combos.filter(combo => !this.isReservedKeyCombo(combo));
    }

    return {
      ...DefaultKeyBinds,
      ...customKeyBinds,
    } satisfies Record<KeyBindTarget, readonly KeyCombo[]>;
  });

  /**
   * A record of all possible keybinds in Kavita, as configured by the user
   */
  public readonly allKeyBinds = computed<Record<KeyBindTarget, KeyCombo[]>>(() => {
    const customKeyBinds =  this.accountService.currentUserSignal()?.preferences.customKeyBinds ?? {};

    return {
      ...DefaultKeyBinds,
      ...customKeyBinds,
    } satisfies Record<KeyBindTarget, readonly KeyCombo[]>;
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

  /**
   * Returns a more human-readable string for the given combo
   * @param combo
   */
  public getReadableComboLabel(combo: KeyCombo): string {
    return combo.map(getReadableKeyLabel).join("+")
  }

  private handleKeyEvent(event: KeyboardEvent) {
    if (!this.listenedKeys().has(event.code as KeyCode)) return;
    if (this.isEditableTarget(event.target)) return;

    const combo = new Set<KeyCode>();
    if (event.ctrlKey) combo.add(KeyCode.Control);
    if (event.altKey) combo.add(KeyCode.Alt);
    if (event.shiftKey) combo.add(KeyCode.Shift);
    if (event.metaKey) combo.add(KeyCode.Meta);

    combo.add(event.code as KeyCode)
    this.checkCombo(combo, event);
  }

  private checkCombo(activeCombo: Set<KeyCode>, e: KeyboardEvent) {
    const keybinds = this.activeKeyBinds();
    if (!activeCombo) return;

    for (const [target, combos] of Object.entries(keybinds)) {
      for (const combo of combos) {
        if (combo.length !== activeCombo.size) continue;

        const allPressed = combo.every(key => activeCombo.has(key));
        if (!allPressed) continue

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

  /**
   * Key events while in this target should be ignored
   * @param target
   * @private
   */
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

  /**
   * Checks the given combo against the ReservedKeyBinds list. If true, combo should be considered invalid and unusable
   * @param combo
   */
  public isReservedKeyCombo(combo: KeyCombo) {
    for (let reservedKeyBind of ReservedKeyBinds) {
      if (combo.length !== reservedKeyBind.length) continue;

      const allMatch = combo.every((key => reservedKeyBind.includes(key)));
      if (allMatch) return true;
    }

    return false;
  }

  /**
   * Returns true if the given combos are equal to the default ones, and can be skipped when saving to user preferences
   * @param target
   * @param combos
   */
  public isDefaultKeyBinds(target: KeyBindTarget, combos: KeyCombo[]) {
    const defaultCombos = DefaultKeyBinds[target];
    if (defaultCombos.length !== combos.length) return false;

    for (let combo of combos) {
      let foundMatch = false;

      for (let defaultCombo of defaultCombos) {
        if (defaultCombo.length !== combo.length) continue;

        if (combo.every(k => defaultCombo.includes(k))) {
          foundMatch = true;
          break;
        }
      }

      if (!foundMatch) return false;
    }

    return true;
  }

}
