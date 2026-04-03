import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  inject,
  input,
  model,
  OnInit,
  signal
} from '@angular/core';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {debounceTime, Subject, switchMap, tap} from "rxjs";
import {FormsModule} from "@angular/forms";
import {TranslocoDirective} from "@jsverse/transloco";
import {CronFrequency} from "../../../shared/_models/cron-frequency";
import {CronFrequencyPipe} from "../../../_pipes/cron-frequency.pipe";
import {SettingsService} from "../../../admin/settings.service";
import {SettingItemComponent} from "../setting-item/setting-item.component";

@Component({
  selector: 'app-setting-cron-item',
  imports: [
    FormsModule,
    TranslocoDirective,
    CronFrequencyPipe,
    SettingItemComponent
  ],
  templateUrl: './setting-cron-item.component.html',
  styleUrl: './setting-cron-item.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SettingCronItemComponent implements OnInit {
  protected readonly CronFrequency = CronFrequency;

  private readonly destroyRef = inject(DestroyRef);
  private readonly settingsService = inject(SettingsService);

  title = input.required<string>();
  subtitle = input<string>();
  frequencies = input<CronFrequency[]>([CronFrequency.Disabled, CronFrequency.Daily, CronFrequency.Weekly, CronFrequency.Custom]);
  value = model.required<string>();

  selectedFrequency = signal<CronFrequency>(CronFrequency.Daily);
  customCron = signal<string>('');
  cronInvalid = signal<boolean>(false);

  private customCron$ = new Subject<string>();
  private initialized = false;

  constructor() {
    // Sync internal state when value changes externally
    effect(() => {
      const val = this.value();
      if (!this.initialized) return;
      this.syncFromValue(val);
    });
  }

  ngOnInit() {
    this.syncFromValue(this.value());
    this.initialized = true;

    // Debounced cron validation pipeline
    this.customCron$.pipe(
      debounceTime(500),
      switchMap(val => this.settingsService.isValidCronExpression(val)),
      tap(isValid => {
        if (isValid) {
          this.cronInvalid.set(false);
          this.value.set(this.customCron());
        } else {
          this.cronInvalid.set(true);
        }
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe();
  }

  onFrequencyChange(freq: CronFrequency) {
    this.selectedFrequency.set(freq);
    if (freq !== CronFrequency.Custom) {
      this.cronInvalid.set(false);
      this.customCron.set('');
      this.value.set(freq);
    }
    // If custom, wait for user to type a valid cron
  }

  onCustomCronChange(val: string) {
    this.customCron.set(val);
    if (!val) {
      this.cronInvalid.set(true);
      return;
    }
    this.customCron$.next(val);
  }

  private syncFromValue(val: string) {
    const freqs = this.frequencies();
    const presetValues: string[] = freqs.filter(f => f !== CronFrequency.Custom);

    if (presetValues.includes(val)) {
      this.selectedFrequency.set(val as CronFrequency);
      this.customCron.set('');
      this.cronInvalid.set(false);
    } else {
      this.selectedFrequency.set(CronFrequency.Custom);
      this.customCron.set(val);
      this.cronInvalid.set(false);
    }
  }
}
