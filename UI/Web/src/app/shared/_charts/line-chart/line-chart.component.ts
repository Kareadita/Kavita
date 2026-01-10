import {ChangeDetectionStrategy, Component, computed, inject, input} from '@angular/core';
import {EChartsDirective, ECOption} from "../../../_directives/echarts.directive";
import {LineSeriesOption} from "echarts/charts";
import {ThemeService} from "../../../_services/theme.service";

type ArrayAble<T> = T | T[];

@Component({
  selector: 'app-line-chart',
  imports: [
    EChartsDirective
  ],
  templateUrl: './line-chart.component.html',
  styleUrl: './line-chart.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LineChartComponent {

  private themeService = inject(ThemeService);

  /**
   * Data used for the series
   */
  data = input.required<any[][] | any[]>();
  /**
   * Labels used for the valueAxis
   */
  axisLabels = input.required<string[]>();
  /**
   * Whether to clamp outliers to a calculated max (95th percentile)
   * @default true
   */
  clampOutliers = input(true);
  legendLabels = input<string[]>([]);
  showLegend = input(true);

  /**
   * Height of the chart
   *
   * @default 300px
   */
  height = input('300px');
  /**
   * Width of the chart
   *
   * @default 100%
   */
  width = input('100%');

  private isMultiLineChart = computed(() => {
    const data = this.data();
    if (data.length === 0) return false;

    return Array.isArray(data[0]);
  });

  private getColorForIndex(index: number): string {
    const palette = this.themeService.chartsColourPalette();
    return palette[index % palette.length];
  }

  private clampedMax = computed<number | null>(() => {
    if (!this.clampOutliers()) return null;

    const data = this.data();
    const allValues = this.isMultiLineChart()
      ? (data as number[][]).flat()
      : (data as number[]);

    const positiveValues = allValues.filter(v => v > 0).sort((a, b) => a - b);
    if (positiveValues.length === 0) return null;

    const p95Index = Math.floor(positiveValues.length * 0.95);
    const p95Value = positiveValues[p95Index];
    const maxValue = positiveValues[positiveValues.length - 1];

    // Only clamp if max is significantly larger than p95 (e.g., 2x)
    if (maxValue <= p95Value * 2) return null;

    // Round up to a nice number
    const magnitude = Math.pow(10, Math.floor(Math.log10(p95Value)));
    return Math.ceil(Math.ceil(p95Value / magnitude) * magnitude * 1.2);
  });

  private seriesOption = computed<ArrayAble<LineSeriesOption>>(() => {
    const data = this.data();
    const isMultiLineChart = this.isMultiLineChart();
    const clampedMax = this.clampedMax();

    if (!isMultiLineChart) {
      return {
        name: this.legendLabels()[0],
        type: 'line',
        smooth: true,
        data: data as any[],
        ...this.getMarkPointConfig(data as number[], clampedMax, 0)
      } as LineSeriesOption
    }

    return data.map((dataSet, index) => ({
      name: this.legendLabels()[index],
      type: 'line',
      data: dataSet as any[],
      smooth: true,
      itemStyle: {
        color: this.getColorForIndex(index),
      },
      ...this.getMarkPointConfig(dataSet as number[], clampedMax, index)
    }))
  });

  private getMarkPointConfig(seriesData: number[], clampedMax: number | null, seriesIndex: number): Partial<LineSeriesOption> {
    if (clampedMax === null) return {};

    const outlierPoints = seriesData
      .map((val, i) => {
        if (val > clampedMax) {
          return {
            name: `outlier-${seriesIndex}-${i}`,
            coord: [i, clampedMax],
            value: val,
            symbolRotate: 0
          };
        }
        return null;
      })
      .filter((p): p is NonNullable<typeof p> => p !== null);

    if (outlierPoints.length === 0) return {};

    return {
      markPoint: {
        symbol: 'triangle',
        symbolSize: 14,
        itemStyle: {
          color: this.getColorForIndex(seriesIndex)
        },
        label: {
          show: true,
          position: 'top',
          formatter: (params: any) => this.formatOutlierLabel(params.value),
          fontSize: 10,
          color: this.themeService.getCssVariable('--body-text-color')
        },
        data: outlierPoints
      }
    };
  }

  private formatOutlierLabel(value: number): string {
    if (value >= 1000) {
      return `${(value / 1000).toFixed(1)}k`;
    }
    return value.toFixed(0);
  }

  protected options = computed<ECOption>(() => {
    const clampedMax = this.clampedMax();

    return {
      legend: {
        show: this.showLegend(),
        data: this.legendLabels(),
        orient: 'horizontal',
        top: '-2%',
        textStyle: {
          color: this.themeService.getCssVariable('--body-text-color'),
        },
      },
      tooltip: {
        show: true,
        trigger: 'axis',
        order: 'valueDesc',
        formatter: (params: any) => this.formatTooltip(params, clampedMax)
      },
      grid: {
        left: '10%',
        right: '5%',
        top: clampedMax ? '10%' : '5%', // Extra room for outlier labels
        bottom: '5%'
      },
      xAxis: {
        type: 'category',
        boundaryGap: false,
        data: this.axisLabels(),
        axisLabel: {
          color: this.themeService.getCssVariable('--body-text-color'),
        }
      },
      yAxis: {
        type: 'value',
        max: clampedMax ?? undefined,
        axisLabel: {
          color: this.themeService.getCssVariable('--body-text-color'),
          formatter: (value: number) => {
            if (clampedMax && value === clampedMax) {
              return `${value}+`;
            }
            return value.toString();
          }
        }
      },
      series: this.seriesOption(),
    };
  });

  private formatTooltip(params: any, clampedMax: number | null): string {
    if (!Array.isArray(params)) {
      params = [params];
    }

    const lines = params.map((param: any) => {
      const value = param.value;
      const marker = param.marker;
      const seriesName = param.seriesName;
      const isOutlier = clampedMax && value > clampedMax;
      const displayValue = isOutlier ? `<strong>${value}</strong>` : value;

      return `${marker} ${seriesName}: ${displayValue}`;
    });

    const header = params[0]?.axisValueLabel ?? '';
    return `${header}<br/>${lines.join('<br/>')}`;
  }

}
