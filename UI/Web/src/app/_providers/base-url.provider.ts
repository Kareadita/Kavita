export function getBaseUrl() : string {
  return document.getElementsByTagName('base')[0]?.getAttribute('href') || '/';
}
