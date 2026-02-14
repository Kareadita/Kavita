import {Library} from "../app/_models/library/library";
import {Series} from "../app/_models/series";
import {ActivatedRoute} from "@angular/router";
import {Signal} from "@angular/core";
import {toSignal} from "@angular/core/rxjs-interop";
import {map} from "rxjs/operators";

/**
 * Type-safety for router resolvers. Add new fields as needed.
 */
export interface ResolvedData {
  library?: Library;
  series?: Series;
}

// export function getResolvedData<K extends keyof ResolvedData>(
//   route: ActivatedRoute,
//   key: K
// ): NonNullable<ResolvedData[K]> {
//   const value = route.snapshot.data[key];
//   if (value == null) {
//     throw new Error(`Route data '${key}' not found. Is the resolver configured?`);
//   }
//   return value as NonNullable<ResolvedData[K]>;
// }

export function getResolvedData<K extends keyof ResolvedData>(
  route: ActivatedRoute,
  key: K
): Signal<NonNullable<ResolvedData[K]>> {
  return toSignal(
    route.data.pipe(map(data => data[key] as NonNullable<ResolvedData[K]>)),
    { requireSync: true }
  );
}
