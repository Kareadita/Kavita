import {HttpParams} from '@angular/common/http';
import {Injectable} from '@angular/core';
import {PaginatedResult} from 'src/app/_models/pagination';
import {ActionItem} from "../../_models/actionables/action-item";
import {AbstractControl, FormArray, FormGroup, ValidationErrors} from "@angular/forms";

export enum KEY_CODES {
  RIGHT_ARROW = 'ArrowRight',
  LEFT_ARROW = 'ArrowLeft',
  DOWN_ARROW = 'ArrowDown',
  UP_ARROW = 'ArrowUp',
  ESC_KEY = 'Escape',
  SPACE = ' ',
  ENTER = 'Enter',
  G = 'g',
  B = 'b',
  F = 'f',
  H = 'h',
  K = 'k',
  BACKSPACE = 'Backspace',
  DELETE = 'Delete',
  SHIFT = 'Shift',
  CONTROL = 'Control',
  META = 'Meta',
  ALT = 'Alt',
}

export interface FormErrorEntry {
  path: string;
  level: 'group' | 'array' | 'control';
  errors: ValidationErrors;
}

/**
 * Recursively walks an AbstractControl tree and collects all errors.
 * Captures errors at group/array level (cross-field validators) AND control level.
 */
export function collectFormErrors(
  control: AbstractControl,
  path: string = ''
): FormErrorEntry[] {
  const results: FormErrorEntry[] = [];

  // Capture errors at the current node (works for groups, arrays, and controls)
  if (control.errors) {
    const level: FormErrorEntry['level'] =
      control instanceof FormGroup ? 'group'
        : control instanceof FormArray ? 'array'
          : 'control';

    results.push({
      path: path || '(root)',
      level,
      errors: control.errors,
    });
  }

  // Recurse into children
  if (control instanceof FormGroup) {
    for (const key of Object.keys(control.controls)) {
      const childPath = path ? `${path}.${key}` : key;
      results.push(...collectFormErrors(control.controls[key], childPath));
    }
  } else if (control instanceof FormArray) {
    control.controls.forEach((child, i) => {
      const childPath = `${path}[${i}]`;
      results.push(...collectFormErrors(child, childPath));
    });
  }

  return results;
}

/**
 * Prints all errors to the console in a readable format.
 */
export function printFormErrors(control: AbstractControl, label = 'Form'): void {
  const entries = collectFormErrors(control);

  if (entries.length === 0) {
    console.log(`[${label}] No errors`);
    return;
  }

  console.group(`[${label}] ${entries.length} error entr${entries.length === 1 ? 'y' : 'ies'}`);
  for (const { path, level, errors } of entries) {
    console.log(`(${level}) ${path}:`, errors);
  }
  console.groupEnd();
}


@Injectable({
  providedIn: 'root'
})
export class UtilityService {

  filter(input: string, filter: string): boolean {
    if (input === null || filter === null || input === undefined || filter === undefined) return false;
    const reg = /[_\.\-]/gi;
    return input.toUpperCase().replace(reg, '').includes(filter.toUpperCase().replace(reg, ''));
  }

  filterMatches(input: string, filter: string): boolean {
    if (input === null || filter === null || input === undefined || filter === undefined) return false;
    const reg = /[_\.\-]/gi;
    return input.toUpperCase().replace(reg, '') === filter.toUpperCase().replace(reg, '');
  }

  isVolume(d: unknown) {
    return d != null && d.hasOwnProperty('chapters');
  }

  isChapter(d: unknown) {
    return d != null && d.hasOwnProperty('volumeId');
  }

  isSeries(d: unknown) {
    return d != null && d.hasOwnProperty('originalName');
  }

  isReadingList(d: unknown) {
    return d != null && d.hasOwnProperty('title') && d.hasOwnProperty('startingYear');
  }

  isUserCollection(d: unknown) {
    return d != null && d.hasOwnProperty('title') && d.hasOwnProperty('itemCount') && !d.hasOwnProperty('startingYear');
  }

  isInViewport(element: Element, additionalTopOffset: number = 0) {
    const rect = element.getBoundingClientRect();
    return (
        rect.top >= additionalTopOffset &&
        rect.left >= 0 &&
        rect.bottom <= (window.innerHeight || document.documentElement.clientHeight) &&
        rect.right <= (window.innerWidth || document.documentElement.clientWidth)
    );
  }

  deepEqual(object1: any | undefined | null, object2: any | undefined | null) {
    if ((object1 === null || object1 === undefined) && (object2 !== null || object2 !== undefined)) return false;
    if ((object2 === null || object2 === undefined) && (object1 !== null || object1 !== undefined)) return false;
    if (object1 === null && object2 === null) return true;
    if (object1 === undefined && object2 === undefined) return true;


    const keys1 = Object.keys(object1);
    const keys2 = Object.keys(object2);
    if (keys1.length !== keys2.length) {
      return false;
    }
    for (const key of keys1) {
      const val1 = object1[key];
      const val2 = object2[key];
      const areObjects = this.isObject(val1) && this.isObject(val2);
      if (
        areObjects && !this.deepEqual(val1, val2) ||
        !areObjects && val1 !== val2
      ) {
        return false;
      }
    }
    return true;
  }

  private isObject(object: any) {
    return object != null && typeof object === 'object';
  }

  addPaginationIfExists(params: HttpParams, pageNum?: number, itemsPerPage?: number) {
    if (pageNum !== null && pageNum !== undefined && itemsPerPage !== null && itemsPerPage !== undefined) {
      params = params.append('pageNumber', pageNum + '');
      params = params.append('pageSize', itemsPerPage + '');
    }
    return params;
  }

  createPaginatedResult<T>(response: any, paginatedVariable: PaginatedResult<T[]> | undefined = undefined) {
    if (paginatedVariable === undefined) {
      paginatedVariable = new PaginatedResult<T[]>();
    }
    if (response.body === null) {
      paginatedVariable.result = [];
    } else {
      paginatedVariable.result = response.body;
    }

    const pageHeader = response.headers?.get('Pagination');
    if (pageHeader !== null) {
      paginatedVariable.pagination = JSON.parse(pageHeader);
    }

    return paginatedVariable;
  }

  copyActionItem(item: ActionItem<any>): ActionItem<any> {
    return {
      ...item,
      children: item.children.map(child => this.copyActionItem(child))
    }
  }
}
