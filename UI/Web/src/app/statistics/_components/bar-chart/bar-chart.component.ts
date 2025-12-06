import {ChangeDetectionStrategy, Component, computed, inject, input} from '@angular/core';
import {EChartsDirective, ECOption} from "../../../_directives/echarts.directive";
import {LabelFormatterCallback, TopLevelFormatterParams, TooltipOption} from "echarts/types/dist/shared";
import {BarSeriesOption} from "echarts/charts";
import {ThemeService} from "../../../_services/theme.service";

// Type copied over from ECharts, as it's not exported
export type OptionDataValue = string | number | Date | null | undefined;

export type ToolTipFormatterContext = {
  data: any | any[];
  event: TopLevelFormatterParams;
}

type ArrayAble<T> = T | T[];

/**
 * Represents a BarChart with one series as backing data
 */
@Component({
  selector: 'app-bar-chart',
  imports: [EChartsDirective],
  templateUrl: './bar-chart.component.html',
  styleUrl: './bar-chart.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BarChartComponent {

  private themeService = inject(ThemeService);

  /**
   * Data used for the series
   */
  data = input.required<any[]>();
  /**
   * Labels used for the valueAxis
   */
  axisLabels = input.required<string[]>();

  /**
   * Labels used for the valueAxis but on the other side
   *
   * I.e. right for horizontal, top for vertical
   */
  axisLabelsOther = input<string[]>();
  /**
   * Should the bars lay horizontal
   */
  horizontal = input(false);
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

  /**
   * Show labels on the series bars
   *
   * @default false
   */
  showLabels = input(false);
  /**
   * Position of the bar labels
   *
   * @default right
   */
  labelPosition = input<'top' | 'bottom' | 'left' | 'right' | 'inside' | 'insideLeft' | 'insideRight' | 'insideTop' | 'insideBottom'>('right');
  /**
   * Formatter method for the bar labels
   */
  seriesLabelFormatter = input<LabelFormatterCallback | undefined>(undefined);
  /**
   * Cycle through the colour pallet
   */
  multiColor = input(false);

  /**
   * Show tooltips when hovering over bars
   *
   * @default false
   */
  showToolTips = input(false);
  /**
   * Full formatter for tooltips, use in conjugation with htmlTooltip to take full control
   *
   * @default undefined
   */
  toolTipFormatter = input<((ctx: ToolTipFormatterContext) => (string | HTMLElement | HTMLElement[]) )| undefined>(undefined);
  /**
   * Simple formatter for tooltips
   *
   * @default undefined
   */
  toolTipValueFormatter = input<((value: OptionDataValue | OptionDataValue[], dataIndex: number) => string) | undefined>(undefined)
  /**
   * When set to true, disabled some default styling on tooltips
   *
   * @default false
   */
  htmlToolTip = input(false);
  yAxisMax = input<number | undefined>(undefined);

  processedData = computed(() => {
    const data = this.data();
    const multiColor = this.multiColor();

    if (!multiColor) return data;

    return data.map((d, index) => {
      return {
        value: d,
        itemStyle: {
          color: this.getColorForIndex(index),
          borderRadius: 5
        }
      };
    })
  });

  protected tooltipOption = computed<TooltipOption | undefined>(() => {
    if (!this.showToolTips()) return undefined;

    const tooltipOption: TooltipOption = {
      trigger: 'axis',
      valueFormatter: this.toolTipValueFormatter(),
    };

    const fn = this.toolTipFormatter();
    if (fn) {
      tooltipOption.formatter = (params: TopLevelFormatterParams) => {
        const ctx: ToolTipFormatterContext = {
          data: Array.isArray(params) ? params.map(p => this.data()[p.dataIndex]) : this.data()[params.dataIndex],
          event: params,
        }

        return fn(ctx);
      }
    }

    // Disable padding such that the custom HTML is all that's shown
    if (this.htmlToolTip()) {
      tooltipOption.padding = [0, 0, 0, 0];
      tooltipOption.backgroundColor = 'transparent';
      tooltipOption.borderColor = 'transparent';
    }

    return tooltipOption;
  });

  protected xAxisOption = computed(() => {
    const isHorizontal = this.horizontal();

    const leftXAxis = {
      type: isHorizontal ? 'value' : 'category',
      data: isHorizontal ? undefined : this.axisLabels(),
      splitLine: { show: false },
      axisLine: {show: false },
      axisLabel: {
        color: this.themeService.getCssVariable('--body-text-color'),
      }
    };

    if (isHorizontal || !this.axisLabelsOther()) {
      return leftXAxis;
    }

    const rightXAxis = {
      type: 'category',
      data: this.axisLabelsOther(),
      position: 'right' as const,
      axisLine: { show: false },
      axisTick: { show: false },
      axisLabel: {
        color: this.themeService.getCssVariable('--body-text-color'),
      }
    };

    return [leftXAxis, rightXAxis];
  });

  protected yAxisOption = computed(() => {
    const isHorizontal = this.horizontal();

    const leftYAxis = {
      type: isHorizontal ? 'category' : 'value',
      data: isHorizontal ? this.axisLabels() : undefined,
      position: 'left' as const,
      axisLine: { show: false },
      axisLabel: {
        rotate: 0,
        color: this.themeService.getCssVariable('--body-text-color'),
      },
      max: this.yAxisMax(),
    };

    if (!isHorizontal || !this.axisLabelsOther()) {
      return leftYAxis;
    }

    const rightYAxis = {
      type: 'category',
      data: this.axisLabelsOther(),
      position: 'right' as const,
      axisLine: { show: false },
      axisTick: { show: false },
      max: this.yAxisMax(),
      axisLabel: {
        color: this.themeService.getCssVariable('--body-text-color'),
      }
    };

    return [leftYAxis, rightYAxis];
  });

  protected seriesOption = computed<ArrayAble<BarSeriesOption>>(() => {
    return [{
      type: 'bar',
      data: this.processedData(),
      label: {
        show: this.showLabels(),
        position: this.labelPosition(),
        formatter: this.seriesLabelFormatter(),
      },
      itemStyle: {
        borderRadius: this.horizontal() ? 5 : [5, 5, 0, 0],
      },
      barGap: '10%',
      barMinHeight: 5
    }];
  });

  private getColorForIndex(index: number): string {
    const palette = this.themeService.chartsColourPalette();
    return palette[index % palette.length];
  }

  protected options = computed(() => {
    return {
      grid: {
        left: '10%',
        right: this.axisLabelsOther() ? '10%' : '5%',
        top: '5%',
        bottom: '5%'
      },
      xAxis: this.xAxisOption(),
      yAxis: this.yAxisOption(),
      series: this.seriesOption(),
      tooltip: this.tooltipOption(),
    };
  });
}
