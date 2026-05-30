import {ChangeDetectionStrategy, Component, computed, inject, input} from '@angular/core';
import {EChartsDirective, ECOption} from "../../../_directives/echarts.directive";
import {ThemeService} from "../../../_services/theme.service";

@Component({
  selector: 'app-sparkline',
  imports: [
    EChartsDirective
  ],
  templateUrl: './sparkline.component.html',
  styleUrl: './sparkline.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SparklineComponent {

  private readonly themeService = inject(ThemeService);

  /**
   * Series values to plot
   */
  data = input.required<number[]>();
  /**
   * Height of the sparkline
   * @default 2.5rem
   */
  height = input('2.5rem');
  /**
   * Width of the sparkline
   * @default 8rem
   */
  width = input('8rem');

  protected options = computed<ECOption>(() => {
    const raw = this.data();

    // Always render a line so rows stay visually consistent. With no data we
    // draw a flat baseline at zero; a single point is duplicated into a flat line.
    let data: number[];
    if (raw.length >= 2) {
      data = raw;
    } else if (raw.length === 1) {
      data = [raw[0], raw[0]];
    } else {
      data = [0, 0];
    }

    const allZero = data.every(v => v === 0);
    const lineColor = this.themeService.getCssVariable('--primary-color');

    return {
      grid: {
        left: 0,
        right: 0,
        top: 2,
        bottom: 2
      },
      xAxis: {
        type: 'category',
        show: false,
        boundaryGap: false,
        data: data.map((_, i) => i)
      },
      yAxis: {
        type: 'value',
        show: false,
        min: 0,
        // Give a flat zero line headroom so it sits on the baseline instead of centering
        max: allZero ? 1 : undefined
      },
      tooltip: {
        show: false
      },
      series: [{
        type: 'line',
        data: data,
        smooth: true,
        symbol: 'none',
        lineStyle: {
          width: 1.5,
          color: lineColor
        },
        areaStyle: {
          color: lineColor,
          opacity: 0.15
        }
      }]
    };
  });

}
