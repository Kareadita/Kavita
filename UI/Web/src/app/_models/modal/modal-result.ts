export interface ModalResult<T = void> {
  success: boolean;
  data?: T;
  coverImageUpdated?: boolean;
  isDeleted?: boolean;
}

export function modalSaved<T>(data?: T, coverImageUpdated = false): ModalResult<T> {
  return { success: true, data, coverImageUpdated, isDeleted: false };
}

export function modalDeleted<T>(data?: T): ModalResult<T> {
  return { success: true, data, coverImageUpdated: false, isDeleted: true };
}

export function modalCancelled<T>(): ModalResult<T> {
  return { success: false };
}
