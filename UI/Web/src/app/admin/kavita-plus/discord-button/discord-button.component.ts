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
})
export class DiscordButtonComponent {
  href = input<string>('');
  label = input<string>('');
}
