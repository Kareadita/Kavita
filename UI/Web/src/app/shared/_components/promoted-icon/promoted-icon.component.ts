import {ChangeDetectionStrategy, Component, input} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
    selector: 'app-promoted-icon',
    imports: [
        TranslocoDirective
    ],
    templateUrl: './promoted-icon.component.html',
    styleUrl: './promoted-icon.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class PromotedIconComponent {
  promoted = input.required<boolean>();
}
