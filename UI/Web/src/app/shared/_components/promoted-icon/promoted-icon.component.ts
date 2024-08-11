import {ChangeDetectionStrategy, Component, Input} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
  selector: 'app-promoted-icon',
  standalone: true,
    imports: [
        TranslocoDirective
    ],
  templateUrl: './promoted-icon.component.html',
  styleUrl: './promoted-icon.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PromotedIconComponent {
  @Input({required: true}) promoted: boolean = false;
}
