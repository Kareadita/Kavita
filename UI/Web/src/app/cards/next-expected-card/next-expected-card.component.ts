import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input} from '@angular/core';
import {CommonModule} from '@angular/common';
import {ImageComponent} from "../../shared/image/image.component";
import {NextExpectedChapter} from "../../_models/series-detail/next-expected-chapter";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";

@Component({
  selector: 'app-next-expected-card',
  standalone: true,
  imports: [CommonModule, ImageComponent, SafeHtmlPipe],
  templateUrl: './next-expected-card.component.html',
  styleUrl: './next-expected-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class NextExpectedCardComponent {
  private readonly cdRef = inject(ChangeDetectorRef);

  /**
   * Card item url. Will internally handle error and missing covers
   */
  @Input() imageUrl = '';
  /**
   * This is the entity we are representing. It will be returned if an action is executed.
   */
  @Input({required: true}) entity!: NextExpectedChapter;

  /**
   * Additional information to show on the overlay area. Will always render.
   */
  @Input() overlayInformation: string = '';
  title: string = '';



  ngOnInit(): void {
    const tokens = this.entity.title.split(':');
    this.overlayInformation = `<div>${tokens[0]}</div><div>${tokens[1]}</div>`;

    if (this.entity.expectedDate) {
      const utcPipe = new UtcToLocalTimePipe();
      this.title = '~ ' + utcPipe.transform(this.entity.expectedDate, 'shortDate');
    }
    this.cdRef.markForCheck();
  }

}
