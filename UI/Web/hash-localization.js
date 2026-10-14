const crypto = require('crypto');
const fs = require('fs');
const path = require('path');

const langsDir = 'src/assets/langs';

function generateChecksum(str, algorithm, encoding) {
    return crypto
        .createHash(algorithm || 'md5')
        .update(str, 'utf8')
        .digest(encoding || 'hex');
}

const result = {};

fs.readdirSync(langsDir)
    .filter(file => file.endsWith('.json'))
    .forEach(file => {
        const content = fs.readFileSync(path.join(langsDir, file), { encoding: 'utf-8' });
        result[path.basename(file, '.json')] = generateChecksum(content);
    });

fs.writeFileSync('./i18n-cache-busting.json', JSON.stringify(result));
