import {WritableSignal} from "@angular/core";

export function patchArray<T extends { id: number }>(arr: T[], entityMap: Map<number, any>): T[] {
  let changed = false;
  const result = arr.map(item => {
    const updated = entityMap.get(item.id);
    if (updated) {
      changed = true;
      return { ...updated } as T;
    }
    return item;
  });
  return changed ? result : arr;
}

export function patchSignalArray<T extends { id: number }>(signal: WritableSignal<T[]>, entityMap: Map<number, any>): void {
  const patched = patchArray(signal(), entityMap);
  if (patched !== signal()) {
    signal.set([...patched]);
  }
}

export function patchEntity<T>(arr: T[], entity: T, selector: (item: T) => any = (item: any) => item.id): T[] {
  const entityKey = selector(entity);
  const idx = arr.findIndex(item => selector(item) === entityKey);
  if (idx === -1) return arr;
  const result = [...arr];
  result[idx] = { ...entity };
  return result;
}

export function patchEntitySignal<T>(signal: WritableSignal<T[]>, entity: T, selector?: (item: T) => any): void {
  const patched = patchEntity(signal(), entity, selector);
  if (patched !== signal()) {
    signal.set(patched);
  }
}
