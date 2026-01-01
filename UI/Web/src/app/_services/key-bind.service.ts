import {computed, DestroyRef, inject, Injectable, signal} from '@angular/core';
import {AccountService, Role} from "./account.service";
import {KeyBind, KeyBindTarget} from "../_models/preferences/preferences";
import {DOCUMENT} from "@angular/common";
import {filter, finalize, Observable, of, Subject, tap, withLatestFrom} from "rxjs";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {map} from "rxjs/operators";
import {GamePadService} from "./game-pad.service";

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

  ArrowUp = "arrowup",
  ArrowDown = "arrowdown",
  ArrowLeft = "arrowleft",
  ArrowRight = "arrowright",

  Comma = ',',
  Space = ' ',
  Escape = 'escape',

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
 * Emitted if a keybind has been recorded
 */
export interface KeyBindEvent {
  /**
   * Target of the event
   */
  target: KeyBindTarget;
  /**
   * Overriding this value must be done in the sync callback of your
   * observable. When true after all observables have completed, will cancel the event that triggered it
   *
   * @default true
   */
  triggered: boolean;
  /**
   * If the original event's target was editable. This is only relevant for KeyBoard events, GamePad events do not
   * contain this information
   */
  inEditableElement: boolean;
}

/**
 * Add any keybinds in this array which cannot be used users ever
 * Example: Page refresh
 */
const ReservedKeyBinds: KeyBind[] = [
  {control: true, key: KeyCode.KeyR},
  {meta: true, key: KeyCode.KeyR},
];

/**
 * This record should hold all KeyBinds Kavita has to offer, with their default combination(s).
 * To add a new keybind to the system, add it here and in the backend enum. Add it to the KeyBindGroups
 * array to be displayed on the settings page
 */
export const DefaultKeyBinds: Readonly<Record<KeyBindTarget, KeyBind[]>> = {
  [KeyBindTarget.NavigateToSettings]: [],
  [KeyBindTarget.OpenSearch]: [{control: true, key: KeyCode.KeyK}],
  [KeyBindTarget.NavigateToScrobbling]: [],
  [KeyBindTarget.ToggleFullScreen]: [{key: KeyCode.KeyF}],
  [KeyBindTarget.BookmarkPage]: [{key: KeyCode.KeyB, control: true}],
  [KeyBindTarget.OpenHelp]: [{key: KeyCode.KeyH}],
  [KeyBindTarget.GoTo]: [{key: KeyCode.KeyG}],
  [KeyBindTarget.ToggleMenu]: [{key: KeyCode.Space}],
  [KeyBindTarget.PageLeft]: [{key: KeyCode.ArrowLeft}],
  [KeyBindTarget.PageRight]: [{key: KeyCode.ArrowRight}],
  [KeyBindTarget.Escape]: [{key: KeyCode.Escape}],
  [KeyBindTarget.PageUp]: [{key: KeyCode.ArrowUp}],
  [KeyBindTarget.PageDown]: [{key: KeyCode.ArrowDown}],
  [KeyBindTarget.OffsetDoublePage]: [{key: KeyCode.KeyO}],
} as const;

type KeyBindGroup = {
  title: string,
  elements: {
    target: KeyBindTarget,
    roles?: Role[];
    restrictedRoles?: Role[],
    kavitaPlus?: boolean;
  }[];
}

export const KeyBindGroups: KeyBindGroup[] = [
  {
    title: 'global-header',
    elements: [
      {target: KeyBindTarget.NavigateToSettings},
      {target: KeyBindTarget.OpenSearch},
      {target: KeyBindTarget.NavigateToScrobbling, kavitaPlus: true},
      {target: KeyBindTarget.Escape},
    ]
  },
  {
    title: 'readers-header',
    elements: [
      {target: KeyBindTarget.ToggleFullScreen},
      {target: KeyBindTarget.BookmarkPage},
      {target: KeyBindTarget.OpenHelp},
      {target: KeyBindTarget.GoTo},
      {target: KeyBindTarget.ToggleMenu},
      {target: KeyBindTarget.PageRight},
      {target: KeyBindTarget.PageLeft},
      {target: KeyBindTarget.PageUp},
      {target: KeyBindTarget.PageDown},
      {target: KeyBindTarget.OffsetDoublePage},
    ],
  }
];

interface RegisterListenerOptions {
  /**
   * @default false
   */
  fireInEditable?: boolean;
  /**
   * @default of(true)
   */
  condition$?: Observable<boolean>;
  /**
   * @default true
   */
  markAsTriggered?: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class KeyBindService {

  private readonly accountService = inject(AccountService);
  private readonly gamePadService = inject(GamePadService);
  private readonly document = inject(DOCUMENT);

  /**
   * Global disable switch for the keybind listener. Make sure you enable again after using
   * so keybinds don't stop working across the app.
   */
  public readonly disabled = signal(false);

  /**
   * Valid custom keybinds as configured by the authenticated user
   * @private
   */
  private readonly customKeyBinds = computed(() => {
    const customKeyBinds = this.accountService.currentUserSignal()?.preferences.customKeyBinds ?? {};
    return Object.fromEntries(Object.entries(customKeyBinds).filter(([target, _]) => {
      return DefaultKeyBinds[target as KeyBindTarget] !== undefined; // Filter out unused or old targets
    }))
  });

  /**
   * All key binds for which the target is currently active
   * @private
   */
  private readonly activeKeyBinds = computed<Record<KeyBindTarget, KeyBind[]>>(() => {
    const customKeyBindsRaw =  this.customKeyBinds();
    const activeTargets = this.activeTargetsSet();

    const customKeyBinds: Partial<Record<KeyBindTarget, KeyBind[]>> = {};
    for (const [target, combos] of Object.entries(customKeyBindsRaw) as [KeyBindTarget, KeyBind[]][]) {
      if (activeTargets.has(target)) {
        customKeyBinds[target] = combos.filter(combo => !this.isReservedKeyBind(combo));
      }
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
    const customKeyBinds =  this.customKeyBinds();

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

  private readonly activeTargets = signal<KeyBindTarget[]>([]);
  private readonly activeTargetsSet = computed(() => new Set(this.activeTargets()));

  /**
   * We do not allow subscribing to the events$ directly, as there is some extra state management for performance
   * reasons. See registerListener for details
   * @private
   */
  private readonly eventsSubject = new Subject<KeyBindEvent>();
  private readonly events$ = this.eventsSubject.asObservable();

  constructor() {
    // We use keydown as to intercept before native browser keybinds, in case we want to cancel the event
    this.document.addEventListener('keydown', e => this.handleKeyEvent(e));

    this.gamePadService.keyDownEvents$.pipe(
      map(e => {
        return {
          key: KeyCode.Empty,
          controllerSequence: e.pressedButtons,
        } as KeyBind;
      }),
      tap(kb => this.checkForKeyBind(kb)),
    ).subscribe();
  }

  private handleKeyEvent(event: KeyboardEvent) {
    if (this.disabled()) return;
    if (event.key === undefined) return;

    const eventKey = event.key.toLowerCase() as KeyCode;

    if (!this.listenedKeys().has(eventKey)) return;

    const activeKeyBind: KeyBind = {
      key: eventKey,
      control: event.ctrlKey,
      meta: event.metaKey,
      shift: event.shiftKey,
      alt: event.altKey,
    };

    this.checkForKeyBind(activeKeyBind, event);
  }

  private checkForKeyBind(activeKeyBind: KeyBind, event?: KeyboardEvent) {
    const activeKeyBinds = this.activeKeyBinds();
    for (const [target, keybinds] of Object.entries(activeKeyBinds)) {
      for (const keybind of keybinds) {

        if (!this.areKeyBindsEqual(activeKeyBind, keybind)) continue;

        const keyBindEvent: KeyBindEvent = {
          target: target as KeyBindTarget,
          triggered: false,
          inEditableElement: event ? this.isEditableTarget(event.target) : false,
        };

        this.eventsSubject.next(keyBindEvent);

        if (event && keyBindEvent.triggered) {
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
   * Register a listener for targets. When a match is found will set KeyBindEvent#triggered to true
   * @param destroyRef$ destroy ref used for lifetime management
   * @param callback
   * @param targetFilter
   * @param options
   */
  public registerListener(
    destroyRef$: DestroyRef,
    callback: (e: KeyBindEvent) => void,
    targetFilter: KeyBindTarget[],
    options?: RegisterListenerOptions,
  ) {
    const {
      fireInEditable = false,
      condition$ = of(true),
      markAsTriggered = true,
    } = options ?? {};

    this.activeTargets.update(s => [...s, ...targetFilter]);

    this.events$.pipe(
      takeUntilDestroyed(destroyRef$),
      filter(e => !e.inEditableElement || fireInEditable),
      filter(e => targetFilter.includes(e.target)),
      withLatestFrom(condition$),
      filter(([_, ok]) => ok),
      map(([e, _]) => e),
      tap(e => {
        if (markAsTriggered) {
          e.triggered = true;  // Set before callback so consumers may override
        }

        callback(e);
      }),
      finalize(() => { // Remove all targets when the consumer has finished
        this.activeTargets.update(targets => {
          const updated = [...targets];
          // Remove only once in case others have registered the same target
          targetFilter.forEach(target => this.removeOnce(updated, target));
          return updated;
        });
      }),
    ).subscribe();
  }

  /**
   * Remove the first occurrence of element in the array
   * @param array
   * @param element
   * @private
   */
  private removeOnce<T>(array: T[], element: T) {
    const index = array.indexOf(element);
    if (index !== -1) {
      array.splice(index, 1);
    }
  }

  /**
   * Returns true if the keybinds are semantically equal
   * @param k1
   * @param k2
   */
  public areKeyBindsEqual(k1: KeyBind, k2: KeyBind) {
    // If a controller sequence is present on either, it takes full and the only priority
    if (k1.controllerSequence || k2.controllerSequence) {
      return k1.controllerSequence?.every(k => k2.controllerSequence?.includes(k)) || false;
    }

    return (
      (k1.alt ?? false) === (k2.alt ?? false) &&
      (k1.shift ?? false) === (k2.shift ?? false) &&
      (k1.control ?? false) === (k2.control ?? false) &&
      (k1.meta ?? false) === (k2.meta ?? false) &&
      k1.key === k2.key
    );
  }

  /**
   * Checks the given keybind against the ReservedKeyBinds list. If true, keybind should be considered invalid and unusable
   * @param keyBind
   */
  public isReservedKeyBind(keyBind: KeyBind) {
    for (let reservedKeyBind of ReservedKeyBinds) {
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
    if (!defaultKeyBinds) {
      throw Error("Could not find default keybinds for " + target)
    }

    if (defaultKeyBinds.length !== keyBinds.length) return false;

    return keyBinds.every(keyBind =>
      defaultKeyBinds.some(defaultKeyBind => this.areKeyBindsEqual(defaultKeyBind, keyBind))
    );
  }

}
