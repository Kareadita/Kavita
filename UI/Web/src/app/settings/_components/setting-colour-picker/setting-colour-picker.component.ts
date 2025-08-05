import {
  Component,
  effect, ElementRef,
  EventEmitter,
  HostListener,
  inject,
  input,
  model,
  OnInit,
  Output,
  signal,
  ViewChild
} from '@angular/core';
import {CommonModule} from '@angular/common';
import {SlotColorPipe} from "../../../_pipes/slot-color.pipe";
import {RgbaColor} from "../../../book-reader/_models/annotations/highlight-slot";
import {LongClickDirective} from "../../../_directives/long-click.directive";
import {ChromePickerComponent, ColorPickerControl} from "@iplab/ngx-color-picker";
import {UserBreakpoint, UtilityService} from "../../../shared/_services/utility.service";

@Component({
  selector: 'app-setting-colour-picker',
  standalone: true,
  imports: [CommonModule, SlotColorPipe, LongClickDirective, ChromePickerComponent],
  templateUrl: './setting-colour-picker.component.html',
  styleUrl: './setting-colour-picker.component.scss'
})
export class SettingColourPickerComponent implements OnInit {

  private readonly elementRef = inject(ElementRef);
  private readonly slotColorPipe = inject(SlotColorPipe);
  private readonly utilityService: UtilityService = inject(UtilityService);

  @ViewChild('colourPopup') colourPopup?: ElementRef;

  editMode = model(false);
  color = input.required<RgbaColor>();
  selected = input.required<boolean>();

  showPicker = signal(false);

  @Output() colorChange = new EventEmitter<string>();
  @Output() selectPicker = new EventEmitter<void>();

  chromeControl!: ColorPickerControl;

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: Event): void {
    if (!this.showPicker()) return;

    if (!this.colourPopup) return;

    const clickedElement = event.target as Node;

    if (!this.elementRef.nativeElement.contains(clickedElement) && !this.colourPopup.nativeElement.contains(clickedElement)) {
      this.showPicker.set(false);
    }
  }

  onColorChange(color: string): void {
    this.colorChange.emit(color);
  }

  onSelect(): void {
    this.selectPicker.emit();
  }

  longClick() {
    this.editMode.update(b => !b);

    if (this.utilityService.activeUserBreakpoint() < UserBreakpoint.Desktop) {
      this.showPicker.update(b => !b);
    }
  }

  togglePicker(): void {
    this.showPicker.update(b => !b);
  }

  ngOnInit(): void {
    this.chromeControl = new ColorPickerControl()
      .setValueFrom(this.slotColorPipe.transform(this.color()))
      .showAlphaChannel()
      .hidePresets();
  }
}
