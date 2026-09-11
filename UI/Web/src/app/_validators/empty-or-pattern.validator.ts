import {PathKind, SchemaPath, SchemaPathRules, validate} from "@angular/forms/signals";

export function emptyOrPattern<TPathKind extends PathKind = PathKind.Root>(
  path: SchemaPath<string, SchemaPathRules.Supported, TPathKind>,
  pattern: RegExp
) {
  validate(path, (ctx) => {
    const value = ctx.value();
    if (value.length === 0) {
      return null;
    }

    if (pattern.test(value)) {
      return null;
    }

    return {
      kind: 'pattern'
    }
  });
}
