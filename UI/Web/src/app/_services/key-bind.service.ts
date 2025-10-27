import {computed, DestroyRef, inject, Injectable} from '@angular/core';
import {AccountService} from "./account.service";
import {KeyBind, KeyBindTarget} from "../_models/preferences/preferences";
import {DOCUMENT} from "@angular/common";
import {filter, ReplaySubject, tap} from "rxjs";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";

/**
 * Codes as returned by KeyBoardEvent.key.toLowerCase()
 */
export enum KeyCode {
  KeyA = "a",
  KeyB = "b",
  KeyC = "c",
  KeyD = "d",
  KeyE = "e",
  KeyF = "f",
  KeyG = "g",
  KeyH = "h",
  KeyI = "i",
  KeyJ = "j",
  KeyK = "k",
  KeyL = "l",
  KeyM = "m",
  KeyN = "n",
  KeyO = "o",
  KeyP = "p",
  KeyQ = "q",
  KeyR = "r",
  KeyS = "s",
  KeyT = "t",
  KeyU = "u",
  KeyV = "v",
  KeyW = "w",
  KeyX = "x",
  KeyY = "y",
  KeyZ = "z",


  Digit0 = "0",
  Digit1 = "1",
  Digit2 = "2",
  Digit3 = "3",
  Digit4 = "4",
  Digit5 = "5",
  Digit6 = "6",
  Digit7 = "7",
  Digit8 = "8",
  Digit9 = "9",

  ArrowUp = "ArrowUp",
  ArrowDown = "ArrowDown",
  ArrowLeft = "ArrowLeft",
  ArrowRight = "ArrowRight",

  // These are not real codes, but ones we map. As we do not want to make
  // a distinction between ShiftLeft and ShiftRight
  Control = "control",
  Alt = "alt",
  Shift = "shift",
  Meta = "meta",

  Empty = '',
}

/**
 * KeyCodes we consider modifiers
 */
export const ModifierKeyCodes: KeyCode[] = [
  KeyCode.Control,
  KeyCode.Alt,
  KeyCode.Shift,
  KeyCode.Meta,
];

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
const ReservedKeyBinds: KeyBind[] = [
  {control: true, key: KeyCode.KeyR},
  {meta: true, key: KeyCode.KeyR},
]

/**
 * This record should hold all KeyBinds Kavita has to offer, with their default combination(s).
 * To add a new keybind to the system, all you have to do it add it here. Event system, and settings page
 * Will update automatically.
 */
export const DefaultKeyBinds: Readonly<Record<KeyBindTarget, KeyBind[]>> = {
  [KeyBindTarget.ToggleSideNav]: [{key: KeyCode.KeyH}]
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
  private readonly activeKeyBinds = computed<Record<KeyBindTarget, KeyBind[]>>(() => {
    const customKeyBindsRaw =  this.accountService.currentUserSignal()?.preferences.customKeyBinds ?? {};

    const customKeyBinds: Partial<Record<KeyBindTarget, KeyBind[]>> = {};
    for (const [target, combos] of Object.entries(customKeyBindsRaw) as [KeyBindTarget, KeyBind[]][]) {
      customKeyBinds[target] = combos.filter(combo => !this.isReservedKeyBind(combo));
    }

    return {
      ...DefaultKeyBinds,
      ...customKeyBinds,
    } satisfies Record<KeyBindTarget, readonly KeyBind[]>;
  });

  /**
   * A record of all possible keybinds in Kavita, as configured by the user
   */
  public readonly allKeyBinds = computed<Record<KeyBindTarget, KeyBind[]>>(() => {
    const customKeyBinds =  this.accountService.currentUserSignal()?.preferences.customKeyBinds ?? {};

    return {
      ...DefaultKeyBinds,
      ...customKeyBinds,
    } satisfies Record<KeyBindTarget, readonly KeyBind[]>;
  });

  /**
   * A set of all keys used in all keybinds, other keys should not be tracked
   * @private
   */
  private readonly listenedKeys = computed(() => {
    const keyBinds = this.activeKeyBinds();
    const combos = Object.values(keyBinds);
    const allKeys = combos.flatMap(c => c).flatMap(c => c).map(kb => kb.key);
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
    const eventKey = event.key.toLowerCase() as KeyCode;

    if (!this.listenedKeys().has(eventKey)) return;
    if (this.isEditableTarget(event.target)) return;

    const activeKeyBinds = this.activeKeyBinds();
    for (const [target, keybinds] of Object.entries(activeKeyBinds)) {
      for (const keybind of keybinds) {

        if (event.altKey !== keybind.alt) continue;
        if (event.metaKey !== keybind.meta) continue;
        if (event.ctrlKey !== keybind.control) continue;
        if (event.shiftKey !== keybind.shift) continue;
        if (eventKey !== keybind.key) continue;

        const keyBindEvent = {
          target: target as KeyBindTarget,
          triggered: false,
        };

        this.eventsSubject.next(keyBindEvent);

        if (keyBindEvent.triggered) {
          event.preventDefault();
          event.stopPropagation();
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

  public areKeyBindsEqual(k1: KeyBind, k2: KeyBind) {
    return (
      (k1.alt ?? false) === (k2.alt ?? false) &&
      (k1.shift ?? false) === (k2.shift ?? false) &&
      (k1.control ?? false) === (k2.control ?? false) &&
      (k1.meta ?? false) === (k2.meta ?? false) &&
      k1.key === k2.key
    );
  }


  /**
   * Checks the given combo against the ReservedKeyBinds list. If true, combo should be considered invalid and unusable
   * @param keyBind
   */
  public isReservedKeyBind(keyBind: KeyBind) {
    for (let reservedKeyBind of ReservedKeyBinds) {
      console.log(reservedKeyBind, keyBind, this.areKeyBindsEqual(reservedKeyBind, keyBind))
      if (this.areKeyBindsEqual(reservedKeyBind, keyBind)) {
        return true;
      }
    }

    return false;
  }

  /**
   * Returns true if the given keyBinds are equal to the default ones for the target, and can be skipped when saving to user preferences
   * @param target
   * @param keyBinds
   */
  public isDefaultKeyBinds(target: KeyBindTarget, keyBinds: KeyBind[]) {
    const defaultKeyBinds = DefaultKeyBinds[target];

    if (defaultKeyBinds.length !== keyBinds.length) return false;

    for (let keyBind of keyBinds) {
      if (defaultKeyBinds.some(k => this.areKeyBindsEqual(k, keyBind))) {
        return true;
      }
    }

    return false;
  }

}
