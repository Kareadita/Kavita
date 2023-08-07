import { Component, Input, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import {CommonModule} from "@angular/common";


export interface TimelineStep {
  title: string;
  active: boolean;
  icon: string;
  index: number;
}


@Component({
  selector: 'app-step-tracker',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './step-tracker.component.html',
  styleUrls: ['./step-tracker.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StepTrackerComponent {
  @Input() steps: Array<TimelineStep> = [];
  @Input() currentStep: number = 0;


  constructor(private readonly cdRef: ChangeDetectorRef) {}

}
