import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input, OnInit} from '@angular/core';
import {ImageComponent} from "../../shared/image/image.component";
import {NextExpectedChapter} from "../../_models/series-detail/next-expected-chapter";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";
import {translate, TranslocoDirective} from "@jsverse/transloco";

@Component({
  selector: 'app-next-expected-card',
  standalone: true,
  imports: [ImageComponent, SafeHtmlPipe, TranslocoDirective],
  templateUrl: './next-expected-card.component.html',
  styleUrl: './next-expected-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class NextExpectedCardComponent implements OnInit {
  private readonly cdRef = inject(ChangeDetectorRef);

  /**
   * Card item url. Will internally handle error and missing covers
   */
  @Input() imageUrl = '';
  /**
   * This is the entity we are representing. It will be returned if an action is executed.
   */
  @Input({required: true}) entity!: NextExpectedChapter;
  title: string = '';

  ngOnInit(): void {
    if (this.entity.expectedDate) {
      const utcPipe = new UtcToLocalTimePipe();
      this.title = translate('next-expected-card.title', {date: utcPipe.transform(this.entity.expectedDate, 'shortDate')});
    }
    this.cdRef.markForCheck();
  }

}
