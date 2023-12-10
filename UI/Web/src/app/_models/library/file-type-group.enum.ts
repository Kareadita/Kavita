export enum FileTypeGroup {
  Archive = 1,
  Epub = 2,
  Pdf = 3,
  Images = 4
}

export const allFileTypeGroup = Object.keys(FileTypeGroup)
.filter(key => !isNaN(Number(key)) && parseInt(key, 10) >= 0)
.map(key => parseInt(key, 10)) as FileTypeGroup[];
