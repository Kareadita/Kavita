import { ChangeDetectionStrategy, Component, Input } from '@angular/core';

@Component({
  selector: 'app-circular-loader',
  templateUrl: './circular-loader.component.html',
  styleUrls: ['./circular-loader.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CircularLoaderComponent {

  @Input() currentValue: number = 0;
  @Input() maxValue: number = 0;
  @Input() animation: boolean = true;
  @Input() innerStrokeColor: string = 'transparent';
}
