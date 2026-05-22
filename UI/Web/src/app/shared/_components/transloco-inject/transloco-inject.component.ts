import {
  AfterContentInit,
  Component,
  ContentChildren,
  DestroyRef,
  inject,
  input,
  QueryList,
  signal,
  TemplateRef
} from '@angular/core';
import {NgTemplateOutlet} from "@angular/common";
import {TranslocoSlotDirective} from "../../../_directives/transloco-slot.directive";
import {TranslocoService} from "@jsverse/transloco";
import {startWith} from "rxjs";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";


type Part =
| { type: 'text'; value: string }
| { type: 'slot'; name: string };

/**
 * Renders a Transloco string with Angular component slots.
 *
 * Translation string uses `{slotName}` placeholders:
 *   "connect-with-discord-help": "Or {connectToDiscordBtn} to fill this automatically"
 *
 * Usage:
 *   <app-transloco-inject key="connect-with-discord-help">
 *     <ng-template translocoSlot="connectToDiscordBtn">
 *       <app-discord-button [label]="t('connect-with-discord')" />
 *     </ng-template>
 *   </app-transloco-inject>
 */
@Component({
  selector: 'app-transloco-inject',
  standalone: true,
  imports: [NgTemplateOutlet],
  // Role="text" groups the inline fragments for screen readers
  host: { role: 'text' },
  template: `
    @for (part of parts(); track $index) {
      @if (part.type === 'text') {
        {{ part.value }}
      } @else {
        @if (getTemplate(part.name); as tpl) {
          <ng-container [ngTemplateOutlet]="tpl" />
        }
      }
    }
    <!-- ng-template[translocoSlot] children render nothing here;
         they're only projected so ContentChildren can query them. -->
    <ng-content />
  `,
})
export class TranslocoInjectComponent implements AfterContentInit {
  readonly key = input.required<string>();

  @ContentChildren(TranslocoSlotDirective)
  private readonly slotDirectives!: QueryList<TranslocoSlotDirective>;

  private readonly transloco = inject(TranslocoService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly parts = signal<Part[]>([]);

  ngAfterContentInit(): void {
    this.transloco.langChanges$
      .pipe(startWith(this.transloco.getActiveLang()), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.buildParts());
  }

  private buildParts(): void {
    // 1. Get the raw untranslated string before Transloco's interpolator runs.
    const raw = this.resolveRawKey(this.transloco.getTranslation(this.transloco.getActiveLang()), this.key());
    if (!raw) {
      this.parts.set([{ type: 'text', value: this.transloco.translate(this.key()) }]);
      return;
    }

    // 2. Swap each {{slotName}} for a null-byte sentinel BEFORE handing off to Transloco.
    //    Null bytes cannot appear in real translation strings, so splits are unambiguous.
    let processed = raw;
    for (const d of this.slotDirectives) {
      processed = processed.replaceAll(`{{${d.translocoSlot()}}}`, `\x00SLOT:${d.translocoSlot()}\x00`);
    }

    // 3. Split on sentinels and build the part list.
    //    (No second translate() call — we already have the raw string from getTranslation().)
    const segments = processed.split(/(\x00SLOT:[^\x00]+\x00)/);
    this.parts.set(
      segments
        .map((seg): Part => {
          const m = seg.match(/^\x00SLOT:([^\x00]+)\x00$/);
          return m ? { type: 'slot', name: m[1] } : { type: 'text', value: seg };
        })
        .filter((p): p is Part => p.type === 'slot' || p.value !== ''),
    );
  }

  /**
   * Resolves a dot-separated key against the translation object,
   * handling both flat ('a.b.c') and nested ({ a: { b: { c: '' } } }) shapes.
   */
  private resolveRawKey(translations: Record<string, unknown>, key: string): string | null {
    // Try flat key first (Transloco sometimes merges scopes as flat keys)
    if (typeof translations[key] === 'string') return translations[key] as string;

    const val = key.split('.').reduce<unknown>((obj, k) =>
        (obj != null && typeof obj === 'object') ? (obj as Record<string, unknown>)[k] : undefined,
      translations,
    );
    return typeof val === 'string' ? val : null;
  }

  protected getTemplate(name: string): TemplateRef<unknown> | null {
    return this.slotDirectives?.find(d => d.translocoSlot() === name)?.tpl ?? null;
  }
}
