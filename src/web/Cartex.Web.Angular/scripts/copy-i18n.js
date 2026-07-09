const fs = require('fs');
const path = require('path');

const source = path.resolve(__dirname, '../../../desktop/Cartex.UI/Assets/Languages');
const additions = path.resolve(__dirname, '../src/i18n');
const target = path.resolve(__dirname, '../public/i18n');

fs.mkdirSync(target, { recursive: true });
for (const file of fs.readdirSync(source).filter(f => f.endsWith('.json'))) {
  const base = JSON.parse(fs.readFileSync(path.join(source, file), 'utf8'));
  const addPath = path.join(additions, file);
  const extra = fs.existsSync(addPath) ? JSON.parse(fs.readFileSync(addPath, 'utf8')) : {};
  fs.writeFileSync(path.join(target, file), JSON.stringify({ ...base, ...extra }, null, 2));
}
console.log(`i18n: ${fs.readdirSync(target).length} files merged from desktop base + web additions`);
