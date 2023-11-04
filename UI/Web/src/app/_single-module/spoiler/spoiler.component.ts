import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  inject,
  Input,
  OnInit,
  ViewEncapsulation
} from '@angular/core';
import {CommonModule} from '@angular/common';
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";
import {TranslocoDirective} from "@ngneat/transloco";

@Component({
  selector: 'app-spoiler',
  standalone: true,
    imports: [CommonModule, SafeHtmlPipe, TranslocoDirective],
  templateUrl: './spoiler.component.html',
  styleUrls: ['./spoiler.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None
})
export class SpoilerComponent implements OnInit{

  @Input({required: true}) html!: string;
  isCollapsed: boolean = true;
  public readonly cdRef = inject(ChangeDetectorRef);

  constructor() {
    this.isCollapsed = true;
    this.cdRef.markForCheck();
  }

  ngOnInit() {
    this.isCollapsed = true;
    this.cdRef.markForCheck();
  }


  toggle() {
    this.isCollapsed = !this.isCollapsed;
    this.cdRef.markForCheck();
  }
}
