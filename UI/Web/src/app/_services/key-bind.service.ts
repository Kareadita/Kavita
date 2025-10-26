import {computed, DestroyRef, effect, inject, Injectable, signal} from '@angular/core';
import {AccountService} from "./account.service";
import {KeyBindTarget} from "../_models/preferences/preferences";
import {DOCUMENT} from "@angular/common";
import {ReplaySubject} from "rxjs";
import {toSignal} from "@angular/core/rxjs-interop";
import {KEY_CODES} from "../shared/_services/utility.service";


export interface KeyBindEvent {
  target: KeyBindTarget;
}

export const DefaultKeyBinds: Readonly<Record<KeyBindTarget, string[]>> = {
  [KeyBindTarget.ToggleSideNav]: [KEY_CODES.H]
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
  private readonly activeKeyBinds = computed<Record<KeyBindTarget, string[]>>(() => {
    const customKeyBinds =  this.accountService.currentUserSignal()?.preferences.customKeyBinds ?? {};
    return {
      ...DefaultKeyBinds,
      ...customKeyBinds,
    } satisfies Record<KeyBindTarget, readonly string[]>;
  });
  /**
   * A record of all possible keybinds in Kavita
   */
  public readonly allKeyBinds = this.activeKeyBinds;

  /**
   * A set of all keys used in all keybinds, other keys should not be tracked
   * @private
   */
  private readonly listenedKeys = computed(() => {
    const keyBinds = this.activeKeyBinds();
    const combos = Object.values(keyBinds);
    const allKeys = combos.flatMap(c => c);
    return new Set(allKeys);
  });

  /**
   * Keys currently pressed
   * @private
   */
  private readonly activeKeys = signal<string[]>([]);

  private readonly eventsSubject = new ReplaySubject<KeyBindEvent>(1);
  /**
   * KeyBindPressEvent events. We're using an observable instead of signal, as we want to be able to call
   * signals in the callback
   */
  public readonly events$ = this.eventsSubject.asObservable();
  /**
   * Be careful with recursion when using effects!
   */
  public readonly events = toSignal(this.events$, {initialValue: null});

  constructor() {
    effect(() => {
      const activeKeys = this.activeKeys();
      const activeSet = new Set(activeKeys);

      const keybinds = this.activeKeyBinds();

      for (const [target, combo] of Object.entries(keybinds)) {
        if (combo.length !== activeKeys.length) continue;

        const allPressed = combo.every(key => activeSet.has(key));
        if (allPressed) {
          this.eventsSubject.next({
            target: target as KeyBindTarget,
          });
        }
      }
    });

    this.document.addEventListener('keydown', e => this.handleKeyDown(e));
    this.document.addEventListener('keyup', e => this.handleKeyUp(e));
  }

  handleKeyDown(event: KeyboardEvent) {
    if (event.repeat) return;

    const key = event.key;
    if (!this.listenedKeys().has(key)) return;

    this.activeKeys.update((keys) => {
      if (!keys.includes(key)) {
        return [...keys, key];
      }
      return keys;
    });
  }

  handleKeyUp(event: KeyboardEvent) {
    const key = event.key;
    if (!this.listenedKeys().has(key)) return;

    this.activeKeys.update((keys) => {
      return keys.filter((k) => k !== key);
    });
  }

}
