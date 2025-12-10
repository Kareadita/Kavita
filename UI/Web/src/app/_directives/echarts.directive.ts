import {
  DestroyRef,
  Directive,
  effect,
  ElementRef,
  inject,
  input,
  NgZone,
  OnDestroy,
  OnInit,
  untracked
} from '@angular/core';
import {EChartsInitOpts, init, EChartsType, ComposeOption, registerTheme} from "echarts/core";
import { BarSeriesOption, LineSeriesOption, PieSeriesOption } from 'echarts/charts';
import {
  TitleComponentOption,
  TooltipComponentOption,
  DatasetComponentOption,
  LegendComponentOption,
  ToolboxComponentOption,
} from 'echarts/components';
import {ThemeService} from "../_services/theme.service";
import {asyncScheduler, Subject, Subscription, tap} from "rxjs";
import {throttleTime} from "rxjs/operators";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";

export type ECOption = ComposeOption<
  | BarSeriesOption
  | LineSeriesOption
  | PieSeriesOption
  | TitleComponentOption
  | TooltipComponentOption
  | DatasetComponentOption
  | LegendComponentOption
  | ToolboxComponentOption
>;

/**
 * Some code in this directive has been (partially) copied from https://github.com/xieziyu/ngx-echarts/blob/master/projects/ngx-echarts/src/lib/ngx-echarts.directive.ts
 */
@Directive({
  selector: '[appECharts]'
})
export class EChartsDirective implements OnInit, OnDestroy {

  private readonly destroyRef = inject(DestroyRef);
  private readonly el = inject(ElementRef);
  private readonly themeService = inject(ThemeService);
  private ngZone = inject(NgZone);

  readonly options = input<ECOption | null>(null);
  readonly initOptions = input<EChartsInitOpts | undefined>(undefined);
  readonly ignoreFirstResize = input(true);

  echart?: EChartsType;

  private resizer$ = new Subject<void>();
  private resizeObserver?: ResizeObserver;
  private resizeObserverHasFired = false;
  private animationFrameID?: number;

  constructor() {
    // Keep up to date with themes
    effect(() => {
      const kavitaTheme = this.themeService.currentTheme(); // Call on theme updates
      const options = untracked(this.options);
      if (!kavitaTheme || !options) return;

      this.ngZone.runOutsideAngular(() => {
        registerTheme(kavitaTheme.name, this.createTheme());

        this.echart?.dispose();
        this.echart = init(this.el.nativeElement, kavitaTheme.name, untracked(this.initOptions));
        this.echart.setOption(options);
      });
    });

    // Keep up to date with options
    effect(() => {
      const options = this.options();
      if (!options || !this.echart) return;

      this.ngZone.runOutsideAngular(() => {
        this.echart!.setOption(options);
      });
    });
  }

  ngOnInit() {
    const options = this.options();
    const kavitaTheme = this.themeService.currentTheme();
    if (!options || !kavitaTheme) return;

    this.ngZone.runOutsideAngular(() => {
      registerTheme(kavitaTheme.name, this.createTheme());

      this.echart = init(this.el.nativeElement, kavitaTheme.name, this.initOptions());
      this.echart.setOption(options);
    });

    this.resizer$.pipe(
      takeUntilDestroyed(this.destroyRef),
      throttleTime(100, asyncScheduler, { leading: false, trailing: true}),
      tap(() => {
        if (this.echart) {
          this.echart.resize();
        }
      })).subscribe();

    this.resizeObserver = this.ngZone.runOutsideAngular(
      () =>
        new window.ResizeObserver(entries => {
          for (const entry of entries) {
            if (entry.target === this.el.nativeElement) {
              if (!this.resizeObserverHasFired) {
                this.resizeObserverHasFired = true;

                if (this.ignoreFirstResize()) {
                  return;
                }
              }

              this.animationFrameID = window.requestAnimationFrame(() => {
                this.resizer$.next();
              });
            }
          }
        })
    );

    this.resizeObserver.observe(this.el.nativeElement);
  }

  ngOnDestroy() {
    if (this.animationFrameID) {
      window.cancelAnimationFrame(this.animationFrameID);
    }

    if (this.resizeObserver) {
      this.resizeObserver.unobserve(this.el.nativeElement);
    }
  }

  // TODO: Create a nice theme
  private createTheme() {
    const textColour = this.getCssVar('--body-text-color');

    // TODO: Use actual colours that don't all look alike
    const colorPalette = [
      this.getCssVar('--primary-color'),
      this.getCssVar('--primary-color-dark-shade'),
      this.getCssVar('--primary-color-darker-shade'),
      this.getCssVar('--primary-color-darkest-shade'),
    ];
    return {
      color: colorPalette,
      categoryAxis: {
        axisLabel: {
          color: textColour,
        }
      },
      valueAxis: {
        axisLabel: {
          color: textColour,
        }
      },
      label: {
        textBorderWidth: 0,
        textBorderColor: 'transparent',
        color: textColour,
      },
    };
  }

  private getCssVar(key: string) {
    return getComputedStyle(document.documentElement).getPropertyValue(key).trim();
  }

}
