import {OutputEmitterRef} from '@angular/core';

/**
 * Contract implemented by every step body rendered inside the
 * {@link ManageLicenseModalComponent}. Each screen owns its own footer and emits
 * navigation intents that the parent modal acts on.
 */
export interface ManageLicenseModalScreen {
  /** Return to the hub (Manage subscription) view. */
  readonly back: OutputEmitterRef<void>;
  /** Close the entire modal. */
  readonly dismiss: OutputEmitterRef<void>;
}
