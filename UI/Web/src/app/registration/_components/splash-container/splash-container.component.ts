import {ChangeDetectionStrategy, Component, inject} from '@angular/core';
import {AsyncPipe, NgStyle} from "@angular/common";
import {NavService} from "../../../_services/nav.service";

@Component({
  selector: 'app-splash-container',
  templateUrl: './splash-container.component.html',
  styleUrls: ['./splash-container.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    NgStyle,
    AsyncPipe
  ],
  standalone: true
})
export class SplashContainerComponent {
  protected readonly navService = inject(NavService);
}
