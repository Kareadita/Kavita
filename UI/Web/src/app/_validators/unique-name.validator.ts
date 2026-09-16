import {SchemaPath, validateHttp} from "@angular/forms/signals";
import {environment} from "../../environments/environment";
import {Signal} from "@angular/core";

const baseUrl = environment.apiUrl;

/**
 * A validation to ensure the uniqueness of a field against the backend. This requires an endpoint at controller/name-exists
 * that accepts a name query parameter.
 * @param path
 * @param controller
 * @param originalName
 */
export function uniqueName(path: SchemaPath<string>, controller: string, originalName: Signal<string>) {
  validateHttp<string, boolean>(path, {
    request: ctx => `${baseUrl}${controller}/name-exists?name=${ctx.valueOf(path)}`,
    onSuccess: (result, ctx) => {
      const name = ctx.valueOf(path);
      if (name != originalName() && result) {
        return {
          kind: 'duplicateName'
        }
      }

      return null;
    },
    onError: (error, ctx) => {
      console.error(error);
      return null;
    },
  });
}
