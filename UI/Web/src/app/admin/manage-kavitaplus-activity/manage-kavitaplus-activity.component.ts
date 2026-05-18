import {ChangeDetectionStrategy, Component} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
  selector: 'app-manage-kavitaplus-activity',
  imports: [
    TranslocoDirective
  ],
  templateUrl: './manage-kavitaplus-activity.component.html',
  styleUrl: './manage-kavitaplus-activity.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManageKavitaplusActivityComponent {

}
