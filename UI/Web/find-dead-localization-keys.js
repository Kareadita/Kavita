const fs = require('fs');
const path = require('path');

// Configuration
const CONFIG = {
    i18nFile: './src/assets/langs/en.json',
    searchDirs: ['./src'],
    extensions: ['.ts', '.html'],
    // Namespaces to skip entirely (dynamic usage too complex to trace)
    blacklistedNamespaces: [
        'month-label-pipe',
        'ordinal-date-pipe',
    ],
    // Patterns that reference i18n keys
    usagePatterns: [
        // Transloco patterns
        /(?:^|[^a-zA-Z0-9_])t\(['"`]([a-zA-Z0-9._-]+)['"`]/g,  // t('key') or t('key', {params})
        /translocoService\.translate\(['"`]([a-zA-Z0-9._-]+)['"`]/g,  // translocoService.translate('key')
        /translocoService\.selectTranslate\(['"`]([a-zA-Z0-9._-]+)['"`]/g,
        /(?:^|[^a-zA-Z0-9_.])translate\(['"`]([a-zA-Z0-9._-]+)['"`]/g,  // translate('key') standalone function
        /transloco\.translate\(['"`]([a-zA-Z0-9._-]+)['"`]/g,  // this.transloco.translate('key')
        /\[transloco\]=['"`]([a-zA-Z0-9._-]+)['"`]/g,      // [transloco]="key"

        // Template literal translate: translate(`namespace.${var}`) or translate(`key`)
        /translate\(`([a-zA-Z0-9._-]+)`\)/g,
        /transloco\.translate\(`([a-zA-Z0-9._-]+)\./g,  // transloco.translate(`namespace. - captures namespace

        // ngx-translate fallback patterns
        /['"`]([a-zA-Z0-9._-]+)['"]\s*\|\s*translate/g,
        /translate\.instant\(['"`]([a-zA-Z0-9._-]+)['"`]/g,
        /translate\.get\(['"`]([a-zA-Z0-9._-]+)['"`]/g,
    ],
    // Pattern for interpolated keys within i18n values: {{key.path}}
    interpolationPattern: /\{\{([a-zA-Z0-9._-]+)\}\}/g,
    // Patterns indicating dynamic key construction - mark whole namespace as used
    dynamicKeyPatterns: [
        /translate\(['"`]([a-zA-Z0-9._-]+)\.\s*['"`]\s*\+/g,  // translate('namespace.' +
        /t\(['"`]([a-zA-Z0-9._-]+)\.\s*['"`]\s*\+/g,          // t('namespace.' +
        /translate\([a-zA-Z0-9_]+\s*\+\s*['"`]\.([a-zA-Z0-9._-]+)['"`]/g,  // translate(variable + '.suffix') - captures suffix
        /\+\s*['"`]\.([a-zA-Z0-9_-]+)['"]\s*[,)]/g,           // + '.suffix', or + '.suffix') - generic dynamic suffix
        /translate\(`([a-zA-Z0-9._-]+)\.\$\{/g,               // translate(`namespace.${...}`) - template literal dynamic
        /transloco\.translate\(`([a-zA-Z0-9._-]+)\.\$\{/g,    // this.transloco.translate(`namespace.${...}`)
    ],
};

// Check if a key belongs to a blacklisted namespace
function isBlacklisted(key) {
    for (const ns of CONFIG.blacklistedNamespaces) {
        if (key === ns || key.startsWith(ns + '.')) {
            return true;
        }
    }
    return false;
}

// Flatten nested JSON to dot-notation keys
function flattenKeys(obj, prefix = '') {
    const keys = [];
    for (const [k, v] of Object.entries(obj)) {
        const fullKey = prefix ? `${prefix}.${k}` : k;
        if (typeof v === 'object' && v !== null && !Array.isArray(v)) {
            keys.push(...flattenKeys(v, fullKey));
        } else {
            keys.push({ key: fullKey, value: v });
        }
    }
    return keys;
}

// Recursively get all files with given extensions
function getFiles(dir, exts, files = []) {
    if (!fs.existsSync(dir)) return files;
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
        const fullPath = path.join(dir, entry.name);
        if (entry.isDirectory() && !entry.name.startsWith('.') && entry.name !== 'node_modules') {
            getFiles(fullPath, exts, files);
        } else if (entry.isFile() && exts.includes(path.extname(entry.name))) {
            files.push(fullPath);
        }
    }
    return files;
}

// Extract Transloco prefixes from a file (and its sibling template/component)
function extractPrefixes(filePath, content, fileContentsCache) {
    const prefixes = [];

    // *transloco="let t; prefix: 'namespace'"
    const structuralPattern = /\*transloco\s*=\s*["'][^"']*prefix\s*:\s*['"]([a-zA-Z0-9._-]+)['"][^"']*["']/g;
    let match;
    while ((match = structuralPattern.exec(content)) !== null) {
        prefixes.push(match[1]);
    }

    // Also check for @Component transloco scope in .ts files
    // providers: [{ provide: TRANSLOCO_SCOPE, useValue: 'namespace' }]
    const scopePattern = /TRANSLOCO_SCOPE\s*,\s*useValue\s*:\s*['"]([a-zA-Z0-9._-]+)['"]/g;
    while ((match = scopePattern.exec(content)) !== null) {
        prefixes.push(match[1]);
    }

    // If this is a .ts file, check sibling .html for prefix
    if (filePath.endsWith('.ts')) {
        const htmlPath = filePath.replace(/\.ts$/, '.html');
        const htmlContent = fileContentsCache.get(htmlPath);
        if (htmlContent) {
            structuralPattern.lastIndex = 0;
            while ((match = structuralPattern.exec(htmlContent)) !== null) {
                prefixes.push(match[1]);
            }
        }
    }

    // If this is a .html file, check sibling .ts for TRANSLOCO_SCOPE
    if (filePath.endsWith('.html')) {
        const tsPath = filePath.replace(/\.html$/, '.ts');
        const tsContent = fileContentsCache.get(tsPath);
        if (tsContent) {
            scopePattern.lastIndex = 0;
            while ((match = scopePattern.exec(tsContent)) !== null) {
                prefixes.push(match[1]);
            }
        }
    }

    return [...new Set(prefixes)]; // dedupe
}

// Extract namespaces that use dynamic key construction
function extractDynamicNamespaces(content, allI18nKeys) {
    const namespaces = new Set();

    // Pattern: translate('namespace.' + or t('namespace.' +
    for (const pattern of CONFIG.dynamicKeyPatterns.slice(0, 2)) {
        pattern.lastIndex = 0;
        let match;
        while ((match = pattern.exec(content)) !== null) {
            namespaces.add(match[1]);
        }
    }

    // Pattern: translate(variable + '.suffix') - find all namespaces that have this suffix
    for (const pattern of CONFIG.dynamicKeyPatterns.slice(2)) {
        pattern.lastIndex = 0;
        let match;
        while ((match = pattern.exec(content)) !== null) {
            const suffix = match[1];
            // Find all namespaces that contain a key ending with this suffix
            for (const key of allI18nKeys) {
                if (key.endsWith('.' + suffix)) {
                    const ns = key.substring(0, key.lastIndexOf('.'));
                    // Only add top-level namespace
                    const topNs = ns.includes('.') ? ns.split('.')[0] : ns;
                    namespaces.add(topNs);
                }
            }
        }
    }

    return namespaces;
}

// Extract all i18n key usages from source files
function findUsedKeys(files, allI18nKeys, debug = false) {
    const used = new Set();
    const dynamicNamespaces = new Set();

    // Pre-load all file contents for sibling lookup
    const fileContentsCache = new Map();
    for (const file of files) {
        fileContentsCache.set(file, fs.readFileSync(file, 'utf-8'));
    }

    for (const file of files) {
        const content = fileContentsCache.get(file);
        const prefixes = extractPrefixes(file, content, fileContentsCache);

        // Debug: check specific file
        if (debug && file.includes('metadata-filter-row')) {
            console.log(`\n[DEBUG] File: ${file}`);
            console.log(`[DEBUG] Prefixes found: ${JSON.stringify(prefixes)}`);
            console.log(`[DEBUG] Contains 'disclaimer-file-size': ${content.includes('disclaimer-file-size')}`);
        }

        // Collect dynamic namespaces
        for (const ns of extractDynamicNamespaces(content, allI18nKeys)) {
            dynamicNamespaces.add(ns);
        }

        // Apply regex patterns
        for (const pattern of CONFIG.usagePatterns) {
            pattern.lastIndex = 0;
            let match;
            while ((match = pattern.exec(content)) !== null) {
                if (match[1]) {
                    const key = match[1];
                    used.add(key);
                    for (const prefix of prefixes) {
                        used.add(`${prefix}.${key}`);
                    }
                }
            }
        }

        // Direct string matching: check if any i18n key appears as a quoted string
        for (const key of allI18nKeys) {
            const shortKey = key.includes('.') ? key.split('.').slice(1).join('.') : key;

            if (content.includes(`'${key}'`) ||
                content.includes(`"${key}"`) ||
                content.includes(`\`${key}\``)) {
                used.add(key);
            }
            // Also check for the unprefixed version with valid prefixes
            if (shortKey !== key && prefixes.length > 0) {
                if (content.includes(`'${shortKey}'`) ||
                    content.includes(`"${shortKey}"`) ||
                    content.includes(`\`${shortKey}\``)) {
                    for (const prefix of prefixes) {
                        if (key.startsWith(prefix + '.')) {
                            used.add(key);
                            if (debug && key.includes('disclaimer-file-size')) {
                                console.log(`[DEBUG] Matched ${key} via prefix ${prefix} in ${file}`);
                            }
                        }
                    }
                }
            }
        }
    }

    // Mark all keys under dynamic namespaces as used
    for (const key of allI18nKeys) {
        for (const ns of dynamicNamespaces) {
            if (key.startsWith(ns + '.')) {
                used.add(key);
            }
        }
    }

    return used;
}

// Extract keys referenced via interpolation in i18n values
function findInterpolatedKeys(entries) {
    const referenced = new Set();
    for (const { value } of entries) {
        if (typeof value !== 'string') continue;
        let match;
        CONFIG.interpolationPattern.lastIndex = 0;
        while ((match = CONFIG.interpolationPattern.exec(value)) !== null) {
            referenced.add(match[1]);
        }
    }
    return referenced;
}

// Check if a key or any of its ancestors is used (prefix matching)
function isKeyUsed(key, usedKeys) {
    // Direct match
    if (usedKeys.has(key)) return true;
    // Check if this key is a prefix of any used key (parent namespace)
    for (const used of usedKeys) {
        if (used.startsWith(key + '.')) return true;
    }
    // Check if any used key is a prefix (dynamic access pattern)
    for (const used of usedKeys) {
        if (key.startsWith(used + '.')) return true;
    }
    return false;
}

// Group dead keys by their parent namespace
function groupByParent(deadKeys) {
    const grouped = {};
    for (const key of deadKeys) {
        const parts = key.split('.');
        if (parts.length === 1) {
            grouped['_root'] = grouped['_root'] || [];
            grouped['_root'].push(key);
        } else {
            const leaf = parts.pop();
            const parent = parts.join('.');
            grouped[parent] = grouped[parent] || [];
            grouped[parent].push(leaf);
        }
    }
    return grouped;
}

// Main
function main() {
    const DEBUG = process.argv.includes('--debug');

    console.log('Loading i18n file...');
    const i18n = JSON.parse(fs.readFileSync(CONFIG.i18nFile, 'utf-8'));
    const entries = flattenKeys(i18n);
    const allKeys = new Set(entries.map(e => e.key));
    console.log(`Found ${allKeys.size} i18n keys`);

    console.log('Scanning source files...');
    const files = CONFIG.searchDirs.flatMap(dir => getFiles(dir, CONFIG.extensions));
    console.log(`Scanning ${files.length} files...`);

    const usedInCode = findUsedKeys(files, [...allKeys], DEBUG);
    console.log(`Found ${usedInCode.size} key references in code`);

    const interpolated = findInterpolatedKeys(entries);
    console.log(`Found ${interpolated.size} interpolated key references`);

    const allUsed = new Set([...usedInCode, ...interpolated]);

    console.log('Finding dead keys...');
    const deadKeys = [];
    for (const key of allKeys) {
        if (isBlacklisted(key)) continue;
        if (!isKeyUsed(key, allUsed)) {
            deadKeys.push(key);
        }
    }

    console.log(`\nFound ${deadKeys.length} potentially dead keys`);

    const result = groupByParent(deadKeys.sort());

    const outputFile = 'dead-i18n-keys.json';
    fs.writeFileSync(outputFile, JSON.stringify(result, null, 2));
    console.log(`Results written to ${outputFile}`);

    // Also print summary
    if (deadKeys.length > 0) {
        console.log('\nDead keys by namespace:');
        for (const [ns, keys] of Object.entries(result)) {
            console.log(`  ${ns}: ${keys.length} keys`);
        }
    }
}

main();
