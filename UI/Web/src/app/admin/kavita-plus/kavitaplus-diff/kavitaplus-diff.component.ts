import {ChangeDetectionStrategy, Component, computed, input} from '@angular/core';
import {NgTemplateOutlet} from '@angular/common';
import {TranslocoDirective} from '@jsverse/transloco';
import {DefaultValuePipe} from "../../../_pipes/default-value.pipe";
import {MetadataFieldChange} from "../../../_models/kavitaplus/metadata-field-change";
import {MetadataFieldChangeKindTitlePipe} from "../../../_pipes/metadata-field-change-kind-title.pipe";
import {MetadataFieldChangeKind} from "../../../_models/kavitaplus/metadata-field-change-kind.enum";

type ValueKind = 'null' | 'primitive' | 'array' | 'object';

interface DiffCell {
  kind: ValueKind;
  text: string | null;
  items: string[] | null;
}

interface SubRow {
  key: string;
  from: DiffCell;
  to: DiffCell;
}

interface ProcessedRow {
  field: MetadataFieldChangeKind;
  from: DiffCell;
  to: DiffCell;
  subRows: SubRow[];
}

function stringify(value: unknown): string {
  if (value === null || value === undefined) return '';
  return String(value);
}

function processCell(value: unknown, depth: number): DiffCell {
  if (value === null || value === undefined) {
    return {kind: 'null', text: null, items: null};
  }
  if (typeof value !== 'object') {
    return {kind: 'primitive', text: String(value), items: null};
  }
  if (Array.isArray(value)) {
    return {kind: 'array', text: null, items: (value as unknown[]).map(stringify)};
  }
  // object
  if (depth >= 2) {
    return {kind: 'primitive', text: JSON.stringify(value), items: null};
  }
  // depth < 2: caller handles sub-row expansion, return object marker
  return {kind: 'object', text: null, items: null};
}

function expandSubRows(from: unknown, to: unknown): SubRow[] {
  const fromObj = (from !== null && typeof from === 'object' && !Array.isArray(from))
    ? from as Record<string, unknown> : {};
  const toObj = (to !== null && typeof to === 'object' && !Array.isArray(to))
    ? to as Record<string, unknown> : {};

  const keys = new Set([...Object.keys(fromObj), ...Object.keys(toObj)]);
  return Array.from(keys).map(key => ({
    key,
    from: processCell(fromObj[key] ?? null, 2),
    to: processCell(toObj[key] ?? null, 2),
  }));
}

@Component({
  selector: 'app-kavitaplus-diff',
  imports: [TranslocoDirective, NgTemplateOutlet, DefaultValuePipe, MetadataFieldChangeKindTitlePipe],
  templateUrl: './kavitaplus-diff.component.html',
  styleUrl: './kavitaplus-diff.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KavitaplusDiffComponent {
  diff = input.required<MetadataFieldChange[]>();

  rows = computed<ProcessedRow[]>(() =>
    this.diff().map(change => {
      const from = processCell(change.from, 1);
      const to = processCell(change.to, 1);
      const isObjectExpansion = from.kind === 'object' || to.kind === 'object';

      return {
        field: change.field,
        from,
        to,
        subRows: isObjectExpansion ? expandSubRows(change.from, change.to) : [],
      };
    })
  );
}
