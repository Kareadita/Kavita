import { Component, Input, ChangeDetectionStrategy, OnInit, ChangeDetectorRef } from '@angular/core';
import { BehaviorSubject, ReplaySubject } from 'rxjs';


export interface TimelineStep {
  title: string;
  active: boolean;
  icon: string;
  index: number;
}


@Component({
  selector: 'app-step-tracker',
  templateUrl: './step-tracker.component.html',
  styleUrls: ['./step-tracker.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StepTrackerComponent implements OnInit {
  @Input() steps: Array<TimelineStep> = [];
  @Input() currentStep!: TimelineStep;
  

  constructor(private readonly cdRef: ChangeDetectorRef) {}

  ngOnInit() {

  }
}
