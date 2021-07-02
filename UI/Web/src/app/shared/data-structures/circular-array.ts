/**
 * An array that loops. [0, 1] and call next 3 times, element 0 will be the current()
 */
export class CircularArray<T> {
    arr: T[];
    currentIndex: number;
  
    constructor(arr: T[], startIndex: number) {
      this.arr = arr;
      this.currentIndex = startIndex || 0;
    }
  
    /**
     * 
     * @returns element in array ahead of current index
     */
    next() {
      const i = this.currentIndex;
      const arr = this.arr;
      this.currentIndex = i < arr.length - 1 ? i + 1 : 0;
      return this.current();
    }
    
    /**
     * 
     * @returns element in array behind the current index
     */
    prev() {
      const i = this.currentIndex;
      const arr = this.arr;
      this.currentIndex = i > 0 ? i - 1 : arr.length - 1;
      return this.current();
    }
  
    /**
     * 
     * @returns Current element
     */
    current() {
      return this.arr[this.currentIndex];
    }
    
    /**
     * Peek the current element
     * @param offset Optional offset to look ahead
     * @returns 
     */
    peek(offset: number = 0) {
      const i = this.currentIndex + 1 + offset;
      const arr = this.arr;
      const peekIndex = i < arr.length - 1 ? i + 1 : 0;
      return this.arr[peekIndex];
    }
  
    /**
     * 
     * @returns Total size of internal array
     */
    size() {
      return this.arr.length;
    }
  
    /**
     * Applies a func against elements up until index. If index is 1 and size is 3, will apply on [2, 3, 0]
     * @param func 
     * @param index 
     */
    applyUntil(func: (item: T, index: number) => void, index?: number) {
      index = index || this.currentIndex;
      for (let offset = 1; offset < this.size(); offset++) {
        const i = this.currentIndex + offset;
        const arr = this.arr;
        const peekIndex = i < arr.length ? i : 0;
  
        if (peekIndex === index) {
          break;
        }
  
        func(this.arr[peekIndex], peekIndex);
      }
  
    }
  
    /**
     * Applies a func against elements for X times. If limit is 1, size is 3, and index is 2. It will apply on [3]
     * @param func 
     * @param limit 
     */
    applyFor(func: (item: T, index: number) => void, limit: number) {
      for (let offset = 1; offset < limit; offset++) {
        const i = this.currentIndex + offset;
        const peekIndex = i < this.arr.length ? i : 0;
  
        func(this.arr[peekIndex], peekIndex);
      }
  
    }
  
  
  }