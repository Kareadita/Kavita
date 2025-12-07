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

  // TODO: Update colours, move into theme service?
  private getColorForIndex(index: number): string {
    const palette = this.themeService.chartsColourPalette();
    return palette[index % palette.length];
  }

  private seriesOption = computed<ArrayAble<LineSeriesOption>>(() => {
    const data = this.data();
    const isMultiLineChart = this.isMultiLineChart();

    if (!isMultiLineChart) {
      return {
        name: this.legendLabels()[0],
        type: 'line',
        stack: 'Total',
        data: data as any[],
      }
    }

    return data.map((dataSet, index) => ({
      name: this.legendLabels()[index],
      type: 'line',
      stack: 'Total',
      data: dataSet as any[],
      itemStyle: {
        color: this.getColorForIndex(index),
      }
    }))
  })

  protected options = computed<ECOption>(() => ({
    legend: {
      show: this.showLegend(),
      data: this.legendLabels(),
      textStyle: {
        color: this.themeService.getCssVariable('--body-text-color'),
      }
    },
    tooltip: {
      show: true,
      trigger: "axis",
      order: "valueDesc"
    },
    grid: {
      left: '10%',
      right: '5%',
      top: '5%',
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
      axisLabel: {
        color: this.themeService.getCssVariable('--body-text-color'),
      }
    },
    series: this.seriesOption(),
  }));

}
