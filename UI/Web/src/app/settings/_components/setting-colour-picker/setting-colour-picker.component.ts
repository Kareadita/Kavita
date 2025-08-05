import {
  Component, DestroyRef,
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
import {ChromePickerComponent, Color, ColorPickerControl} from "@iplab/ngx-color-picker";
import {UserBreakpoint, UtilityService} from "../../../shared/_services/utility.service";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {debounceTime, distinctUntilChanged} from "rxjs/operators";
import {tap} from "rxjs";

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
  private readonly destroyRef = inject(DestroyRef);
  private readonly utilityService: UtilityService = inject(UtilityService);

  @ViewChild('colourPopup') colourPopup?: ElementRef;

  editMode = model(false);
  color = model.required<RgbaColor>();
  selected = input.required<boolean>();

  showPicker = signal(false);

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

    this.chromeControl.valueChanges
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        distinctUntilChanged(),
        debounceTime(500), // TODO: Find a fitting time, or move to explicit save?
        tap((color) => {
          const rgba: RgbaColor = {
            a: color.getRgba().alpha,
            r: Math.floor(color.getRgba().red),
            g: Math.floor(color.getRgba().green),
            b: Math.floor(color.getRgba().blue),
          };

          this.color.set(rgba);
        }),
      )
      .subscribe()
  }
}
