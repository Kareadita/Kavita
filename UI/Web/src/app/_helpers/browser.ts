export const isSafari = [
    'iPad Simulator',
    'iPhone Simulator',
    'iPod Simulator',
    'iPad',
    'iPhone',
    'iPod'
  ].includes(navigator.platform)
  // iPad on iOS 13 detection
  || (navigator.userAgent.includes("Mac") && "ontouchend" in document);

/**
 * Represents a Version for a browser
 */
export class Version {
  major: number;
  minor: number;
  patch: number;

  constructor(major: number, minor: number, patch: number) {
    this.major = major;
    this.minor = minor;
    this.patch = patch;
  }

  isLessThan(other: Version): boolean {
    if (this.major < other.major) return true;
    if (this.major > other.major) return false;
    if (this.minor < other.minor) return true;
    if (this.minor > other.minor) return false;
    return this.patch < other.patch;
  }

  isGreaterThan(other: Version): boolean {
    if (this.major > other.major) return true;
    if (this.major < other.major) return false;
    if (this.minor > other.minor) return true;
    if (this.minor < other.minor) return false;
    return this.patch > other.patch;
  }

  isEqualTo(other: Version): boolean {
    return (
      this.major === other.major &&
      this.minor === other.minor &&
      this.patch === other.patch
    );
  }
}


export const getIosVersion = () => {
  const match = navigator.userAgent.match(/OS (\d+)_(\d+)_?(\d+)?/);
  if (match) {
    const major = parseInt(match[1], 10);
    const minor = parseInt(match[2], 10);
    const patch = parseInt(match[3] || '0', 10);

    return new Version(major, minor, patch);
  }
  return null;
}
