import {SchemaPathTree, validate} from "@angular/forms/signals";

/**
 * 2 strings must match each other to be valid. Exports kind `mismatch`
 * @param field
 * @param other
 */
export function mustMatchValidator(field: SchemaPathTree<string>, other: SchemaPathTree<string>) {
  validate(field, ({value, valueOf}) => {
    const current = value();

    if (!current || current.trim().length === 0) {
      return null;
    }

    if (current === valueOf(other)) {
      return null; // match
    }

    return { kind: 'mismatch' };
  })
}
