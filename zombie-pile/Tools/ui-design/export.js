const { chromium } = require('/opt/node22/lib/node_modules/playwright');
const fs = require('fs');
(async () => {
  const b = await chromium.launch({ args: ['--use-gl=swiftshader'] });
  const manifest = {};
  for (const pg of ['sprites.html', 'sprites2.html', 'logo.html']) {
    const p = await b.newPage({ viewport: { width: 1400, height: 900 }, deviceScaleFactor: 2 });
    await p.goto('http://localhost:8765/ui/' + pg); await p.waitForTimeout(800);
    for (const h of await p.$$('.spr')) {
      const name = await h.getAttribute('data-name'); const border = await h.getAttribute('data-border');
      // pad the clip so outlines/shadows that spill outside the box are kept
      const bb = await h.boundingBox(); const pad = name === 'logo' ? 0 : (name.startsWith('ic_') || name === 'crosshair' ? 0 : 0);
      await p.screenshot({ path: `out/${name}.png`, omitBackground: true, clip: { x: bb.x - pad, y: bb.y - pad, width: bb.width + 2 * pad, height: bb.height + 2 * pad } });
      manifest[name] = { w: bb.width, h: bb.height, border: border ? border.split(',').map(Number) : null };
    }
    await p.close();
  }
  fs.writeFileSync('out/manifest.json', JSON.stringify(manifest, null, 1)); console.log(Object.keys(manifest).join(' '));
  await b.close();
})();
