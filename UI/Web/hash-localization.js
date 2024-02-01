const crypto = require('crypto');
const fs = require('fs');
const glob = require('glob');

const jsonFilesDir = 'dist/browser/assets/langs/'; // Adjust the path to your JSON files
const outputDir = 'dist/browser/assets/langs'; // Directory to store minified files

function generateChecksum(str, algorithm, encoding) {
    return crypto
        .createHash(algorithm || 'md5')
        .update(str, 'utf8')
        .digest(encoding || 'hex');
}

const result = {};

// Remove file if it exists
const cacheBustingFilePath = './i18n-cache-busting.json';
if (fs.existsSync(cacheBustingFilePath)) {
    fs.unlinkSync(cacheBustingFilePath);
}

glob.sync(`${jsonFilesDir}**/*.json`).forEach(path => {
    let tokens = path.split('dist\\browser\\assets\\langs\\');
    if (tokens.length === 1) {
        tokens = path.split('dist/browser/assets/langs/');
    }
    const lang = tokens[1];
    const content = fs.readFileSync(path, { encoding: 'utf-8' });
    result[lang.replace('.json', '')] = generateChecksum(content);
});

fs.writeFileSync('./i18n-cache-busting.json', JSON.stringify(result));
fs.writeFileSync(`dist/browser/i18n-cache-busting.json`, JSON.stringify(result));
