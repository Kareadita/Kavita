import {ChangeDetectionStrategy, Component, input} from '@angular/core';

@Component({
  selector: 'app-banner',
  templateUrl: './banner.component.html',
  styleUrl: './banner.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[attr.color]': 'color()' }
})
export class BannerComponent {
  title     = input.required<string>();
  body      = input('');
  color     = input<'primary' | 'danger' | 'warning'>('primary');
  isCta     = input(false);
  iconClass = input('');
}
