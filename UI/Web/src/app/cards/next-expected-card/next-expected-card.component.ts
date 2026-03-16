import {ChangeDetectionStrategy, Component, computed, input} from '@angular/core';
import {ImageComponent} from "../../shared/image/image.component";
import {NextExpectedChapter} from "../../_models/series-detail/next-expected-chapter";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";
import {translate, TranslocoDirective} from "@jsverse/transloco";

@Component({
    selector: 'app-next-expected-card',
    imports: [ImageComponent, SafeHtmlPipe, TranslocoDirective],
    templateUrl: './next-expected-card.component.html',
    styleUrl: './next-expected-card.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class NextExpectedCardComponent {
  private readonly utcPipe = new UtcToLocalTimePipe();
  /**
   * Card item url. Will internally handle error and missing covers
   */
  imageUrl = input.required<string>();
  /**
   * This is the entity we are representing. It will be returned if an action is executed.
   */
  entity = input.required<NextExpectedChapter>();
  title = computed(() => {
    const expectedDate = this.entity()?.expectedDate;
    if (expectedDate) {
      return translate('next-expected-card.title', {date: this.utcPipe.transform(expectedDate, 'shortDate')})
    }
    return '';
  });
}
