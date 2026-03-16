import {ChangeDetectionStrategy, Component, input} from '@angular/core';

@Component({
    selector: 'app-update-section',
    imports: [],
    templateUrl: './update-section.component.html',
    styleUrl: './update-section.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class UpdateSectionComponent {
  items = input.required<string[]>();
  title = input.required<string>();
}
