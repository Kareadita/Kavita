import {ChangeDetectionStrategy, Component, input, signal, ViewEncapsulation} from '@angular/core';
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
    selector: 'app-spoiler',
    imports: [SafeHtmlPipe, TranslocoDirective],
    templateUrl: './spoiler.component.html',
    styleUrls: ['./spoiler.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    encapsulation: ViewEncapsulation.None
})
export class SpoilerComponent {

  html = input.required<string>();
  isCollapsed = signal<boolean>(true);

  toggle() {
    this.isCollapsed.update(x => !x);
  }
}
