import {ChangeDetectionStrategy, Component} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {WikiLink} from "../../../_models/wiki";

@Component({
  selector: 'app-kavita-plus-connect-providers',
  imports: [
    TranslocoDirective
  ],
  templateUrl: './kavita-plus-connect-providers.component.html',
  styleUrl: './kavita-plus-connect-providers.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KavitaPlusConnectProvidersComponent {

  protected readonly WikiLink = WikiLink;
}
