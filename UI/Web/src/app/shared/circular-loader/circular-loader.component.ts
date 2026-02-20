import {ChangeDetectionStrategy, Component, input} from '@angular/core';
import {NgStyle} from "@angular/common";
import {NgCircleProgressModule} from "ng-circle-progress";

@Component({
  selector: 'app-circular-loader',
  imports: [NgCircleProgressModule, NgStyle],
  templateUrl: './circular-loader.component.html',
  styleUrls: ['./circular-loader.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CircularLoaderComponent {

  currentValue = input<number>(0);
  /**
   * If an animation should be used
   */
  animation = input<boolean>(true);
  /**
   * Color of an inner bar
   */
  innerStrokeColor = input<string>('transparent');
  /**
   * Color of the Downloader bar
   */
  outerStrokeColor = input<string>('#4ac694');
  backgroundColor= input<string>('#000');
  fontSize = input<number>(36);
  /**
   * Show the icon inside the downloader
   */
  showIcon = input<boolean>(true);
  /**
   * The width in pixels of the loader
   */
  width = input<number>(100);
  /**
   * The height in pixels of the loader
   */
  height = input<number>(100);
  /**
   * Centers the icon in the middle of the loader. Best for card use.
   */
  center = input<boolean>(true);
}
