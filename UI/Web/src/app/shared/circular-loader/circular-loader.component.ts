import {ChangeDetectionStrategy, Component, Input} from '@angular/core';
import {NgClass, NgStyle} from "@angular/common";
import {NgCircleProgressModule } from "ng-circle-progress";

@Component({
  selector: 'app-circular-loader',
  standalone: true,
  imports: [NgCircleProgressModule, NgStyle, NgClass],
  templateUrl: './circular-loader.component.html',
  styleUrls: ['./circular-loader.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CircularLoaderComponent {

  @Input() currentValue: number = 0;
  /**
   * If an animation should be used
   */
  @Input() animation: boolean = true;
  /**
   * Color of an inner bar
   */
  @Input() innerStrokeColor: string = 'transparent';
  /**
   * Color of the Downloader bar
   */
  @Input() outerStrokeColor: string = '#4ac694';
  @Input() backgroundColor: string = '#000';
  @Input() fontSize: string = '36px';
  /**
   * Show the icon inside the downloader
   */
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
