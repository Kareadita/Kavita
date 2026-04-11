import {Library} from "../app/_models/library/library";
import {Series} from "../app/_models/series";
import {ActivatedRoute} from "@angular/router";
import {DestroyRef, inject, signal, Signal, WritableSignal} from "@angular/core";
import {takeUntilDestroyed, toSignal} from "@angular/core/rxjs-interop";
import {map} from "rxjs/operators";
import {Volume} from "../app/_models/volume";
import {Chapter} from "../app/_models/chapter";
import {Person} from "../app/_models/metadata/person";
import {ReadingList} from "../app/_models/reading-list/reading-list";
import {UserCollection} from "../app/_models/collection-tag";

/**
 * Type-safety for router resolvers. Add new fields as needed.
 */
export interface ResolvedData {
  library?: Library;
  series?: Series;
  volume?: Volume;
  chapter?: Chapter;
  person?: Person;
  readingList?: ReadingList;
  collection?: UserCollection;
}


export function getResolvedData<K extends keyof ResolvedData>(
  route: ActivatedRoute,
  key: K
): Signal<NonNullable<ResolvedData[K]>> {
  return toSignal(
    route.data.pipe(map(data => data[key] as NonNullable<ResolvedData[K]>)),
    { requireSync: true }
  );
}

export function getWritableResolvedData<K extends keyof ResolvedData>(
  route: ActivatedRoute, key: K
): WritableSignal<NonNullable<ResolvedData[K]>> {
  const destroyRef = inject(DestroyRef);
  const initial = route.snapshot.data[key] as NonNullable<ResolvedData[K]>;
  const sig = signal(initial);

  route.data.pipe(
    map(data => data[key] as NonNullable<ResolvedData[K]>),
    takeUntilDestroyed(destroyRef)
  ).subscribe(value => sig.set(value));
  return sig;
}
