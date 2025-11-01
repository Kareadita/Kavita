import {Injectable, signal} from '@angular/core';
import {Subject} from "rxjs";

interface GamePadKeyEvent {
  /**
   * Buttons currently pressed
   */
  pressedButtons: readonly GamePadButtonKey[];
  /**
   * If the event is keydown, all newly added buttons
   */
  newButtons?: readonly GamePadButtonKey[];
  /**
   * If the event is keyup, all removed buttons
   */
  removedButtons?: readonly GamePadButtonKey[];
}

export enum GamePadButtonKey {
  A = 'A',
  B = 'B',
  X = 'X',
  Y = 'Y',
  LB = 'LB',
  RB = 'RB',
  LT = 'LT',
  RT = 'RT',
  Back = 'Back',
  Start = 'Start',
  AxisLeft = 'Axis-Left',   // Left Stick Button
  AxisRight = 'Axis-Right', // Right Stick Button
  DPadUp = 'DPad-Up',
  DPadDown = 'DPad-Down',
  DPadLeft = 'DPad-Left',
  DPadRight = 'DPad-Right',
  Power = 'Power',          // Guide / Home / Xbox Button
}

/**
 * Button order follows W3C standard mapping:
 * https://www.w3.org/TR/gamepad/#remapping
 */
export const GAMEPAD_BUTTON_KEYS: readonly GamePadButtonKey[] = [
  GamePadButtonKey.A,
  GamePadButtonKey.B,
  GamePadButtonKey.X,
  GamePadButtonKey.Y,
  GamePadButtonKey.LB,
  GamePadButtonKey.RB,
  GamePadButtonKey.LT,
  GamePadButtonKey.RT,
  GamePadButtonKey.Back,
  GamePadButtonKey.Start,
  GamePadButtonKey.AxisLeft,
  GamePadButtonKey.AxisRight,
  GamePadButtonKey.DPadUp,
  GamePadButtonKey.DPadDown,
  GamePadButtonKey.DPadLeft,
  GamePadButtonKey.DPadRight,
  GamePadButtonKey.Power,
];


/**
 * GamePadService provides a wrapper around the native GamePad browser API as it has a bad DX
 */
@Injectable({
  providedIn: 'root'
})
export class GamePadService {

  protected readonly _gamePads = signal<Set<Gamepad>>(new Set());
  public readonly gamePads = this._gamePads.asReadonly();

  private readonly keyUpEvents = new Subject<GamePadKeyEvent>();
  public readonly keyUpEvents$ = this.keyUpEvents.asObservable();
  private readonly keyDownEvents = new Subject<GamePadKeyEvent>();
  public readonly keyDownEvents$ = this.keyDownEvents.asObservable();

  private lastState = new Map<number, readonly GamePadButtonKey[]>();
  private pollId?: number;

  constructor() {
    window.addEventListener('gamepadconnected', (e: GamepadEvent) => {
      const startLoop = this.gamePads().size === 0;

      this._gamePads.update(s => new Set(s).add(e.gamepad));

      if (startLoop) {
        this.poll();
      }
    });

    window.addEventListener('gamepaddisconnected', (e: GamepadEvent) => {
      this._gamePads.update(s => {
        const newSet = new Set(s);
        newSet.delete(e.gamepad);
        return newSet;
      });

      this.lastState.delete(e.gamepad.index);
      if (this.gamePads().size == 0 && this.pollId) {
        cancelAnimationFrame(this.pollId);
      }
    });
  }

  private poll() {
    if (this.gamePads().size === 0) {
      return;
    }

    for (const gamePad of this.gamePads()) {
      const pressed: GamePadButtonKey[] = [];

      for (let idx = 0; idx < gamePad.buttons.length; idx++) {
        if (gamePad.buttons[idx].pressed) {
          pressed.push(GAMEPAD_BUTTON_KEYS[idx]);
        }
      }

      const last = this.lastState.get(gamePad.index) ?? [];
      const newButtons = pressed.filter(btn => !last.includes(btn));
      const removedButtons = last.filter(btn => !pressed.includes(btn));

      if (newButtons.length > 0) {
        this.keyDownEvents.next({
          pressedButtons: [...pressed],
          newButtons: [...newButtons],
        });
      }

      if (removedButtons.length > 0) {
        this.keyUpEvents.next({
          pressedButtons: [...pressed],
          removedButtons: [...removedButtons],
        });
      }

      this.lastState.set(gamePad.index, [...pressed]);
    }

    this.pollId = requestAnimationFrame(() => this.poll());
  }

}
