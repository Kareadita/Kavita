import {ChangeDetectionStrategy, Component, input} from '@angular/core';
import {NgOptimizedImage} from "@angular/common";

@Component({
  selector: 'app-discord-button',
  imports: [
    NgOptimizedImage
  ],
  templateUrl: './discord-button.component.html',
  styleUrl: './discord-button.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[class.block]': 'block()',
  },
})
export class DiscordButtonComponent {
  href = input<string>('');
  label = input<string>('');
  /** When true, the button fills the full width of its container (useful on mobile) */
  block = input<boolean>(false);
}
