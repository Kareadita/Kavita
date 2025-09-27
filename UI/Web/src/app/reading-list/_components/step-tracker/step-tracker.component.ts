import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input} from '@angular/core';
import {NgClass} from "@angular/common";


export interface TimelineStep {
  title: string;
  active: boolean;
  icon: string;
  index: number;
}


@Component({
    selector: 'app-step-tracker',
  imports: [
    NgClass
  ],
    templateUrl: './step-tracker.component.html',
    styleUrls: ['./step-tracker.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class StepTrackerComponent {
  private readonly cdRef = inject(ChangeDetectorRef);

  @Input() steps: Array<TimelineStep> = [];
  @Input() currentStep: number = 0;

}
