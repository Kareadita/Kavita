import {PathKind, SchemaPath, SchemaPathRules, validate} from "@angular/forms/signals";

//https://stackoverflow.com/questions/2123131/determine-if-10-digit-string-is-valid-amazon-asin
const AsinPattern = /^(B0|BT)[0-9A-Z]{8}$/;

/**
 * Validates that the string has a high probability of being an ASIN
 */
export function amazonCode<TPathKind extends PathKind = PathKind.Root>(
  path: SchemaPath<string, SchemaPathRules.Supported, TPathKind>
) {
  validate(path, (ctx) => {
    const asin = ctx.value();
    if (!asin || asin.trim().length === 0) {
      return null;
    }

    if (AsinPattern.test(asin.toUpperCase())) {
      return null;
    }

    return {
      kind: 'invalidAmazonCode'
    };
  });
}
