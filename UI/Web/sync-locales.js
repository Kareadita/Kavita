const fs = require('fs');
const path = require('path');

function syncLocales() {
    const webDir = path.resolve(__dirname);
    const langDir = path.join(webDir, 'src', 'assets', 'langs');
    const sourceFile = path.join(langDir, 'en.json');

    console.log('Web directory:', webDir);
    console.log('Language directory:', langDir);
    console.log('Source file:', sourceFile);

    if (!fs.existsSync(sourceFile)) {
        console.error(`Source file not found: ${sourceFile}`);
        process.exit(1);
    }

    const sourceData = JSON.parse(fs.readFileSync(sourceFile, 'utf8'));
    const localeFiles = fs.readdirSync(langDir).filter(file => file.endsWith('.json') && file !== 'en.json');

    localeFiles.forEach(localeFile => {
        const filePath = path.join(langDir, localeFile);
        console.log(`Processing: ${filePath}`);

        if (!fs.existsSync(filePath)) {
            console.error(`File not found: ${filePath}`);
            return;
        }

        let localeData = JSON.parse(fs.readFileSync(filePath, 'utf8'));
        let updated = false;

        function updateNestedObject(source, target, parentKeys = []) {
            const updatedTarget = {};
            Object.keys(source).forEach(key => {
                const fullKeyPath = [...parentKeys, key].join('.'); // Track parent keys
                if (typeof source[key] === 'object' && source[key] !== null) {
                    if (!target[key] || Object.keys(target[key]).length === 0) {
                        updatedTarget[key] = {};
                        updated = true;
                        console.log(`Added new object for key: ${fullKeyPath}`);
                    } else {
                        updatedTarget[key] = target[key];
                    }
                    updatedTarget[key] = updateNestedObject(source[key], updatedTarget[key], [...parentKeys, key]);
                } else {
                    if (typeof source[key] === 'string') {
                        if (source[key].match(/{{.+\..+}}/)) {
                            if (target[key] !== source[key]) {
                                updatedTarget[key] = source[key];
                                updated = true;
                                console.log(`Updated key: ${fullKeyPath}`);
                            } else {
                                updatedTarget[key] = target[key];
                            }
                        } else if (!target.hasOwnProperty(key)) {
                            updatedTarget[key] = '';
                            updated = true;
                            console.log(`Added empty string for key: ${fullKeyPath}`);
                        } else {
                            updatedTarget[key] = target[key];
                        }
                    }
                }
            });
            return updatedTarget;
        }

        localeData = updateNestedObject(sourceData, localeData);

        if (updated) {
            fs.writeFileSync(filePath, JSON.stringify(localeData, null, 2));
            console.log(`Updated ${localeFile}`);
        } else {
            console.log(`No updates needed for ${localeFile}`);
        }
    });
}

syncLocales();
