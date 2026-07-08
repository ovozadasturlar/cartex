const fs = require('fs');
const path = require('path');

const source = path.resolve(__dirname, '../../../desktop/Cartex.UI/Assets/Languages');
const target = path.resolve(__dirname, '../public/i18n');

fs.mkdirSync(target, { recursive: true });
for (const file of fs.readdirSync(source).filter(f => f.endsWith('.json'))) {
  fs.copyFileSync(path.join(source, file), path.join(target, file));
}
console.log(`i18n: ${fs.readdirSync(target).length} files copied from desktop`);
