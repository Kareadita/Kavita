import {createMetadataKey, PathKind, SchemaPath, SchemaPathRules, validate, validateTree} from "@angular/forms/signals";
import {environment} from "../../../environments/environment";
import {translate} from "@jsverse/transloco";


export const REQUIRED_IF_NAME = createMetadataKey<string>();

/**
 *
 * @param path The field that is required if pathOther has a valid value
 * @param pathOther Must have REQUIRED_IF_NAME metadata set
 */
export function requiredIf<TValue, TValueOther, TPathKind extends PathKind = PathKind.Root, TPathKindOther extends PathKind = PathKind.Root>(
  path: SchemaPath<TValue, SchemaPathRules.Supported, TPathKind>,
  pathOther: SchemaPath<TValueOther, SchemaPathRules.Supported, TPathKindOther>,
) {
  validateTree(path, (ctx) => {
    const value = ctx.value();
    const otherValue = ctx.valueOf(pathOther);
    const otherState = ctx.stateOf(pathOther);

    if (!otherState.valid()) {
      if (!environment.production && !otherState.fieldTree().hasMetadata(REQUIRED_IF_NAME)) {
        throw new Error("The metadata REQUIRED_IF_NAME must be set when using requiredIf for proper error messages");
      }

      return {
        kind: 'requiredIfOtherInvalid',
        message: translate('errors.required-if-other-invalid', {other: otherState.fieldTree().metadata(REQUIRED_IF_NAME)!()}),
        fieldTree: ctx.fieldTree
      };
    }

    if (!otherValue || (Array.isArray(otherValue) && otherValue.length === 0)) {
      return null;
    }

    if (value || (Array.isArray(value) && value.length > 0)) {
      return null;
    }

    return {
      kind: 'required',
      fieldTree: ctx.fieldTree
    }
  })
}

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
