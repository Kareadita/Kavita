import {ChangeDetectionStrategy, Component, computed, input} from '@angular/core';
import {NgTemplateOutlet} from '@angular/common';
import {RouterLink} from '@angular/router';
import {TranslocoDirective} from '@jsverse/transloco';
import {DefaultValuePipe} from "../../../_pipes/default-value.pipe";
import {MetadataFieldChange} from "../../../_models/kavitaplus/metadata-field-change";
import {MetadataFieldChangeKindTitlePipe} from "../../../_pipes/metadata-field-change-kind-title.pipe";
import {MetadataFieldChangeKind} from "../../../_models/kavitaplus/metadata-field-change-kind.enum";
import {RelationshipPipe} from "../../../_pipes/relationship.pipe";
import {RelationKind} from "../../../_models/series-detail/relation-kind";
import {TagBadgeComponent, TagBadgeCursor} from "../../../shared/tag-badge/tag-badge.component";

type ValueKind = 'null' | 'primitive' | 'array' | 'object' | 'relations';

interface RelationLink {
  name: string;
  kind: RelationKind | null;
  seriesId: number;
  libraryId: number;
}

interface DiffCell {
  kind: ValueKind;
  text: string | null;
  items: string[] | null;
  relations?: RelationLink[] | null;
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

function toRelationLink(item: unknown): RelationLink {
  const obj = (item !== null && typeof item === 'object') ? item as Record<string, unknown> : {};
  return {
    name: stringify(obj['relatedSeriesName']),
    kind: typeof obj['kind'] === 'number' ? obj['kind'] as RelationKind : null,
    seriesId: Number(obj['relatedSeriesId']),
    libraryId: Number(obj['relatedSeriesLibraryId']),
  };
}

function processCell(value: unknown, depth: number, field?: MetadataFieldChangeKind): DiffCell {
  if (value === null || value === undefined) {
    return {kind: 'null', text: null, items: null};
  }
  if (field === MetadataFieldChangeKind.Relationships && Array.isArray(value)) {
    return {kind: 'relations', text: null, items: null, relations: (value as unknown[]).map(toRelationLink)};
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
  imports: [TranslocoDirective, NgTemplateOutlet, RouterLink, DefaultValuePipe, MetadataFieldChangeKindTitlePipe, RelationshipPipe, TagBadgeComponent],
  templateUrl: './kavitaplus-diff.component.html',
  styleUrl: './kavitaplus-diff.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KavitaplusDiffComponent {
  protected readonly TagBadgeCursor = TagBadgeCursor;

  diff = input.required<MetadataFieldChange[]>();

  rows = computed<ProcessedRow[]>(() =>
    this.diff().map(change => {
      const from = processCell(change.from, 1, change.field);
      const to = processCell(change.to, 1, change.field);
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
