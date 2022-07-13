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
  @Input() fontSize: string = '36px';
  @Input() showIcon: boolean = true;
  /**
   * The width in pixels of the loader
   */
  @Input() width: string = '100px';
  /**
   * The height in pixels of the loader
   */
   @Input() height: string = '100px';
  /**
   * Centers the icon in the middle of the loader. Best for card use. 
   */
  @Input() center: boolean = true;
}
