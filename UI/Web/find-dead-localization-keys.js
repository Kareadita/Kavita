const fs = require('fs');
const path = require('path');

// Configuration
const CONFIG = {
    i18nFile: './src/assets/langs/en.json',
    searchDirs: ['./src'],
    extensions: ['.ts', '.html'],
    // Patterns that reference i18n keys
    usagePatterns: [
        // Transloco patterns
        /(?:^|[^a-zA-Z0-9_])t\(['"`]([a-zA-Z0-9._-]+)['"`]/g,  // t('key') or t('key', {params})
        /translocoService\.translate\(['"`]([a-zA-Z0-9._-]+)['"`]/g,  // translocoService.translate('key')
        /translocoService\.selectTranslate\(['"`]([a-zA-Z0-9._-]+)['"`]/g,
        /\|\s*transloco(?:\s*:\s*\{[^}]*\})?/g,            // | transloco pipe (key before pipe)
        /\[transloco\]=['"`]([a-zA-Z0-9._-]+)['"`]/g,      // [transloco]="key"

        // String literals in component metadata / configs (description:, tooltip:, etc.)
        /:\s*['"`]([a-zA-Z0-9][a-zA-Z0-9_-]*(?:-[a-zA-Z0-9_]+)+)['"`]/g,  // kebab-case keys as values

        // ngx-translate fallback patterns
        /['"`]([a-zA-Z0-9._-]+)['"]\s*\|\s*translate/g,
        /translate\.instant\(['"`]([a-zA-Z0-9._-]+)['"`]/g,
        /translate\.get\(['"`]([a-zA-Z0-9._-]+)['"`]/g,
    ],
    // Pattern for interpolated keys within i18n values: {{key.path}}
    interpolationPattern: /\{\{([a-zA-Z0-9._-]+)\}\}/g,
};

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

// Extract Transloco prefixes from a file
function extractPrefixes(content) {
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

    return prefixes;
}

// Extract all i18n key usages from source files
function findUsedKeys(files, allI18nKeys) {
    const used = new Set();

    for (const file of files) {
        const content = fs.readFileSync(file, 'utf-8');
        const prefixes = extractPrefixes(content);

        // Apply regex patterns
        for (const pattern of CONFIG.usagePatterns) {
            pattern.lastIndex = 0;
            let match;
            while ((match = pattern.exec(content)) !== null) {
                if (match[1]) {
                    const key = match[1];
                    // Add the raw key (for fully qualified keys)
                    used.add(key);
                    // Add prefixed versions
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
                        }
                    }
                }
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
    console.log('Loading i18n file...');
    const i18n = JSON.parse(fs.readFileSync(CONFIG.i18nFile, 'utf-8'));
    const entries = flattenKeys(i18n);
    const allKeys = new Set(entries.map(e => e.key));
    console.log(`Found ${allKeys.size} i18n keys`);

    console.log('Scanning source files...');
    const files = CONFIG.searchDirs.flatMap(dir => getFiles(dir, CONFIG.extensions));
    console.log(`Scanning ${files.length} files...`);

    const usedInCode = findUsedKeys(files, [...allKeys]);
    console.log(`Found ${usedInCode.size} key references in code`);

    const interpolated = findInterpolatedKeys(entries);
    console.log(`Found ${interpolated.size} interpolated key references`);

    const allUsed = new Set([...usedInCode, ...interpolated]);

    console.log('Finding dead keys...');
    const deadKeys = [];
    for (const key of allKeys) {
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
