import { Injectable } from '@angular/core';
import { JumpKey } from '../_models/jumpbar/jump-key';

const keySize = 25; // Height of the JumpBar button

@Injectable({
  providedIn: 'root'
})
export class JumpbarService {

  resumeKeys: {[key: string]: string} = {};
  // Used for custom filtered urls
  resumeScroll: {[key: string]: number} = {};

  constructor() { }


  getResumeKey(key: string) {
    const k = key.toUpperCase();
    if (this.resumeKeys.hasOwnProperty(k)) return this.resumeKeys[k];
    return '';
  }

  getResumePosition(url: string) {
    if (this.resumeScroll.hasOwnProperty(url)) return this.resumeScroll[url];
    return 0;
  }

  saveResumeKey(key: string, value: string) {
    const k = key.toUpperCase();
    this.resumeKeys[k] = value;
  }

  saveResumePosition(url: string, value: number) {
    this.resumeScroll[url] = value;
  }

  generateJumpBar(jumpBarKeys: Array<JumpKey>, currentSize: number) {
    const fullSize = (jumpBarKeys.length * keySize);
    if (currentSize >= fullSize) {
      return [...jumpBarKeys];
    }

    const jumpBarKeysToRender: Array<JumpKey> = [];
    const targetNumberOfKeys = parseInt(Math.floor(currentSize / keySize) + '', 10);
    const removeCount = jumpBarKeys.length - targetNumberOfKeys - 3;
    if (removeCount <= 0) return [...jumpBarKeys];

    const removalTimes = Math.ceil(removeCount / 2);
    const midPoint = Math.floor(jumpBarKeys.length / 2);
    jumpBarKeysToRender.push(jumpBarKeys[0]);
    this._removeFirstPartOfJumpBar(midPoint, removalTimes, jumpBarKeys, jumpBarKeysToRender);
    jumpBarKeysToRender.push(jumpBarKeys[midPoint]);
    this._removeSecondPartOfJumpBar(midPoint, removalTimes, jumpBarKeys, jumpBarKeysToRender);
    jumpBarKeysToRender.push(jumpBarKeys[jumpBarKeys.length - 1]);

    return jumpBarKeysToRender;
  }

  _removeSecondPartOfJumpBar(midPoint: number, numberOfRemovals: number = 1, jumpBarKeys: Array<JumpKey>, jumpBarKeysToRender: Array<JumpKey>) {
    const removedIndexes: Array<number> = [];
    for(let removal = 0; removal < numberOfRemovals; removal++) {
      let min = 100000000;
      let minIndex = -1;
      for(let i = midPoint + 1; i < jumpBarKeys.length - 2; i++) {
        if (jumpBarKeys[i].size < min && !removedIndexes.includes(i)) {
          min = jumpBarKeys[i].size;
          minIndex = i;
        }
      }
      removedIndexes.push(minIndex);
    }
    for(let i = midPoint + 1; i < jumpBarKeys.length - 2; i++) {
      if (!removedIndexes.includes(i)) jumpBarKeysToRender.push(jumpBarKeys[i]);
    }
  }

  _removeFirstPartOfJumpBar(midPoint: number, numberOfRemovals: number = 1, jumpBarKeys: Array<JumpKey>, jumpBarKeysToRender: Array<JumpKey>) {
    const removedIndexes: Array<number> = [];

    for(let removal = 0; removal < numberOfRemovals; removal++) {
      let min = 100000000;
      let minIndex = -1;

      for(let i = 1; i < midPoint; i++) {
        if (jumpBarKeys[i].size < min && !removedIndexes.includes(i)) {
          min = jumpBarKeys[i].size;
          minIndex = i;
        }
      }
      removedIndexes.push(minIndex);
    }

    for(let i = 1; i < midPoint; i++) {
      if (!removedIndexes.includes(i)) jumpBarKeysToRender.push(jumpBarKeys[i]);
    }
  }

  /**
   *
   * @param data An array of objects
   * @param keySelector A method to fetch a string from the object, which is used to classify the JumpKey
   * @returns
   */
   getJumpKeys(data :Array<any>, keySelector: (data: any) => string) {
    const keys: {[key: string]: number} = {};
    data.forEach(obj => {
      let ch = keySelector(obj).charAt(0).toUpperCase();
      if (/\d|\#|!|%|@|\(|\)|\^|\.|_|\*/g.test(ch)) {
        ch = '#';
      }
      if (!keys.hasOwnProperty(ch)) {
        keys[ch] = 0;
      }
      keys[ch] += 1;
    });
    return Object.keys(keys).map(k => {
      k = k.toUpperCase();
      return {
        key: k,
        size: keys[k],
        title: k
      }
    }).sort((a, b) => {
      if (a.key < b.key) return -1;
      if (a.key > b.key) return 1;
      return 0;
    });
  }
}
