import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject} from '@angular/core';
import {CommonModule} from '@angular/common';

@Component({
  selector: 'app-user-holds',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './user-holds.component.html',
  styleUrls: ['./user-holds.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UserHoldsComponent {
  private readonly cdRef = inject(ChangeDetectorRef);

  constructor() {}
}
