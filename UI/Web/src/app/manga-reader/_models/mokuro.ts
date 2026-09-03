import {FileDimension} from './file-dimension';

export interface MokuroBlock {
  box: [number, number, number, number];
  vertical: boolean;
  font_size: number;
  lines: string[];
  lines_coords?: number[][][];
}

export interface MokuroPage {
  version: string;
  img_width: number;
  img_height: number;
  img_path: string;
  blocks: MokuroBlock[];
}

export interface MokuroVolume {
  version: string;
  title?: string;
  title_uuid?: string;
  volume?: string;
  volume_uuid?: string;
  pages: MokuroPage[];
}

const pathCollator = new Intl.Collator(undefined, {numeric: true, sensitivity: 'base'});

export function alignMokuroPages(pages: MokuroPage[], dimensions: FileDimension[]): MokuroPage[] {
  if (pages.length < 2 || dimensions.length === 0) return pages;

  const ordered: Array<MokuroPage | undefined> = new Array(pages.length);
  const unused = new Set(pages.map((_, index) => index));
  const pagePaths = pages.map(page => normalizePath(page.img_path));
  const indicesByPath = indexPaths(pagePaths, path => path);
  const indicesByFileName = indexPaths(pagePaths, fileName);

  for (const dimension of [...dimensions].sort((a, b) => a.pageNumber - b.pageNumber)) {
    if (!dimension.fileName || dimension.pageNumber < 0 || dimension.pageNumber >= ordered.length) continue;

    const filePath = normalizePath(dimension.fileName);
    const exact = uniqueUnusedIndex(indicesByPath.get(filePath), unused);
    const candidate = exact.count > 0
      ? exact
      : uniqueUnusedIndex(indicesByFileName.get(fileName(filePath)), unused);

    if (candidate.count !== 1) continue;
    ordered[dimension.pageNumber] = pages[candidate.index];
    unused.delete(candidate.index);
  }

  const remaining = [...unused].sort((a, b) => pathCollator.compare(pagePaths[a], pagePaths[b]));
  let remainingIndex = 0;

  for (let index = 0; index < ordered.length; index++) {
    if (!ordered[index]) ordered[index] = pages[remaining[remainingIndex++]];
  }

  return ordered.filter((page): page is MokuroPage => page !== undefined);
}

function normalizePath(path: string): string {
  return path.replace(/\\/g, '/').replace(/^\.\//, '').replace(/^\/+/, '').toLowerCase();
}

function fileName(path: string): string {
  return path.slice(path.lastIndexOf('/') + 1);
}

function indexPaths(paths: string[], getKey: (path: string) => string): Map<string, number[]> {
  const index = new Map<string, number[]>();
  paths.forEach((path, pathIndex) => {
    const key = getKey(path);
    const matches = index.get(key) ?? [];
    matches.push(pathIndex);
    index.set(key, matches);
  });
  return index;
}

function uniqueUnusedIndex(indices: number[] | undefined, unused: Set<number>): {count: number; index: number} {
  let count = 0;
  let index = -1;

  for (const candidate of indices ?? []) {
    if (!unused.has(candidate)) continue;
    count++;
    index = candidate;
    if (count > 1) break;
  }

  return {count, index};
}
