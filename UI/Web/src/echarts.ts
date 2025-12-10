import {use} from "echarts/core";
import {BarChart, LineChart, PieChart} from "echarts/charts";
import {
  DatasetComponent,
  GridComponent, LegendComponent,
  TitleComponent,
  TooltipComponent,
  TransformComponent,
  ToolboxComponent
} from "echarts/components";
import {LabelLayout, UniversalTransition} from "echarts/features";
import {SVGRenderer} from "echarts/renderers";


export function registerECharts() {
  use([
    BarChart,
    LineChart,
    PieChart,
    TitleComponent,
    TooltipComponent,
    GridComponent,
    DatasetComponent,
    TransformComponent,
    LegendComponent,
    LabelLayout,
    UniversalTransition,
    SVGRenderer,
    ToolboxComponent
  ]);
}
