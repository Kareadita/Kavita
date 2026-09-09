import {PathKind, SchemaPath, SchemaPathRules, validate} from "@angular/forms/signals";
import {environment} from "../../environments/environment";

const defaultUrlOptions = {
  requireTls: true,
}

export function url<TPathKind extends PathKind = PathKind.Root>(
  path: SchemaPath<string, SchemaPathRules.Supported, TPathKind>,
  options?: { requireTls: boolean },
) {
  const finalOptions = {
    ...defaultUrlOptions,
    ...options
  };

  validate(path, (value) => {
    const uri = value.value();
    if (!uri || uri.trim().length === 0) {
      return null;
    }

    if (environment.production && finalOptions.requireTls && !uri.startsWith('https')) {
      return {
        kind: 'requireTls',
      }
    }

    try {
      new URL(uri);
    } catch {
      return {
        kind: 'invalidUri'
      };
    }

    return null;
  });
}
