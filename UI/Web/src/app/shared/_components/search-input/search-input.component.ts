import {ChangeDetectionStrategy, Component, computed, inject, input, model, output} from '@angular/core';
import {takeUntilDestroyed, toObservable} from '@angular/core/rxjs-interop';
import {distinctUntilChanged, map, of, skip, switchMap, timer} from 'rxjs';
import {TranslocoService} from '@jsverse/transloco';

let nextId = 0;

/**
 * Reusable search field: leading magnifying-glass icon, an optional inline clear button, and an
 * accessible (visually-hidden) label. Two ways to handle the label:
 *  - pass a translated {@link label} and the component renders its own visually-hidden label + placeholder, or
 *  - omit {@link label} and pass an {@link inputId} that matches an external `<label for="...">`.
 * The debounced {@link search} output is what consumers should react to; {@link value}/valueChange stay
 * immediate so the text and clear button remain responsive.
 */
@Component({
  selector: 'app-search-input',
  imports: [],
  templateUrl: './search-input.component.html',
  styleUrl: './search-input.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SearchInputComponent {
  private readonly translocoService = inject(TranslocoService);

  /** Two-way bound search text. Updates immediately on each keystroke. */
  value = model<string>('');
  /** Already-localized label text. When set, renders a visually-hidden label and is used as the placeholder. */
  label = input<string>('');
  /** Id linking the label and input. Auto-generated when not supplied. */
  inputId = input<string>(`search-input-${nextId++}`);
  /** Already-localized aria-label for the clear button. Falls back to a generic "clear search". */
  clearLabel = input<string>('');
  /** Debounce before the search output emits. 0 = immediate. */
  debounceMs = input<number>(300);
  /** Debounced, de-duplicated search value. */
  search = output<string>();

  clearAriaLabel = computed(() => this.clearLabel() || this.translocoService.translate('search-input.clear'));

  constructor() {
    toObservable(this.value).pipe(
      skip(1), // ignore the initial value so we don't fire a search on first render
      switchMap(v => this.debounceMs() > 0 ? timer(this.debounceMs()).pipe(map(() => v)) : of(v)),
      distinctUntilChanged(),
      takeUntilDestroyed(),
    ).subscribe(v => this.search.emit(v));
  }

  onInput(value: string) {
    this.value.set(value);
  }

  clear() {
    this.value.set('');
  }
}
