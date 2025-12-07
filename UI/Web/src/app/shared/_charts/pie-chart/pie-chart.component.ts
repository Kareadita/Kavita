import {ChangeDetectionStrategy, Component, computed, inject, input} from '@angular/core';
import {EChartsDirective, ECOption} from "../../../_directives/echarts.directive";
import {ThemeService} from "../../../_services/theme.service";

@Component({
  selector: 'app-pie-chart',
  imports: [
    EChartsDirective
  ],
  templateUrl: './pie-chart.component.html',
  styleUrl: './pie-chart.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PieChartComponent {

  private readonly themeService = inject(ThemeService);

  data = input.required<any[]>();
  valueTransformer = input.required<(v: any) => string>();

  options = computed<ECOption>(() => {
    return {
      name: 'Format',
      legend: {
        top: '5%',
        left: 'center',
        textStyle: {
          color: this.themeService.getCssVariable('--body-text-color'),
        }
      },
      tooltip: {
        trigger: 'item'
      },
      series: [{
        type: 'pie',
        radius: ['40%', '70%'],
        center: ['50%', '70%'],
        startAngle: 180,
        endAngle: 360,
        color: this.themeService.chartsColourPalette(),
        data: (this.data() || []).map(r => {
          return {
            value: r.count,
            name: this.valueTransformer()(r.value)
          }
        }),
        label: {
          color: this.themeService.getCssVariable('--body-text-color'),
        }
      }],
    };
  });

}
