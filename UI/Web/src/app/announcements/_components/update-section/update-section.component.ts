import {ChangeDetectionStrategy, Component, Input} from '@angular/core';

@Component({
    selector: 'app-update-section',
    imports: [],
    templateUrl: './update-section.component.html',
    styleUrl: './update-section.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class UpdateSectionComponent {
  @Input({required: true}) items: Array<string> = [];
  @Input({required: true}) title: string = '';
}
