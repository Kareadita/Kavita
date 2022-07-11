import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-splash-container',
  templateUrl: './splash-container.component.html',
  styleUrls: ['./splash-container.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SplashContainerComponent {}