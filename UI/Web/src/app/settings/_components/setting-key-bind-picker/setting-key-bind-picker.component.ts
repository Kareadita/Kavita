import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  forwardRef,
  inject,
  input,
  OnDestroy,
  signal
} from '@angular/core';
import {ControlValueAccessor, FormControl, NG_VALUE_ACCESSOR} from "@angular/forms";
import {KeyBindService, KeyCode, ModifierKeyCodes} from "../../../_services/key-bind.service";
import {KeyBind, KeyBindTarget} from "../../../_models/preferences/preferences";
import {KeyBindPipe} from "../../../_pipes/key-bind.pipe";
import {DOCUMENT} from "@angular/common";
import {GamePadService} from "../../../_services/game-pad.service";
import {filter, fromEvent, merge, Subscription, tap} from "rxjs";
import {TagBadgeComponent, TagBadgeCursor} from "../../../shared/tag-badge/tag-badge.component";
import {TranslocoDirective} from "@jsverse/transloco";
import {DefaultValuePipe} from "../../../_pipes/default-value.pipe";
import {AccountService} from "../../../_services/account.service";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {debounceTime, take} from "rxjs/operators";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";

@Component({
  selector: 'app-setting-key-bind-picker',
  imports: [
    KeyBindPipe,
    TagBadgeComponent,
    TranslocoDirective,
    DefaultValuePipe,
    NgbTooltip
  ],
  templateUrl: './setting-key-bind-picker.component.html',
  styleUrl: './setting-key-bind-picker.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => SettingKeyBindPickerComponent),
      multi: true,
    }
  ]
})
export class SettingKeyBindPickerComponent implements ControlValueAccessor, OnDestroy {

  private readonly destroyRef = inject(DestroyRef);
  protected readonly keyBindService = inject(KeyBindService);
  private readonly gamePadService = inject(GamePadService);
  private readonly accountService = inject(AccountService);
  private readonly document = inject(DOCUMENT);
  private readonly elementRef = inject(ElementRef);

  control = input.required<FormControl<KeyBind>>();
  target = input.required<KeyBindTarget>();
  index = input.required<number>();
  duplicated = input.required<boolean>();

  selectedKeyBind = signal<KeyBind>({key: KeyCode.Empty});
  disabled = signal(false);

  private _onChange: (value: KeyBind) => void = () => {};
  private _onTouched: () => void = () => {};
  protected readonly subscriptions = signal<Subscription[]>([]);
  protected readonly isListening = computed(() => this.subscriptions().length > 0);
  protected readonly tagBadgeCursor = computed(() =>
    this.accountService.isReadOnly() ? TagBadgeCursor.NotAllowed : TagBadgeCursor.Clickable);

  constructor() {
    effect(() => {
      const selectedKeys = this.selectedKeyBind();
      this._onChange(selectedKeys);
      this._onTouched();
    });

    fromEvent(this.document, 'click')
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        filter((event: Event) => {
          return !this.elementRef.nativeElement.contains(event.target);
        }),
        filter(() => this.isListening()),
        tap(() => this.stopListening()),
      ).subscribe();

  }

  writeValue(keyBind: KeyBind): void {
      this.selectedKeyBind.set(keyBind)
  }

  registerOnChange(fn: (_: KeyBind) => void): void {
    this._onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this._onTouched = fn;
  }

  setDisabledState?(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }

  ngOnDestroy() {
    this.keyBindService.disabled.set(false);
    this.subscriptions().forEach(s => s.unsubscribe());
  }

  startListening() {
    if (this.isListening() || this.accountService.isReadOnly()) return;

    this.keyBindService.disabled.set(true);

    const keydown$ = fromEvent(this.document, 'keydown').pipe(
      tap((e) => this.onKeyDown(e as KeyboardEvent)),
    );

    const gamePad$ = this.gamePadService.keyDownEvents$.pipe(
      tap(e => this.selectedKeyBind.set({
        key: KeyCode.Empty,
        controllerSequence: e.pressedButtons,
      })),
    );

    const sub = merge(keydown$, gamePad$).pipe(
      takeUntilDestroyed(this.destroyRef),
      debounceTime(700),
      filter(() => this.control().valid),
      take(1),
      tap(() => this.stopListening()),
    ).subscribe();

    this.subscriptions.update(s => [sub, ...s]);
  }

  stopListening() {
    this.keyBindService.disabled.set(false);
    this.subscriptions().forEach(s => s.unsubscribe());
    this.subscriptions.set([]);
  }

  private onKeyDown = (event: KeyboardEvent) => {
    const eventKey = event.key.toLowerCase() as KeyCode;

    this.selectedKeyBind.set({
      key: ModifierKeyCodes.includes(eventKey) ? KeyCode.Empty : eventKey,
      meta: event.metaKey,
      alt: event.altKey,
      control: event.ctrlKey,
      shift: event.shiftKey,
    });

    event.preventDefault();
    event.stopPropagation();
  };
}
