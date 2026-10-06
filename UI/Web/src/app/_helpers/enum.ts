
export function allEnums<T>(enumObj: T): (T[keyof T] & number)[] {
  return Object.keys(enumObj as object)
    .filter(key => !isNaN(Number(key)) && parseInt(key, 10) >= 0)
    .map(key => parseInt(key, 10)) as (T[keyof T] & number)[];
}
