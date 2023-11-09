const fs = require('fs');
const jsonminify = require('jsonminify');

const jsonFilesDir = 'dist/browser/assets/langs'; // Adjust the path to your JSON files
const outputDir = 'dist/browser/assets/langs'; // Directory to store minified files

fs.readdirSync(jsonFilesDir).forEach(file => {
    if (file.endsWith('.json')) {
        const filePath = `${jsonFilesDir}/${file}`;
        const content = fs.readFileSync(filePath, 'utf8');
        const minifiedContent = jsonminify(content);
        const outputFile = `${outputDir}/${file}`;
        fs.writeFileSync(outputFile, minifiedContent, 'utf8');
        console.log(`Minified: ${file}`);
    }
});
