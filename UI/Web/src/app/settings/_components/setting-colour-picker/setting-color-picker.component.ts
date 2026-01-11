import {
  Component,
  DestroyRef,
  ElementRef,
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
import {NgClass, NgStyle} from '@angular/common';
import {SlotColorPipe} from "../../../_pipes/slot-color.pipe";
import {RgbaColor} from "../../../book-reader/_models/annotations/highlight-slot";
import {LongClickDirective} from "../../../_directives/long-click.directive";
import {ChromePickerComponent, Color, ColorPickerControl} from "@iplab/ngx-color-picker";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {debounceTime, distinctUntilChanged} from "rxjs/operators";
import {tap} from "rxjs";
import {Breakpoint, BreakpointService} from "../../../_services/breakpoint.service";

@Component({
  selector: 'app-setting-colour-picker',
  standalone: true,
  imports: [SlotColorPipe, LongClickDirective, ChromePickerComponent, NgClass, NgStyle],
  templateUrl: './setting-color-picker.component.html',
  styleUrl: './setting-color-picker.component.scss'
})
export class SettingColorPickerComponent implements OnInit {

  private readonly elementRef = inject(ElementRef);
  private readonly slotColorPipe = inject(SlotColorPipe);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly breakpointService = inject(BreakpointService);

  @ViewChild('colorPopup') colorPopup?: ElementRef;

  id = input.required<string>();
  label = input.required<string>();
  /**
   * Moves the pop-up right on mobile devices
   */
  first = input(false);
  /**
   * Moves the pop-up left on mobile devices
   */
  last = input(false);

  editMode = model(false);
  color = model.required<RgbaColor>();
  /**
   * If the edit mode can be changed due to user input
   */
  canChangeEditMode = input(true);
  selected = input.required<boolean>();

  showPicker = signal(false);

  @Output() selectPicker = new EventEmitter<void>();
  /**
   * Emits the raw color from the color picker rather than our RgbaColor
   */
  @Output() rawColorChange = new EventEmitter<Color>();

  chromeControl!: ColorPickerControl;

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: Event) {
    if (!this.showPicker()) return;

    if (!this.colorPopup) return;

    const clickedElement = event.target as Node;

    if (!this.elementRef.nativeElement.contains(clickedElement) && !this.colorPopup.nativeElement.contains(clickedElement)) {
      this.showPicker.set(false);
    }
  }

  onSelect() {
    this.selectPicker.emit();
  }

  longClick() {
    if (!this.canChangeEditMode()) return;

    this.editMode.update(b => !b);

    if (this.breakpointService.activeBreakpoint() < Breakpoint.Desktop) {
      this.showPicker.update(b => !b);
    }
  }

  togglePicker() {
    this.showPicker.update(b => !b);
  }

  ngOnInit() {
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
          this.rawColorChange.emit(color);
        }),
      )
      .subscribe()
  }
}
